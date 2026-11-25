// 3D STEP viewer (FR-MA-2, SR-MA-2, #351) for the Model Analysis proof of concept.
// MG module M20 (3D Viewer); axis-view access and the view cube extend M20 per issue #351.
//
// Flow for each load (the server already validated the file on upload, NFR-MA-2):
//   1. download the bytes for the stored model by URL (AbortController-cancellable);
//   2. tessellate them in the browser with occt-import-js (Open CASCADE as WASM),
//      lazily loaded once per page and cached;
//   3. measure the mesh (stepMeasure.js) and render it with three.js.
// The result object (including the timing breakdown) goes back to the Blazor
// component, which shows the summary.
//
// Exports (fixed contract, see IMPL-BRIEF.md / POC-storage-plan.md §4.3.2):
//   init(host, dotnet)                     — create the scene; status via dotnet.ReportStatus
//   loadModel(host, { name, url })          — Promise<{ ok, aborted?, error?, measurements?, timings? }>
//   clear(host)                             — remove the current model
//   setView(host, { bounds?, edges?, grid? }) — visibility toggles
//   snapView(host, axis)                    — axis ∈ '+x' '-x' '+y' '-y' '+z' '-z' 'iso' (look direction); ~300 ms animated camera
//   dispose(host)                           — tear the scene down
//
// View cube (FR-MA-2, #351): a clickable cube in the top-right corner, on its own overlay canvas so
// pointer events never reach OrbitControls. Clicking a face views the model FROM that side, which is
// snapView with the opposite look direction (+Z face -> snapView('-z')). Hovering highlights the face.
// Built and disposed inside init()/dispose(); the Blazor toolbar only holds Iso and the display toggles.
//
// `measurements` = measure()'s result plus `gridCellMm`.
// `timings` = { downloadMs, kernelMs, tessellateMs, measureMs, renderMs, totalMs };
// `kernelMs` is the time this call waited for the occt WASM (0 once cached) — the
// first-load WASM fetch previously hid inside an undifferentiated total (NFR-MA-2).
// Starting a new loadModel aborts any in-flight one; the aborted call resolves
// { ok: false, aborted: true } without touching the scene.

import * as THREE from '../lib/three/three.module.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { measure } from './stepMeasure.js';

const OCCT_BASE = '/lib/occt-import-js/';
let occtPromise = null;

/** Loads the occt-import-js WASM module once per page. */
function loadOcct() {
    if (occtPromise) return occtPromise;
    occtPromise = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = OCCT_BASE + 'occt-import-js.js';
        script.onload = () => globalThis.occtimportjs({ locateFile: f => OCCT_BASE + f }).then(resolve, reject);
        script.onerror = () => reject(new Error('Could not load occt-import-js.'));
        document.head.appendChild(script);
    }).catch(e => { occtPromise = null; throw e; });
    return occtPromise;
}

const viewers = new WeakMap();

// Each entry names the direction the camera LOOKS IN (so '+z' sits on the −Z side looking toward +Z).
// `cam` is the unit direction from the model to the camera, which is the negation of the look direction
// for the axis views; `iso` is the default frame, which looks along −(1, 0.8, 1.2).
// `up` is the screen-up vector: +Y for the X/Z views (matches the grid in the XZ plane); the ±Y views
// look straight down/up the Y axis, so screen-up switches to +Z there or OrbitControls' internal
// frame degenerates when up is parallel to the view.
const SNAP_AXES = {
    '+x': { cam: [-1, 0, 0], up: [0, 1, 0] },
    '-x': { cam: [1, 0, 0], up: [0, 1, 0] },
    '+y': { cam: [0, -1, 0], up: [0, 0, 1] },
    '-y': { cam: [0, 1, 0], up: [0, 0, 1] },
    '+z': { cam: [0, 0, -1], up: [0, 1, 0] },
    '-z': { cam: [0, 0, 1], up: [0, 1, 0] },
    iso: { cam: [1, 0.8, 1.2], up: [0, 1, 0] },
};

/** Creates the three.js scene inside `host`. `dotnet` receives status messages (may be null). */
export function init(host, dotnet) {
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    host.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xf4f5f7);
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 1e6);
    camera.position.set(200, 150, 200);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8f99, 1.6));
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    camera.add(sun);
    sun.position.set(1, 2, 3);
    scene.add(camera);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.screenSpacePanning = true;

    const indicator = buildAxisIndicator();
    const cube = buildViewCube(host, axis => snapView(host, axis));

    const resize = () => {
        const w = host.clientWidth, h = host.clientHeight || 1;
        renderer.setSize(w, h, false);
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
    };
    const observer = new ResizeObserver(resize);
    observer.observe(host);
    resize();

    let running = true;
    const loop = () => {
        if (!running) return;
        controls.update();

        const w = host.clientWidth, h = host.clientHeight || 1;
        renderer.setViewport(0, 0, w, h);
        renderer.setScissorTest(false);
        renderer.autoClear = true;
        renderer.render(scene, camera);

        // Bottom-left axis indicator: second scissored viewport, same renderer.
        const size = Math.max(80, Math.min(110, Math.round(Math.min(w, h) * 0.18)));
        const margin = 12;
        positionIndicatorCamera(indicator, camera, controls.target);
        renderer.autoClear = false;
        renderer.clearDepth();
        renderer.setViewport(margin, margin, size, size);
        renderer.setScissor(margin, margin, size, size);
        renderer.setScissorTest(true);
        renderer.render(indicator.scene, indicator.camera);
        renderer.setScissorTest(false);
        renderer.autoClear = true;

        // View cube: its own canvas and renderer, posed like the main camera.
        cube.update(camera, controls.target);

        requestAnimationFrame(loop);
    };
    loop();

    viewers.set(host, {
        host, dotnet, renderer, scene, camera, controls, indicator, cube,
        model: null, loadToken: 0, abortController: null, snapAnim: null,
        dispose() {
            running = false;
            observer.disconnect();
            if (this.snapAnim) cancelAnimationFrame(this.snapAnim.frameId);
            this.abortController?.abort();
            controls.dispose();
            cube.dispose();
            clearModel(this);
            renderer.dispose();
            renderer.domElement.remove();
        },
    });
    // Start fetching the 7 MB WASM now so the first load is faster.
    loadOcct().catch(() => { });
}

export function dispose(host) {
    viewers.get(host)?.dispose();
    viewers.delete(host);
}

/** Loads the model's STEP bytes from `url`. Aborts any load already in flight on `host`. */
export async function loadModel(host, { name, url }) {
    const v = viewers.get(host);
    if (!v) return { ok: false, error: 'Viewer is not initialised.' };

    v.abortController?.abort();
    const controller = new AbortController();
    v.abortController = controller;
    const token = ++v.loadToken;
    const stale = () => token !== v.loadToken;

    const status = msg => v.dotnet?.invokeMethodAsync('ReportStatus', msg).catch(() => { });
    const t0 = performance.now();
    const timings = {};

    try {
        // 1. Download.
        status('Downloading model…');
        let t = performance.now();
        const res = await fetch(url, { signal: controller.signal });
        if (!res.ok) {
            const body = await res.json().catch(() => ({}));
            throw new Error(body.message ?? `Could not download '${name}' (${res.status}).`);
        }
        const bytes = new Uint8Array(await res.arrayBuffer());
        timings.downloadMs = performance.now() - t;
        if (stale()) return { ok: false, aborted: true };

        // 2. Geometry kernel (cached after the first call).
        status('Loading geometry kernel…');
        t = performance.now();
        const occt = await loadOcct();
        timings.kernelMs = performance.now() - t;
        if (stale()) return { ok: false, aborted: true };

        // 3. Tessellation.
        status('Tessellating model…');
        await nextFrame(); // let the status paint before the synchronous WASM call
        t = performance.now();
        const result = occt.ReadStepFile(bytes, { linearUnit: 'millimeter' });
        timings.tessellateMs = performance.now() - t;
        if (stale()) return { ok: false, aborted: true };
        if (!result.success || !result.meshes?.length)
            throw new Error(`'${name}' contains no readable geometry.`);

        // 4. Measure and render.
        status('Measuring and rendering…');
        t = performance.now();
        const measurements = measure(result.meshes);
        timings.measureMs = performance.now() - t;
        if (stale()) return { ok: false, aborted: true };

        t = performance.now();
        showModel(v, result.meshes, measurements);
        await nextFrame();
        timings.renderMs = performance.now() - t;
        timings.totalMs = performance.now() - t0;

        return { ok: true, measurements, timings };
    } catch (e) {
        if (stale()) return { ok: false, aborted: true };
        clearModel(v);
        return { ok: false, error: e?.message ?? String(e) };
    }
}

/** Removes the current model, if any. */
export function clear(host) {
    const v = viewers.get(host);
    if (!v) return;
    v.loadToken++; // invalidate any in-flight loadModel
    v.abortController?.abort();
    clearModel(v);
}

export function setView(host, opts) {
    const v = viewers.get(host);
    if (!v?.model) return;
    if (opts.bounds !== undefined) v.model.bounds.visible = opts.bounds;
    if (opts.edges !== undefined) v.model.edges.forEach(e => e.visible = opts.edges);
    if (opts.grid !== undefined) v.model.grid.visible = opts.grid;
}

/** Animates the camera to look along `axis` ('+x' '-x' '+y' '-y' '+z' '-z' 'iso'), framed to fit. No-op without a model. */
export function snapView(host, axis) {
    const v = viewers.get(host);
    if (!v?.model) return;
    const spec = SNAP_AXES[axis];
    if (!spec) return;

    const { box } = v.model;
    const center = box.getCenter(new THREE.Vector3());
    const radius = box.getSize(new THREE.Vector3()).length() / 2 || 1;
    const dist = radius / Math.sin(THREE.MathUtils.degToRad(v.camera.fov / 2));
    const dir = new THREE.Vector3(...spec.cam).normalize();
    const targetPos = center.clone().addScaledVector(dir, dist);
    const targetUp = new THREE.Vector3(...spec.up);

    animateCamera(v, targetPos, center, targetUp);
}

function animateCamera(v, targetPos, targetTarget, targetUp) {
    if (v.snapAnim) cancelAnimationFrame(v.snapAnim.frameId);

    const startPos = v.camera.position.clone();
    const startTarget = v.controls.target.clone();
    const startUp = v.camera.up.clone();
    const duration = 300;
    const t0 = performance.now();
    const ease = t => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);

    const anim = { frameId: 0 };
    const step = () => {
        const t = Math.min((performance.now() - t0) / duration, 1);
        const e = ease(t);
        v.camera.position.lerpVectors(startPos, targetPos, e);
        v.controls.target.lerpVectors(startTarget, targetTarget, e);
        v.camera.up.copy(startUp).lerp(targetUp, e).normalize();
        v.camera.updateProjectionMatrix();
        v.controls.update();
        if (t < 1) anim.frameId = requestAnimationFrame(step);
        else v.snapAnim = null;
    };
    v.snapAnim = anim;
    anim.frameId = requestAnimationFrame(step);
}

const luminance = c => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;

const nextFrame = () => new Promise(r => requestAnimationFrame(() => setTimeout(r, 0)));

function clearModel(v) {
    if (!v.model) return;
    v.scene.remove(v.model.root);
    v.model.root.traverse(o => {
        o.geometry?.dispose();
        (Array.isArray(o.material) ? o.material : [o.material]).forEach(m => m?.dispose());
    });
    v.model = null;
}

function showModel(v, meshes, m) {
    clearModel(v);
    const root = new THREE.Group();
    const edges = [];

    for (const mesh of meshes) {
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.Float32BufferAttribute(mesh.attributes.position.array, 3));
        if (mesh.attributes.normal) g.setAttribute('normal', new THREE.Float32BufferAttribute(mesh.attributes.normal.array, 3));
        else g.computeVertexNormals();
        g.setIndex(mesh.index.array);

        // Use the CAD colour unless it is too dark to show shading and edges.
        const fileColor = mesh.color ? new THREE.Color(...mesh.color) : null;
        const color = fileColor && luminance(fileColor) > 0.25 ? fileColor : new THREE.Color(0xb8c2cc);
        const material = new THREE.MeshStandardMaterial({
            color, metalness: 0.05, roughness: 0.75, side: THREE.DoubleSide,
            polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
        });
        root.add(new THREE.Mesh(g, material));

        // Feature edges give the CAD look and make small features readable.
        const e = new THREE.LineSegments(new THREE.EdgesGeometry(g, 25), new THREE.LineBasicMaterial({ color: 0x2b313a }));
        edges.push(e);
        root.add(e);
    }

    const box = new THREE.Box3(new THREE.Vector3(...m.min), new THREE.Vector3(...m.max));
    const bounds = new THREE.Box3Helper(box, 0xd9480f);
    root.add(bounds);

    // Grid on the plane under the model, sized to it, with a round cell size (SR-MA-2).
    const maxDim = Math.max(...m.size);
    const cell = Math.pow(10, Math.floor(Math.log10(maxDim / 4)));
    const span = Math.ceil((maxDim * 1.5) / cell) * cell;
    const grid = new THREE.GridHelper(span, span / cell, 0x7a828c, 0xc9ced4);
    const center = box.getCenter(new THREE.Vector3());
    grid.position.set(center.x, m.min[1], center.z);
    root.add(grid);
    // (No AxesHelper here any more — the labelled bottom-left indicator replaces it.)

    v.scene.add(root);
    v.model = { root, bounds, edges, grid, box, gridCellMm: cell };
    frame(v);
    m.gridCellMm = cell;
}

/** Points the camera at the model so it fills the view (today's default / 'iso' framing). */
function frame(v) {
    const { box } = v.model;
    const center = box.getCenter(new THREE.Vector3());
    const radius = box.getSize(new THREE.Vector3()).length() / 2 || 1;
    const dist = radius / Math.sin(THREE.MathUtils.degToRad(v.camera.fov / 2));
    const dir = new THREE.Vector3(...SNAP_AXES.iso.cam).normalize();
    v.camera.up.set(...SNAP_AXES.iso.up);
    v.camera.position.copy(center).addScaledVector(dir, dist);
    v.camera.near = dist / 1000;
    v.camera.far = dist * 100;
    v.camera.updateProjectionMatrix();
    v.controls.target.copy(center);
    v.controls.update();
}

// ---- View cube (FR-MA-2, #351) ---------------------------------------------

const CUBE_PX = 100;        // CSS size; keep in sync with .tt-view-cube in app.css
const CUBE_DISTANCE = 6.5;  // camera distance from the cube centre (cube half-size is 1)
// One textured plane per face. `n` is the outward normal; `up` is the label's screen-up when that
// face is turned to the viewer: +Y for the X and Z faces (same as the X/Z views), +Z for the Y faces
// (same as the ±Y views). The label's right direction is up × n, so every label reads upright from outside.
const CUBE_FACE_SPEC = [
    { face: '+x', n: [1, 0, 0], up: [0, 1, 0] },
    { face: '-x', n: [-1, 0, 0], up: [0, 1, 0] },
    { face: '+y', n: [0, 1, 0], up: [0, 0, 1] },
    { face: '-y', n: [0, -1, 0], up: [0, 0, 1] },
    { face: '+z', n: [0, 0, 1], up: [0, 1, 0] },
    { face: '-z', n: [0, 0, -1], up: [0, 1, 0] },
];
const CUBE_LABELS = { '+x': '+X', '-x': '−X', '+y': '+Y', '-y': '−Y', '+z': '+Z', '-z': '−Z' };
// A face names the side the camera is ON: clicking +Z views from +Z, i.e. looks along -Z.
const FACE_TO_SNAP = { '+x': '-x', '-x': '+x', '+y': '-y', '-y': '+y', '+z': '-z', '-z': '+z' };

/** Builds the view cube in the top-right of `host`. `onSnap(axis)` receives the snapView axis for a clicked face. */
function buildViewCube(host, onSnap) {
    const wrap = document.createElement('div');
    wrap.className = 'tt-view-cube';
    const canvas = document.createElement('canvas');
    wrap.appendChild(canvas);
    host.appendChild(wrap);

    const accent = getComputedStyle(host).getPropertyValue('--tt-accent').trim() || '#7a003c';
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
    renderer.setClearColor(0x000000, 0);
    renderer.setPixelRatio(window.devicePixelRatio || 1);
    renderer.setSize(CUBE_PX, CUBE_PX, false);

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 100);

    const faces = CUBE_FACE_SPEC.map(spec => {
        const normal = new THREE.Vector3(...spec.n);
        const up = new THREE.Vector3(...spec.up);
        const right = new THREE.Vector3().crossVectors(up, normal);
        const texNormal = makeCubeFaceTexture(CUBE_LABELS[spec.face], '#eef0f3', '#1f2933');
        const texHover = makeCubeFaceTexture(CUBE_LABELS[spec.face], accent, '#ffffff');
        const material = new THREE.MeshBasicMaterial({ map: texNormal });
        const geometry = new THREE.PlaneGeometry(2, 2);
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.copy(normal);
        mesh.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(right, up, normal));
        mesh.userData.face = spec.face;
        scene.add(mesh);
        return { face: spec.face, mesh, material, texNormal, texHover };
    });
    const byFace = Object.fromEntries(faces.map(f => [f.face, f]));

    const boxEdges = new THREE.EdgesGeometry(new THREE.BoxGeometry(2, 2, 2));
    const edgeMaterial = new THREE.LineBasicMaterial({ color: 0x6b7480 });
    scene.add(new THREE.LineSegments(boxEdges, edgeMaterial));

    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const meshes = faces.map(f => f.mesh);
    let hovered = null;
    let pointerClient = null; // last client position over the cube, re-tested each frame as the cube turns

    const faceAt = (clientX, clientY) => {
        const r = canvas.getBoundingClientRect();
        pointer.set(((clientX - r.left) / r.width) * 2 - 1, -((clientY - r.top) / r.height) * 2 + 1);
        raycaster.setFromCamera(pointer, camera);
        const hit = raycaster.intersectObjects(meshes, false)[0];
        return hit ? hit.object.userData.face : null;
    };

    const setHovered = face => {
        if (face === hovered) return;
        if (hovered) byFace[hovered].material.map = byFace[hovered].texNormal;
        hovered = face;
        if (face) {
            byFace[face].material.map = byFace[face].texHover;
            canvas.title = `View from ${CUBE_LABELS[face]}`;
        } else {
            canvas.title = '';
        }
        canvas.style.cursor = face ? 'pointer' : 'default';
    };

    const onMove = e => {
        pointerClient = [e.clientX, e.clientY];
        setHovered(faceAt(e.clientX, e.clientY));
    };
    const onLeave = () => {
        pointerClient = null;
        setHovered(null);
    };
    // A press that moves (a drag over the cube) is not a click, so it never snaps the view.
    let pressAt = null;
    const onDown = e => { pressAt = [e.clientX, e.clientY]; };
    const onClick = e => {
        if (pressAt && Math.hypot(e.clientX - pressAt[0], e.clientY - pressAt[1]) > 4) return;
        const face = faceAt(e.clientX, e.clientY);
        if (face) onSnap(FACE_TO_SNAP[face]);
    };
    canvas.addEventListener('pointerdown', onDown);
    canvas.addEventListener('pointermove', onMove);
    canvas.addEventListener('pointerleave', onLeave);
    canvas.addEventListener('click', onClick);
    canvas.style.touchAction = 'none';

    return {
        /** Poses the cube like the main camera (same rotation, cube centred on the origin) and draws it. */
        update(mainCamera, target) {
            const dpr = window.devicePixelRatio || 1;
            if (renderer.getPixelRatio() !== dpr) {
                renderer.setPixelRatio(dpr);
                renderer.setSize(CUBE_PX, CUBE_PX, false);
            }
            camera.position.copy(mainCamera.position).sub(target);
            if (camera.position.lengthSq() < 1e-9) camera.position.set(1, 1, 1);
            camera.position.normalize().multiplyScalar(CUBE_DISTANCE);
            camera.up.copy(mainCamera.up);
            camera.lookAt(0, 0, 0);
            camera.updateMatrixWorld();
            if (pointerClient) setHovered(faceAt(pointerClient[0], pointerClient[1]));
            renderer.render(scene, camera);
        },
        dispose() {
            canvas.removeEventListener('pointerdown', onDown);
            canvas.removeEventListener('pointermove', onMove);
            canvas.removeEventListener('pointerleave', onLeave);
            canvas.removeEventListener('click', onClick);
            faces.forEach(f => {
                f.mesh.geometry.dispose();
                f.material.dispose();
                f.texNormal.dispose();
                f.texHover.dispose();
            });
            boxEdges.dispose();
            edgeMaterial.dispose();
            renderer.dispose();
            renderer.forceContextLoss();
            wrap.remove();
        },
    };
}

function makeCubeFaceTexture(text, background, foreground) {
    const size = 256;
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = size;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = background;
    ctx.fillRect(0, 0, size, size);
    ctx.fillStyle = foreground;
    ctx.font = 'bold 112px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, size / 2, size / 2 + 6);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    return texture;
}

// ---- Bottom-left labelled X/Y/Z axis indicator -----------------------------

const AXIS_SPEC = [
    { dir: [1, 0, 0], color: 0xe03131, label: 'X' },
    { dir: [0, 1, 0], color: 0x2f9e44, label: 'Y' },
    { dir: [0, 0, 1], color: 0x1971c2, label: 'Z' },
];

function buildAxisIndicator() {
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(50, 1, 0.1, 10);

    for (const a of AXIS_SPEC) {
        const dir = new THREE.Vector3(...a.dir);
        const geo = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, 0, 0), dir.clone().multiplyScalar(0.75)]);
        scene.add(new THREE.Line(geo, new THREE.LineBasicMaterial({ color: a.color })));

        // Inset from the frustum edge (and shrunk) so the label sprites are never
        // clipped by the small scissored viewport at any camera angle.
        const sprite = makeAxisLabelSprite(a.label, a.color);
        sprite.position.copy(dir.clone().multiplyScalar(1.05));
        sprite.scale.set(0.38, 0.38, 0.38);
        scene.add(sprite);
    }
    return { scene, camera };
}

function makeAxisLabelSprite(text, color) {
    const size = 64;
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = size;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#' + color.toString(16).padStart(6, '0');
    ctx.beginPath();
    ctx.arc(size / 2, size / 2, size / 2 - 2, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 34px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, size / 2, size / 2 + 2);
    const texture = new THREE.CanvasTexture(canvas);
    texture.minFilter = THREE.LinearFilter;
    const material = new THREE.SpriteMaterial({ map: texture, depthTest: false, depthWrite: false });
    return new THREE.Sprite(material);
}

/** Places the indicator camera so it shares the main camera's rotation around the origin. */
function positionIndicatorCamera(indicator, mainCamera, target) {
    const dir = mainCamera.position.clone().sub(target);
    if (dir.lengthSq() < 1e-9) dir.set(1, 1, 1);
    // Pulled back further than the triad's extent (radius ~1.4) so the 50deg-FOV
    // frustum has margin on all sides and the label sprites never clip the
    // viewport edge, from any camera orientation.
    dir.normalize().multiplyScalar(4.5);
    indicator.camera.up.copy(mainCamera.up);
    indicator.camera.position.copy(dir);
    indicator.camera.lookAt(0, 0, 0);
}

// 3D STEP viewer (FR-MA-2) for the Model Analysis proof of concept.
//
// Flow for each load:
//   1. send the file to the server (/api/models/inspect), which validates it and
//      reads its metadata (FR-MA-1, NFR-MA-1);
//   2. tessellate it in the browser with occt-import-js (Open CASCADE as WASM);
//   3. measure the mesh (stepMeasure.js) and render it with three.js.
// The result object goes back to the Blazor component, which shows the summary.

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

/** Creates the three.js scene inside `host`. `dotnet` receives status messages. */
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
        renderer.render(scene, camera);
        requestAnimationFrame(loop);
    };
    loop();

    viewers.set(host, {
        host, dotnet, renderer, scene, camera, controls, model: null,
        dispose() {
            running = false;
            observer.disconnect();
            controls.dispose();
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

/** Loads the file the user picked in `input`. */
export async function loadFromInput(host, input) {
    const file = input.files?.[0];
    if (!file) return { ok: false, error: 'Choose a .stp or .step file first.' };
    return load(host, file.name, async () => new Uint8Array(await file.arrayBuffer()));
}

/** Loads one of the server's sample files. */
export async function loadSample(host, name) {
    return load(host, name, async () => {
        const res = await fetch('/api/samples/' + encodeURIComponent(name));
        if (!res.ok) throw new Error(`Could not download sample '${name}' (${res.status}).`);
        return new Uint8Array(await res.arrayBuffer());
    });
}

async function load(host, name, getBytes) {
    const v = viewers.get(host);
    if (!v) return { ok: false, error: 'Viewer is not initialised.' };
    const result = await loadInto(v, name, getBytes);
    // Never leave a previous model on screen next to a rejection message.
    if (!result.ok) clearModel(v);
    return result;
}

async function loadInto(v, name, getBytes) {
    const status = msg => v.dotnet.invokeMethodAsync('ReportStatus', msg).catch(() => { });
    const t0 = performance.now();
    const timings = {};

    try {
        const bytes = await getBytes();

        // 1. Server validation and metadata.
        status('Validating file on the server…');
        let t = performance.now();
        const res = await fetch('/api/models/inspect?name=' + encodeURIComponent(name), {
            method: 'POST',
            headers: { 'Content-Type': 'application/octet-stream' },
            body: bytes,
        });
        const body = await res.json().catch(() => ({}));
        timings.serverMs = performance.now() - t;
        if (!res.ok) return { ok: false, error: body.message ?? `Server rejected the file (${res.status}).` };

        // 2. Tessellation in the browser.
        status('Loading geometry kernel…');
        const occt = await loadOcct();
        status('Tessellating model…');
        await nextFrame(); // let the status paint before the synchronous WASM call
        t = performance.now();
        const result = occt.ReadStepFile(bytes, { linearUnit: 'millimeter' });
        timings.tessellateMs = performance.now() - t;
        if (!result.success || !result.meshes?.length)
            return { ok: false, error: `'${name}' contains no readable geometry.` };

        // 3. Measure and render.
        status('Measuring and rendering…');
        t = performance.now();
        const measurements = measure(result.meshes);
        timings.measureMs = performance.now() - t;
        t = performance.now();
        showModel(v, result.meshes, measurements);
        await nextFrame();
        timings.renderMs = performance.now() - t;
        timings.totalMs = performance.now() - t0;

        return { ok: true, inspection: body, measurements, timings };
    } catch (e) {
        return { ok: false, error: e?.message ?? String(e) };
    }
}

const luminance = c => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;

const nextFrame = () => new Promise(r => requestAnimationFrame(() => setTimeout(r, 0)));

export function setView(host, opts) {
    const v = viewers.get(host);
    if (!v?.model) return;
    if (opts.bounds !== undefined) v.model.bounds.visible = opts.bounds;
    if (opts.edges !== undefined) v.model.edges.forEach(e => e.visible = opts.edges);
    if (opts.grid !== undefined) v.model.grid.visible = opts.grid;
    if (opts.reset) frame(v);
}

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
    const axes = new THREE.AxesHelper(cell * 2);
    axes.position.set(m.min[0], m.min[1], m.min[2]);
    root.add(axes);

    v.scene.add(root);
    v.model = { root, bounds, edges, grid, box, gridCellMm: cell };
    frame(v);
    m.gridCellMm = cell;
}

/** Points the camera at the model so it fills the view. */
function frame(v) {
    const { box } = v.model;
    const center = box.getCenter(new THREE.Vector3());
    const radius = box.getSize(new THREE.Vector3()).length() / 2 || 1;
    const dist = radius / Math.sin(THREE.MathUtils.degToRad(v.camera.fov / 2));
    const dir = new THREE.Vector3(1, 0.8, 1.2).normalize();
    v.camera.position.copy(center).addScaledVector(dir, dist);
    v.camera.near = dist / 1000;
    v.camera.far = dist * 100;
    v.camera.updateProjectionMatrix();
    v.controls.target.copy(center);
    v.controls.update();
}

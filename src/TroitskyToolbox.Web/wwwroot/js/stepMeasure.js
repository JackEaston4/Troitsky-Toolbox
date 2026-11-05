// Measurements of a tessellated STEP model (FR-MA-3).
//
// Input is the `meshes` array returned by occt-import-js ReadStepFile: each mesh
// has attributes.position.array (xyz triplets, in mm) and index.array (triangles).
// The importer bakes each part's placement into its positions, so all meshes
// share one coordinate system.
//
// Volume and centroid use the divergence theorem over the closed triangulated
// surface (sum of signed tetrahedra against the origin). They are exact for the
// mesh; for curved CAD faces the error is set by the tessellation tolerance.

/**
 * @param {Array<{name?: string, attributes: {position: {array: number[]}}, index: {array: number[]}}>} meshes
 */
export function measure(meshes) {
    const min = [Infinity, Infinity, Infinity];
    const max = [-Infinity, -Infinity, -Infinity];
    let signedVolume6 = 0; // 6 × signed volume
    let area2 = 0;         // 2 × area
    const moment24 = [0, 0, 0]; // 24 × first moment of volume
    let triangles = 0;
    let vertices = 0;

    for (const mesh of meshes) {
        const p = mesh.attributes.position.array;
        const idx = mesh.index.array;
        vertices += p.length / 3;

        for (let i = 0; i < p.length; i += 3) {
            for (let k = 0; k < 3; k++) {
                const v = p[i + k];
                if (v < min[k]) min[k] = v;
                if (v > max[k]) max[k] = v;
            }
        }

        for (let t = 0; t < idx.length; t += 3) {
            const a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
            const ax = p[a], ay = p[a + 1], az = p[a + 2];
            const bx = p[b], by = p[b + 1], bz = p[b + 2];
            const cx = p[c], cy = p[c + 1], cz = p[c + 2];

            // a · (b × c)
            const det = ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
            signedVolume6 += det;
            moment24[0] += det * (ax + bx + cx);
            moment24[1] += det * (ay + by + cy);
            moment24[2] += det * (az + bz + cz);

            // |(b - a) × (c - a)|
            const ux = bx - ax, uy = by - ay, uz = bz - az;
            const vx = cx - ax, vy = cy - ay, vz = cz - az;
            const nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            area2 += Math.sqrt(nx * nx + ny * ny + nz * nz);
            triangles++;
        }
    }

    if (triangles === 0) return null;

    const volume = signedVolume6 / 6;
    const size = [max[0] - min[0], max[1] - min[1], max[2] - min[2]];
    const centroid = Math.abs(volume) > 1e-12
        ? moment24.map(m => m / (4 * signedVolume6))
        : [(min[0] + max[0]) / 2, (min[1] + max[1]) / 2, (min[2] + max[2]) / 2];

    return {
        min, max, size,
        volumeMm3: Math.abs(volume),
        // Negative signed volume means the triangles face inwards; the magnitude is still right.
        inwardNormals: volume < 0,
        surfaceAreaMm2: area2 / 2,
        centroid,
        triangles,
        vertices,
        meshes: meshes.length,
        closed: isClosed(meshes, size),
    };
}

/**
 * True if, after welding coincident vertices, every triangle edge is shared by an
 * even number of triangles (2, or 4 where two parts of a solid touch along a
 * line). A closed surface is what makes the volume and centroid valid.
 */
function isClosed(meshes, size) {
    const diag = Math.hypot(size[0], size[1], size[2]) || 1;
    const q = 1e6 / diag; // weld tolerance: one millionth of the model diagonal
    const ids = new Map();
    const edges = new Map();
    const key = (p, i) => `${Math.round(p[i] * q)},${Math.round(p[i + 1] * q)},${Math.round(p[i + 2] * q)}`;

    for (const mesh of meshes) {
        const p = mesh.attributes.position.array;
        const local = new Int32Array(p.length / 3);
        for (let i = 0; i < p.length; i += 3) {
            const k = key(p, i);
            let id = ids.get(k);
            if (id === undefined) { id = ids.size; ids.set(k, id); }
            local[i / 3] = id;
        }
        const idx = mesh.index.array;
        for (let t = 0; t < idx.length; t += 3) {
            const tri = [local[idx[t]], local[idx[t + 1]], local[idx[t + 2]]];
            for (let e = 0; e < 3; e++) {
                const u = tri[e], v = tri[(e + 1) % 3];
                if (u === v) continue; // degenerate sliver
                const ek = u < v ? u * 0x100000000 + v : v * 0x100000000 + u;
                edges.set(ek, (edges.get(ek) ?? 0) + 1);
            }
        }
    }
    for (const n of edges.values()) if (n % 2 !== 0) return false;
    return edges.size > 0;
}

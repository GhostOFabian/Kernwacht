// Prozedurale Geometrie mit Normalen. Runde Formen erhalten weiche (interpolierte) Normalen,
// Kanten werden über abgeschrägte Würfel (rbox) weich gezeichnet – kein Pixel-/Würfel-Look.

export interface MeshData {
  positions: Float32Array;
  normals: Float32Array;
}

class Builder {
  p: number[] = [];
  n: number[] = [];
  /** Dreieck mit flacher Normale. */
  tri(ax: number, ay: number, az: number, bx: number, by: number, bz: number, cx: number, cy: number, cz: number): void {
    const ux = bx - ax, uy = by - ay, uz = bz - az;
    const vx = cx - ax, vy = cy - ay, vz = cz - az;
    let nx = uy * vz - uz * vy;
    let ny = uz * vx - ux * vz;
    let nz = ux * vy - uy * vx;
    const l = Math.hypot(nx, ny, nz) || 1;
    nx /= l; ny /= l; nz /= l;
    this.p.push(ax, ay, az, bx, by, bz, cx, cy, cz);
    this.n.push(nx, ny, nz, nx, ny, nz, nx, ny, nz);
  }
  /** Dreieck mit expliziten (weichen) Normalen je Ecke. */
  triN(a: number[], b: number[], c: number[], na: number[], nb: number[], nc: number[]): void {
    this.p.push(a[0], a[1], a[2], b[0], b[1], b[2], c[0], c[1], c[2]);
    this.n.push(na[0], na[1], na[2], nb[0], nb[1], nb[2], nc[0], nc[1], nc[2]);
  }
  quad(a: number[], b: number[], c: number[], d: number[]): void {
    this.tri(a[0], a[1], a[2], b[0], b[1], b[2], c[0], c[1], c[2]);
    this.tri(a[0], a[1], a[2], c[0], c[1], c[2], d[0], d[1], d[2]);
  }
  build(): MeshData {
    return { positions: new Float32Array(this.p), normals: new Float32Array(this.n) };
  }
}

function norm(v: number[]): number[] {
  const l = Math.hypot(v[0], v[1], v[2]) || 1;
  return [v[0] / l, v[1] / l, v[2] / l];
}

/** Würfel 1×1×1, zentriert (scharfe Kanten). */
export function box(): MeshData {
  const b = new Builder();
  const h = 0.5;
  const v = (x: number, y: number, z: number) => [x * h, y * h, z * h];
  b.quad(v(-1, -1, 1), v(1, -1, 1), v(1, 1, 1), v(-1, 1, 1));
  b.quad(v(1, -1, -1), v(-1, -1, -1), v(-1, 1, -1), v(1, 1, -1));
  b.quad(v(1, -1, 1), v(1, -1, -1), v(1, 1, -1), v(1, 1, 1));
  b.quad(v(-1, -1, -1), v(-1, -1, 1), v(-1, 1, 1), v(-1, 1, -1));
  b.quad(v(-1, 1, 1), v(1, 1, 1), v(1, 1, -1), v(-1, 1, -1));
  b.quad(v(-1, -1, -1), v(1, -1, -1), v(1, -1, 1), v(-1, -1, 1));
  return b.build();
}

/**
 * Abgeschrägter Würfel 1×1×1 (zentriert). Kanten und Ecken sind gefast; die Normalen der Fasen
 * werden gemittelt, sodass Kanten im Licht weich glänzen.
 */
export function roundedBox(bevel = 0.12): MeshData {
  const b = new Builder();
  const h = 0.5;
  const i = h - bevel;
  // Stützpunkte: Für jede Achse drei Ebenen (-h, -i, i, h)
  const faces: [number[], number[]][] = [];
  const push = (pts: number[][], n: number[]) => faces.push([pts.flat(), n]);
  // Hauptflächen
  push([[-i, -i, h], [i, -i, h], [i, i, h], [-i, i, h]], [0, 0, 1]);
  push([[i, -i, -h], [-i, -i, -h], [-i, i, -h], [i, i, -h]], [0, 0, -1]);
  push([[h, -i, i], [h, -i, -i], [h, i, -i], [h, i, i]], [1, 0, 0]);
  push([[-h, -i, -i], [-h, -i, i], [-h, i, i], [-h, i, -i]], [-1, 0, 0]);
  push([[-i, h, i], [i, h, i], [i, h, -i], [-i, h, -i]], [0, 1, 0]);
  push([[-i, -h, -i], [i, -h, -i], [i, -h, i], [-i, -h, i]], [0, -1, 0]);
  for (const [pts, n] of faces) {
    const q = [pts.slice(0, 3), pts.slice(3, 6), pts.slice(6, 9), pts.slice(9, 12)];
    b.triN(q[0], q[1], q[2], n, n, n);
    b.triN(q[0], q[2], q[3], n, n, n);
  }
  // Kantenfasen (12 Kanten) – Normalen zwischen den beiden Flächen
  const edge = (a: number[], bb: number[], c: number[], d: number[], na: number[], nb: number[]): void => {
    const nm = norm([na[0] + nb[0], na[1] + nb[1], na[2] + nb[2]]);
    const n1 = norm([na[0] * 0.6 + nm[0], na[1] * 0.6 + nm[1], na[2] * 0.6 + nm[2]]);
    const n2 = norm([nb[0] * 0.6 + nm[0], nb[1] * 0.6 + nm[1], nb[2] * 0.6 + nm[2]]);
    b.triN(a, bb, c, n1, n1, n2);
    b.triN(a, c, d, n1, n2, n2);
  };
  const s = [-1, 1];
  for (const sy of s) for (const sz of s) {
    // Kanten entlang X
    edge([-i, sy * i, sz * h], [i, sy * i, sz * h], [i, sy * h, sz * i], [-i, sy * h, sz * i], [0, 0, sz], [0, sy, 0]);
  }
  for (const sx of s) for (const sz of s) {
    edge([sx * i, -i, sz * h], [sx * h, -i, sz * i], [sx * h, i, sz * i], [sx * i, i, sz * h], [0, 0, sz], [sx, 0, 0]);
  }
  for (const sx of s) for (const sy of s) {
    edge([sx * h, sy * i, -i], [sx * h, sy * i, i], [sx * i, sy * h, i], [sx * i, sy * h, -i], [sx, 0, 0], [0, sy, 0]);
  }
  // Eckdreiecke
  for (const sx of s) for (const sy of s) for (const sz of s) {
    const n = norm([sx, sy, sz]);
    b.triN([sx * h, sy * i, sz * i], [sx * i, sy * h, sz * i], [sx * i, sy * i, sz * h], n, n, n);
  }
  // Doppelseitig zeichnen wir ohnehin (kein Culling); Dreiecksorientierung ist daher unkritisch.
  return b.build();
}

/** Zylinder/Kegelstumpf, Höhe 1 (y 0..1), Radius unten rb, oben rt. smooth = weiche Mantelnormalen. */
export function cylinder(segments: number, rb = 0.5, rt = 0.5, caps = true, smooth = false): MeshData {
  const b = new Builder();
  const slope = (rb - rt);
  for (let i = 0; i < segments; i++) {
    const a0 = (i / segments) * Math.PI * 2;
    const a1 = ((i + 1) / segments) * Math.PI * 2;
    const c0 = Math.cos(a0), s0 = Math.sin(a0), c1 = Math.cos(a1), s1 = Math.sin(a1);
    const p0 = [c0 * rb, 0, s0 * rb];
    const p1 = [c1 * rb, 0, s1 * rb];
    const q0 = [c0 * rt, 1, s0 * rt];
    const q1 = [c1 * rt, 1, s1 * rt];
    if (smooth) {
      const n0 = norm([c0, slope, s0]);
      const n1 = norm([c1, slope, s1]);
      if (rt > 0) {
        b.triN(p1, p0, q0, n1, n0, n0);
        b.triN(p1, q0, q1, n1, n0, n1);
      } else {
        const na = norm([Math.cos((a0 + a1) / 2), slope, Math.sin((a0 + a1) / 2)]);
        b.triN(p1, p0, [0, 1, 0], n1, n0, na);
      }
    } else if (rt > 0) b.quad(p1, p0, q0, q1);
    else b.tri(p1[0], p1[1], p1[2], p0[0], p0[1], p0[2], 0, 1, 0);
    if (caps) {
      if (rt > 0) b.tri(0, 1, 0, q1[0], 1, q1[2], q0[0], 1, q0[2]);
      b.tri(0, 0, 0, p0[0], 0, p0[2], p1[0], 0, p1[2]);
    }
  }
  return b.build();
}

/** Oktaeder, Radius 0.5, zentriert (facettiert – für Kristalle gewollt). */
export function octahedron(): MeshData {
  const b = new Builder();
  const r = 0.5;
  const t = [0, r, 0];
  const d = [0, -r, 0];
  const ring = [[r, 0, 0], [0, 0, r], [-r, 0, 0], [0, 0, -r]];
  for (let i = 0; i < 4; i++) {
    const a = ring[i];
    const c = ring[(i + 1) % 4];
    b.tri(t[0], t[1], t[2], c[0], c[1], c[2], a[0], a[1], a[2]);
    b.tri(d[0], d[1], d[2], a[0], a[1], a[2], c[0], c[1], c[2]);
  }
  return b.build();
}

/** Ikosaeder-Kugel, Radius 0.5. smooth = weiche Normalen (runde Kugel). */
export function sphere(subdiv = 1, smooth = true): MeshData {
  const t = (1 + Math.sqrt(5)) / 2;
  let verts: number[][] = [
    [-1, t, 0], [1, t, 0], [-1, -t, 0], [1, -t, 0], [0, -1, t], [0, 1, t], [0, -1, -t], [0, 1, -t],
    [t, 0, -1], [t, 0, 1], [-t, 0, -1], [-t, 0, 1],
  ].map((v) => norm(v));
  let faces = [
    [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
    [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
  ];
  for (let s = 0; s < subdiv; s++) {
    const nf: number[][] = [];
    const cache = new Map<string, number>();
    const mid = (a: number, c: number): number => {
      const key = a < c ? `${a}_${c}` : `${c}_${a}`;
      const hit = cache.get(key);
      if (hit !== undefined) return hit;
      const va = verts[a];
      const vc = verts[c];
      verts.push(norm([(va[0] + vc[0]) / 2, (va[1] + vc[1]) / 2, (va[2] + vc[2]) / 2]));
      cache.set(key, verts.length - 1);
      return verts.length - 1;
    };
    for (const [a, bb, c] of faces) {
      const ab = mid(a, bb), bc = mid(bb, c), ca = mid(c, a);
      nf.push([a, ab, ca], [bb, bc, ab], [c, ca, bc], [ab, bc, ca]);
    }
    faces = nf;
  }
  const b = new Builder();
  const sc = (v: number[]) => [v[0] * 0.5, v[1] * 0.5, v[2] * 0.5];
  for (const [a, bb, c] of faces) {
    const va = verts[a], vb = verts[bb], vc = verts[c];
    if (smooth) b.triN(sc(va), sc(vb), sc(vc), va, vb, vc);
    else {
      const A = sc(va), B = sc(vb), C = sc(vc);
      b.tri(A[0], A[1], A[2], B[0], B[1], B[2], C[0], C[1], C[2]);
    }
  }
  verts = [];
  return b.build();
}

/** Flacher Ring (Kreisring) in der XZ-Ebene, Außenradius 1, Innenradius inner. Normale nach oben. */
export function ring(segments: number, inner: number): MeshData {
  const b = new Builder();
  const up = [0, 1, 0];
  for (let i = 0; i < segments; i++) {
    const a0 = (i / segments) * Math.PI * 2;
    const a1 = ((i + 1) / segments) * Math.PI * 2;
    const o0 = [Math.cos(a0), 0, Math.sin(a0)];
    const o1 = [Math.cos(a1), 0, Math.sin(a1)];
    const i0 = [o0[0] * inner, 0, o0[2] * inner];
    const i1 = [o1[0] * inner, 0, o1[2] * inner];
    b.triN(i0, i1, o1, up, up, up);
    b.triN(i0, o1, o0, up, up, up);
  }
  return b.build();
}

/** Gefüllte Scheibe in der XZ-Ebene, Radius 1. */
export function disc(segments: number): MeshData {
  const b = new Builder();
  const up = [0, 1, 0];
  for (let i = 0; i < segments; i++) {
    const a0 = (i / segments) * Math.PI * 2;
    const a1 = ((i + 1) / segments) * Math.PI * 2;
    b.triN([0, 0, 0], [Math.cos(a1), 0, Math.sin(a1)], [Math.cos(a0), 0, Math.sin(a0)], up, up, up);
  }
  return b.build();
}

/** Ebene in der XZ-Ebene (1×1, zentriert), fein unterteilt für Wellenanimation. */
export function plane(div: number): MeshData {
  const b = new Builder();
  const up = [0, 1, 0];
  for (let z = 0; z < div; z++) {
    for (let x = 0; x < div; x++) {
      const x0 = x / div - 0.5, x1 = (x + 1) / div - 0.5, z0 = z / div - 0.5, z1 = (z + 1) / div - 0.5;
      b.triN([x0, 0, z0], [x1, 0, z0], [x1, 0, z1], up, up, up);
      b.triN([x0, 0, z0], [x1, 0, z1], [x0, 0, z1], up, up, up);
    }
  }
  return b.build();
}

/** Pyramide/Kegel mit facettierten Seiten, Höhe 1, Basis auf y=0. */
export function pyramid(sides = 4): MeshData {
  return cylinder(sides, 0.5, 0, true);
}

/** Torus, Radius 0.5, Rohrdicke tube, weiche Normalen. */
export function torus(seg: number, sides: number, tube: number): MeshData {
  const b = new Builder();
  const R = 0.5;
  const pt = (u: number, v: number): number[] => {
    const cu = Math.cos(u), su = Math.sin(u), cv = Math.cos(v), sv = Math.sin(v);
    return [(R + tube * cv) * cu, tube * sv, (R + tube * cv) * su];
  };
  const nr = (u: number, v: number): number[] => [Math.cos(v) * Math.cos(u), Math.sin(v), Math.cos(v) * Math.sin(u)];
  for (let i = 0; i < seg; i++) {
    for (let j = 0; j < sides; j++) {
      const u0 = (i / seg) * Math.PI * 2, u1 = ((i + 1) / seg) * Math.PI * 2;
      const v0 = (j / sides) * Math.PI * 2, v1 = ((j + 1) / sides) * Math.PI * 2;
      b.triN(pt(u0, v0), pt(u0, v1), pt(u1, v1), nr(u0, v0), nr(u0, v1), nr(u1, v1));
      b.triN(pt(u0, v0), pt(u1, v1), pt(u1, v0), nr(u0, v0), nr(u1, v1), nr(u1, v0));
    }
  }
  return b.build();
}

/** Flügel/Blatt: flaches, spitz zulaufendes Element in der XZ-Ebene (Länge entlang +Z). */
export function leaf(): MeshData {
  const b = new Builder();
  const up = [0, 1, 0];
  const pts = [[0, 0, 0], [0.35, 0, 0.3], [0.25, 0, 0.75], [0, 0, 1], [-0.25, 0, 0.75], [-0.35, 0, 0.3]];
  for (let i = 1; i < pts.length - 1; i++) b.triN(pts[0], pts[i], pts[i + 1], up, up, up);
  return b.build();
}

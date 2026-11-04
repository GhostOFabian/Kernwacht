// Kleiner GLB-Viewer (WebGL2): PBR-artige Beleuchtung, Normal Maps, Emission, Skinning, Animationen.
// Nutzung: const v = new GlbViewer(canvas); await v.load(url); v.play('Walk');
(function () {
  'use strict';

  const VS = `#version 300 es
  layout(location=0) in vec3 aPos; layout(location=1) in vec3 aNrm; layout(location=2) in vec4 aTan;
  layout(location=3) in vec2 aUV; layout(location=4) in vec4 aJ; layout(location=5) in vec4 aW; layout(location=6) in vec3 aC;
  uniform mat4 uVP, uModel; uniform mat4 uJ[96]; uniform bool uSkin;
  out vec3 vW; out vec3 vN; out vec4 vT; out vec2 vUV; out vec3 vC;
  void main(){ vC = aC;
    mat4 M = uModel;
    if (uSkin) M = aW.x*uJ[int(aJ.x)] + aW.y*uJ[int(aJ.y)] + aW.z*uJ[int(aJ.z)] + aW.w*uJ[int(aJ.w)];
    vec4 w = M * vec4(aPos,1.0);
    mat3 nm = mat3(M);
    vW = w.xyz; vN = normalize(nm*aNrm); vT = vec4(normalize(nm*aTan.xyz), aTan.w); vUV = aUV;
    gl_Position = uVP * w;
  }`;
  const FS = `#version 300 es
  precision highp float;
  in vec3 vW; in vec3 vN; in vec4 vT; in vec2 vUV; in vec3 vC;
  uniform sampler2D tBase, tNrm, tOrm, tEm; uniform bool uHasEm; uniform vec3 uEmF; uniform vec4 uBaseF;
  uniform vec3 uCam; uniform float uExposure;
  out vec4 o;
  vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
  float D(float nh, float a){ float a2=a*a; float d=nh*nh*(a2-1.0)+1.0; return a2/(3.14159*d*d); }
  vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14),0.0,1.0); }
  void main(){
    vec3 base = toLin(texture(tBase, vUV).rgb) * uBaseF.rgb * vC;
    vec3 orm = texture(tOrm, vUV).rgb;
    float rough = clamp(orm.g, 0.05, 1.0), metal = orm.b, ao = mix(1.0, orm.r, 0.6);
    vec3 n = normalize(vN); vec3 t = normalize(vT.xyz - n*dot(n,vT.xyz)); vec3 b = cross(n,t)*vT.w;
    vec3 tn = texture(tNrm, vUV).xyz*2.0-1.0;
    n = normalize(mat3(t,b,n)*tn);
    vec3 v = normalize(uCam - vW);
    if (dot(n,v) < 0.0) n = -n;
    vec3 f0 = mix(vec3(0.04), base, metal);
    vec3 diff = base*(1.0-metal);
    vec3 col = vec3(0.0);
    vec3 L[3]; vec3 C[3];
    L[0]=normalize(vec3(0.5,0.9,0.6)); C[0]=vec3(2.6,2.35,2.1);
    L[1]=normalize(vec3(-0.7,0.4,-0.4)); C[1]=vec3(0.45,0.55,0.9);
    L[2]=normalize(vec3(-0.2,0.3,-1.0)); C[2]=vec3(1.1,0.75,0.55);
    for(int i=0;i<3;i++){
      vec3 l=L[i]; vec3 h=normalize(l+v);
      float nl=max(dot(n,l),0.0), nh=max(dot(n,h),0.0), vh=max(dot(v,h),0.0);
      vec3 F = f0 + (1.0-f0)*pow(1.0-vh,5.0);
      float spec = D(nh, rough*rough) * 0.25 / max(0.15, nl*max(dot(n,v),0.0)+0.1);
      col += (diff*(1.0-F)/3.14159*3.14159*0.9 + F*spec) * C[i] * nl;
    }
    vec3 sky = mix(vec3(0.05,0.045,0.06), vec3(0.18,0.19,0.24), n.y*0.5+0.5);
    col += (diff + f0*0.6) * sky * ao * 1.4;
    float rim = pow(1.0-max(dot(n,v),0.0), 4.0);
    col += rim * vec3(0.25,0.22,0.35) * 0.6;
    if (uHasEm) col += toLin(texture(tEm, vUV).rgb) * uEmF * 2.2;
    col = aces(col*uExposure);
    o = vec4(pow(col, vec3(1.0/2.2)), uBaseF.a);
  }`;
  const GVS = `#version 300 es
  layout(location=0) in vec3 aPos; uniform mat4 uVP; uniform float uR; out vec2 vP;
  void main(){ vP=aPos.xz; gl_Position=uVP*vec4(aPos.x*uR,0.0,aPos.z*uR,1.0); }`;
  const GFS = `#version 300 es
  precision highp float; in vec2 vP; out vec4 o;
  void main(){ float d=length(vP); float a=smoothstep(1.0,0.0,d); o=vec4(0.0,0.0,0.0,a*0.55); }`;

  function mat4() { const m = new Float32Array(16); m[0] = m[5] = m[10] = m[15] = 1; return m; }
  function mul(a, b) {
    const o = new Float32Array(16);
    for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) {
      let s = 0; for (let k = 0; k < 4; k++) s += a[k * 4 + r] * b[c * 4 + k]; o[c * 4 + r] = s;
    }
    return o;
  }
  function trs(t, q, s) {
    const [x, y, z, w] = q, [sx, sy, sz] = s;
    const m = new Float32Array(16);
    m[0] = (1 - 2 * (y * y + z * z)) * sx; m[1] = 2 * (x * y + z * w) * sx; m[2] = 2 * (x * z - y * w) * sx;
    m[4] = 2 * (x * y - z * w) * sy; m[5] = (1 - 2 * (x * x + z * z)) * sy; m[6] = 2 * (y * z + x * w) * sy;
    m[8] = 2 * (x * z + y * w) * sz; m[9] = 2 * (y * z - x * w) * sz; m[10] = (1 - 2 * (x * x + y * y)) * sz;
    m[12] = t[0]; m[13] = t[1]; m[14] = t[2]; m[15] = 1;
    return m;
  }
  function persp(fov, asp, n, f) {
    const t = 1 / Math.tan(fov / 2), m = new Float32Array(16);
    m[0] = t / asp; m[5] = t; m[10] = (f + n) / (n - f); m[11] = -1; m[14] = 2 * f * n / (n - f); return m;
  }
  function lookAt(e, c, u) {
    let zx = e[0] - c[0], zy = e[1] - c[1], zz = e[2] - c[2]; let l = Math.hypot(zx, zy, zz); zx /= l; zy /= l; zz /= l;
    let xx = u[1] * zz - u[2] * zy, xy = u[2] * zx - u[0] * zz, xz = u[0] * zy - u[1] * zx; l = Math.hypot(xx, xy, xz); xx /= l; xy /= l; xz /= l;
    const yx = zy * xz - zz * xy, yy = zz * xx - zx * xz, yz = zx * xy - zy * xx;
    const m = new Float32Array(16);
    m[0] = xx; m[1] = yx; m[2] = zx; m[4] = xy; m[5] = yy; m[6] = zy; m[8] = xz; m[9] = yz; m[10] = zz;
    m[12] = -(xx * e[0] + xy * e[1] + xz * e[2]); m[13] = -(yx * e[0] + yy * e[1] + yz * e[2]); m[14] = -(zx * e[0] + zy * e[1] + zz * e[2]); m[15] = 1;
    return m;
  }
  function slerp(a, b, t) {
    let d = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
    const bb = d < 0 ? b.map((x) => -x) : b; d = Math.abs(d);
    if (d > 0.9995) { const r = a.map((x, i) => x + (bb[i] - x) * t); const l = Math.hypot(...r); return r.map((x) => x / l); }
    const th = Math.acos(d), s = Math.sin(th), w1 = Math.sin((1 - t) * th) / s, w2 = Math.sin(t * th) / s;
    return a.map((x, i) => x * w1 + bb[i] * w2);
  }

  class GlbViewer {
    constructor(canvas) {
      this.c = canvas;
      const gl = this.gl = canvas.getContext('webgl2', { antialias: true, preserveDrawingBuffer: true, alpha: true, premultipliedAlpha: false });
      if (!gl) throw new Error('WebGL2 fehlt');
      this.prog = this.mk(VS, FS); this.gprog = this.mk(GVS, GFS);
      this.yaw = 0.6; this.pitch = 0.32; this.dist = 3; this.clip = null; this.t = 0; this.speed = 1; this.spin = 0.35;
      this.exposure = 1.0; this.loopClip = true;
      const g = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, g);
      const q = []; const N = 32; for (let i = 0; i < N; i++) { const a0 = i / N * 6.2832, a1 = (i + 1) / N * 6.2832; q.push(0, 0, 0, Math.cos(a0), 0, Math.sin(a0), Math.cos(a1), 0, Math.sin(a1)); }
      gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(q), gl.STATIC_DRAW);
      this.gvao = gl.createVertexArray(); gl.bindVertexArray(this.gvao); gl.enableVertexAttribArray(0); gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0); gl.bindVertexArray(null);
      this.gcount = N * 3;
      this.white = this.tex1([255, 255, 255, 255]); this.flatN = this.tex1([128, 128, 255, 255]); this.ormDef = this.tex1([255, 200, 0, 255]);
      let drag = null;
      canvas.addEventListener('pointerdown', (e) => { drag = [e.clientX, e.clientY]; this.spin = 0; canvas.setPointerCapture(e.pointerId); });
      canvas.addEventListener('pointermove', (e) => { if (!drag) return; this.yaw += (e.clientX - drag[0]) * 0.01; this.pitch = Math.max(-0.2, Math.min(1.3, this.pitch + (e.clientY - drag[1]) * 0.01)); drag = [e.clientX, e.clientY]; });
      canvas.addEventListener('pointerup', () => { drag = null; });
      canvas.addEventListener('wheel', (e) => { e.preventDefault(); this.dist *= e.deltaY > 0 ? 1.1 : 0.9; }, { passive: false });
    }
    mk(vs, fs) {
      const gl = this.gl;
      const sh = (t, s) => { const x = gl.createShader(t); gl.shaderSource(x, s); gl.compileShader(x); if (!gl.getShaderParameter(x, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(x)); return x; };
      const p = gl.createProgram(); gl.attachShader(p, sh(gl.VERTEX_SHADER, vs)); gl.attachShader(p, sh(gl.FRAGMENT_SHADER, fs)); gl.linkProgram(p);
      if (!gl.getProgramParameter(p, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(p));
      return p;
    }
    tex1(c) { const gl = this.gl; const t = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, t); gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(c)); return t; }

    async load(url) {
      const buf = await (await fetch(url)).arrayBuffer();
      return this.parse(buf);
    }
    async parse(buf) {
      const gl = this.gl;
      const dv = new DataView(buf);
      if (dv.getUint32(0, true) !== 0x46546C67) throw new Error('Keine GLB-Datei');
      const jl = dv.getUint32(12, true);
      const J = JSON.parse(new TextDecoder().decode(new Uint8Array(buf, 20, jl)));
      const bo = 20 + jl + 8;
      const bin = buf.slice(bo, bo + dv.getUint32(20 + jl, true));
      this.J = J;
      const view = (i) => { const v = J.bufferViews[i]; return new Uint8Array(bin, v.byteOffset || 0, v.byteLength); };
      const TA = { 5126: Float32Array, 5121: Uint8Array, 5123: Uint16Array, 5125: Uint32Array };
      const NC = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16 };
      const acc = (i) => { const a = J.accessors[i]; const v = J.bufferViews[a.bufferView]; const T = TA[a.componentType];
        return new T(bin, (v.byteOffset || 0) + (a.byteOffset || 0), a.count * NC[a.type]); };
      this.acc = acc;
      // Texturen
      const texs = await Promise.all((J.textures || []).map(async (t) => {
        const im = J.images[t.source];
        const bmp = await createImageBitmap(new Blob([view(im.bufferView)], { type: im.mimeType }));
        const tx = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, tx);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, bmp);
        gl.generateMipmap(gl.TEXTURE_2D);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
        const ext = gl.getExtension('EXT_texture_filter_anisotropic'); if (ext) gl.texParameterf(gl.TEXTURE_2D, ext.TEXTURE_MAX_ANISOTROPY_EXT, 8);
        return tx;
      }));
      this.mats = (J.materials || []).map((m) => {
        const p = m.pbrMetallicRoughness || {};
        const es = m.extensions && m.extensions.KHR_materials_emissive_strength ? m.extensions.KHR_materials_emissive_strength.emissiveStrength : 1;
        return { base: p.baseColorTexture ? texs[p.baseColorTexture.index] : this.white, orm: p.metallicRoughnessTexture ? texs[p.metallicRoughnessTexture.index] : this.ormDef,
          nrm: m.normalTexture ? texs[m.normalTexture.index] : this.flatN, em: m.emissiveTexture ? texs[m.emissiveTexture.index] : null,
          emF: (m.emissiveFactor || [0, 0, 0]).map((x) => x * es), baseF: p.baseColorFactor || [1, 1, 1, 1], name: m.name };
      });
      // Meshes
      let tris = 0;
      const lo = [1e9, 1e9, 1e9], hi = [-1e9, -1e9, -1e9];
      this.meshes = J.meshes.map((me) => me.primitives.map((pr) => {
        const vao = gl.createVertexArray(); gl.bindVertexArray(vao);
        const bindA = (loc, name, size, norm) => {
          const i = pr.attributes[name]; if (i === undefined) { gl.disableVertexAttribArray(loc); return; }
          const a = J.accessors[i]; const data = acc(i);
          const b = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, b); gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
          gl.enableVertexAttribArray(loc);
          gl.vertexAttribPointer(loc, size, a.componentType, !!norm, 0, 0);
          if (name === 'POSITION') { pr._min = a.min; pr._max = a.max; }
        };
        bindA(0, 'POSITION', 3); bindA(1, 'NORMAL', 3); bindA(2, 'TANGENT', 4); bindA(3, 'TEXCOORD_0', 2); bindA(4, 'JOINTS_0', 4); bindA(5, 'WEIGHTS_0', 4); bindA(6, 'COLOR_0', 3);
        if (pr.attributes.COLOR_0 === undefined) gl.vertexAttrib3f(6, 1, 1, 1);
        const ia = J.accessors[pr.indices]; const ib = gl.createBuffer(); gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ib); gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, acc(pr.indices), gl.STATIC_DRAW);
        gl.bindVertexArray(null);
        tris += ia.count / 3;
        return { vao, count: ia.count, type: ia.componentType, mat: this.mats[pr.material], skinned: pr.attributes.JOINTS_0 !== undefined, min: pr._min, max: pr._max };
      }));
      this.tris = tris;
      this.nodes = J.nodes.map((n) => ({ t: n.translation || [0, 0, 0], r: n.rotation || [0, 0, 0, 1], s: n.scale || [1, 1, 1], base: null, children: n.children || [], mesh: n.mesh, skin: n.skin, name: n.name }));
      this.nodes.forEach((n) => { n.base = { t: [...n.t], r: [...n.r], s: [...n.s] }; });
      this.parent = new Array(this.nodes.length).fill(-1);
      this.nodes.forEach((n, i) => n.children.forEach((c) => { this.parent[c] = i; }));
      this.skins = (J.skins || []).map((s) => ({ joints: s.joints, ibm: acc(s.inverseBindMatrices) }));
      this.clips = {};
      (J.animations || []).forEach((a) => {
        let dur = 0;
        const ch = a.channels.map((c) => { const s = a.samplers[c.sampler]; const tin = acc(s.input); dur = Math.max(dur, tin[tin.length - 1]);
          return { node: c.target.node, path: c.target.path, tin, out: acc(s.output), n: c.target.path === 'rotation' ? 4 : 3 }; });
        this.clips[a.name] = { ch, dur };
      });
      const W0 = this.world();
      this.nodes.forEach((n, i) => {
        if (n.mesh === undefined) return;
        const M = n.skin !== undefined ? mat4() : W0[i];
        for (const pr of this.meshes[n.mesh]) for (let c = 0; c < 8; c++) {
          const p = [c & 1 ? pr.max[0] : pr.min[0], c & 2 ? pr.max[1] : pr.min[1], c & 4 ? pr.max[2] : pr.min[2]];
          for (let k = 0; k < 3; k++) { const v = M[k] * p[0] + M[4 + k] * p[1] + M[8 + k] * p[2] + M[12 + k]; lo[k] = Math.min(lo[k], v); hi[k] = Math.max(hi[k], v); }
        }
      });
      this.center = [(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2];
      this.size = Math.max(hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]);
      this.radius = Math.max(hi[0] - lo[0], hi[2] - lo[2]) * 0.6;
      this.dist = this.size * 1.9 + 0.3;
      this.extras = (J.nodes[0] && J.nodes[0].extras) || {};
      this.clip = this.clips.Idle ? 'Idle' : null; this.t = 0;
      return { tris, clips: Object.keys(this.clips), extras: this.extras, materials: this.mats.map((m) => m.name) };
    }
    play(name, loop = true) { this.clip = this.clips[name] ? name : null; this.t = 0; this.loopClip = loop; }
    sample(t) {
      this.nodes.forEach((n) => { n.t = [...n.base.t]; n.r = [...n.base.r]; n.s = [...n.base.s]; });
      const c = this.clip && this.clips[this.clip];
      if (!c) return;
      const tt = this.loopClip ? (c.dur > 0 ? t % c.dur : 0) : Math.min(t, c.dur);
      for (const ch of c.ch) {
        const ti = ch.tin; let k = 0; while (k < ti.length - 2 && ti[k + 1] < tt) k++;
        const t0 = ti[k], t1 = ti[Math.min(k + 1, ti.length - 1)];
        const f = t1 > t0 ? Math.max(0, Math.min(1, (tt - t0) / (t1 - t0))) : 0;
        const a = Array.from(ch.out.slice(k * ch.n, k * ch.n + ch.n)), b = Array.from(ch.out.slice((k + 1) * ch.n, (k + 1) * ch.n + ch.n));
        const v = b.length < ch.n ? a : ch.n === 4 ? slerp(a, b, f) : a.map((x, i) => x + (b[i] - x) * f);
        const nd = this.nodes[ch.node];
        if (ch.path === 'rotation') nd.r = v; else if (ch.path === 'translation') nd.t = v; else nd.s = v;
      }
    }
    world() {
      const W = new Array(this.nodes.length);
      const rec = (i, pm) => { const n = this.nodes[i]; const m = mul(pm, trs(n.t, n.r, n.s)); W[i] = m; n.children.forEach((c) => rec(c, m)); };
      this.nodes.forEach((n, i) => { if (this.parent[i] < 0) rec(i, mat4()); });
      return W;
    }
    render(dt = 0) {
      const gl = this.gl, c = this.c;
      if (!this.meshes) return;
      const w = c.clientWidth * (window.devicePixelRatio || 1), h = c.clientHeight * (window.devicePixelRatio || 1);
      if (c.width !== w || c.height !== h) { c.width = w; c.height = h; }
      this.t += dt * this.speed; this.yaw += dt * this.spin;
      this.sample(this.t);
      const W = this.world();
      gl.viewport(0, 0, c.width, c.height);
      gl.clearColor(0, 0, 0, 0); gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
      gl.enable(gl.DEPTH_TEST);
      const ce = this.center, d = this.dist;
      const eye = [ce[0] + Math.sin(this.yaw) * Math.cos(this.pitch) * d, ce[1] + Math.sin(this.pitch) * d, ce[2] + Math.cos(this.yaw) * Math.cos(this.pitch) * d];
      const VP = mul(persp(0.62, c.width / c.height, 0.02, 100), lookAt(eye, ce, [0, 1, 0]));
      // Bodenschatten
      gl.useProgram(this.gprog); gl.enable(gl.BLEND); gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA); gl.depthMask(false);
      gl.uniformMatrix4fv(gl.getUniformLocation(this.gprog, 'uVP'), false, VP); gl.uniform1f(gl.getUniformLocation(this.gprog, 'uR'), Math.max(0.3, this.radius * 1.2));
      gl.bindVertexArray(this.gvao); gl.drawArrays(gl.TRIANGLES, 0, this.gcount); gl.depthMask(true); gl.disable(gl.BLEND);
      const p = this.prog; gl.useProgram(p);
      const U = (n) => gl.getUniformLocation(p, n);
      gl.uniformMatrix4fv(U('uVP'), false, VP); gl.uniform3fv(U('uCam'), eye); gl.uniform1f(U('uExposure'), this.exposure);
      gl.uniform1i(U('tBase'), 0); gl.uniform1i(U('tNrm'), 1); gl.uniform1i(U('tOrm'), 2); gl.uniform1i(U('tEm'), 3);
      this.nodes.forEach((n, i) => {
        if (n.mesh === undefined) return;
        let skin = false;
        if (n.skin !== undefined) {
          const s = this.skins[n.skin]; const arr = new Float32Array(96 * 16);
          s.joints.forEach((j, k) => { arr.set(mul(W[j], s.ibm.subarray(k * 16, k * 16 + 16)), k * 16); });
          gl.uniformMatrix4fv(U('uJ'), false, arr); skin = true;
        }
        gl.uniform1i(U('uSkin'), skin ? 1 : 0);
        gl.uniformMatrix4fv(U('uModel'), false, W[i]);
        for (const pr of this.meshes[n.mesh]) {
          const m = pr.mat;
          gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, m.base);
          gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, m.nrm);
          gl.activeTexture(gl.TEXTURE2); gl.bindTexture(gl.TEXTURE_2D, m.orm);
          gl.activeTexture(gl.TEXTURE3); gl.bindTexture(gl.TEXTURE_2D, m.em || this.white);
          gl.uniform1i(U('uHasEm'), m.em ? 1 : 0); gl.uniform3fv(U('uEmF'), m.emF); gl.uniform4fv(U('uBaseF'), m.baseF);
          gl.bindVertexArray(pr.vao); gl.drawElements(gl.TRIANGLES, pr.count, pr.type, 0);
        }
      });
      gl.bindVertexArray(null);
    }
    start() {
      let last = performance.now();
      const loop = (now) => { this.render(Math.min(0.05, (now - last) / 1000)); last = now; this.raf = requestAnimationFrame(loop); };
      this.raf = requestAnimationFrame(loop);
    }
    stop() { cancelAnimationFrame(this.raf); }
  }
  window.GlbViewer = GlbViewer;
})();

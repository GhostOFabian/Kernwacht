import { box, cylinder, disc, leaf, octahedron, plane, pyramid, ring, roundedBox, sphere, torus, type MeshData } from './geometry.ts';
import { hexToRgb, writeTRS } from './math3d.ts';

// WebGL2-Renderer mit:
//  * instanziertem Zeichnen (ein Draw Call je Mesh-Art und Pass, Instanzpuffer pro Batch),
//  * weicher Beleuchtung (Normalen, Umgebungslicht, Glanz, Randlicht) und Schattenkarte (PCF),
//  * Materialien: Standard, Wasser, Lava, Wind (Pflanzen), unbeleuchtet,
//  * Sprite-Partikeln (weiche Kreise, Funken, Ringe, Rauch, Sterne, Strahlen) additiv oder mit Alpha,
//  * Nachbearbeitung: MSAA, Glühen (Bloom), Farbstimmung, Vignette.

export type MeshId =
  | 'box' | 'rbox' | 'cyl6' | 'cyl8' | 'cyl12' | 'cyl16' | 'cone4' | 'cone6' | 'cone8' | 'cone12'
  | 'octa' | 'sphere' | 'sphereLo' | 'gem' | 'ring' | 'ringThin' | 'disc' | 'torus' | 'hex' | 'plane' | 'leaf';

export type Pass = 'opaque' | 'transparent' | 'overlay';

/** Materialkennungen (pro Instanz). */
export const MAT = { std: 0, water: 1, lava: 2, sway: 3, unlit: 4, terrain: 5 } as const;

export type SpriteShape = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;
/** 0 Glühen, 1 weiche Scheibe, 2 Ring, 3 Funke/Streifen, 4 Rauch, 5 Stern, 6 Strahl, 7 Wirbel (3.13) */
export const SHAPE = { glow: 0, disc: 1, ring: 2, spark: 3, smoke: 4, star: 5, beam: 6, vortex: 7 } as const;

const FLOATS = 22;
const SFLOATS = 17;

const VS = `#version 300 es
layout(location=0) in vec3 a_pos;
layout(location=1) in vec3 a_nrm;
layout(location=2) in vec4 a_m0;
layout(location=3) in vec4 a_m1;
layout(location=4) in vec4 a_m2;
layout(location=5) in vec4 a_m3;
layout(location=6) in vec4 a_color;
layout(location=7) in vec2 a_em;
uniform mat4 u_vp;
uniform mat4 u_lightVP;
uniform float u_time;
out vec3 v_world;
out vec3 v_n;
out vec4 v_color;
out float v_emit;
out float v_mat;
out vec4 v_lpos;
void main() {
  mat4 m = mat4(a_m0, a_m1, a_m2, a_m3);
  vec4 w = m * vec4(a_pos, 1.0);
  float mat = a_em.y;
  if (mat > 2.5 && mat < 3.5) {
    float h = max(0.0, w.y - a_m3.y);
    w.x += sin(u_time * 1.7 + w.x * 0.8 + w.z * 0.6) * 0.06 * h;
    w.z += cos(u_time * 1.3 + w.x * 0.5 + w.z * 0.3) * 0.05 * h;
  }
  if (mat > 0.5 && mat < 2.5) {
    w.y += sin(u_time * 1.1 + w.x * 0.6) * 0.05 + cos(u_time * 0.8 + w.z * 0.5) * 0.05;
  }
  // Kofaktormatrix statt inverse(): korrekt für ungleichmäßige Skalierung und deutlich günstiger.
  vec3 c0 = a_m0.xyz, c1 = a_m1.xyz, c2 = a_m2.xyz;
  mat3 nm = mat3(cross(c1, c2), cross(c2, c0), cross(c0, c1));
  v_n = normalize(nm * a_nrm);
  v_world = w.xyz;
  v_color = a_color;
  v_emit = a_em.x;
  v_mat = mat;
  v_lpos = u_lightVP * w;
  gl_Position = u_vp * w;
}`;

const FS = `#version 300 es
precision highp float;
precision highp sampler2DShadow;
in vec3 v_world;
in vec3 v_n;
in vec4 v_color;
in float v_emit;
in float v_mat;
in vec4 v_lpos;
uniform vec3 u_light;
uniform vec3 u_lightColor;
uniform vec3 u_sky;
uniform vec3 u_ground;
uniform vec3 u_toCam;
uniform float u_unlit;
uniform vec3 u_fog;
uniform vec3 u_fogTop;
uniform vec4 u_fogParams;
uniform vec4 u_island;
uniform vec2 u_res;
uniform float u_clouds;
uniform float u_fancy;
uniform float u_time;
uniform sampler2DShadow u_shadow;
uniform float u_shadowOn;
out vec4 o;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { return vnoise(p) * 0.55 + vnoise(p * 2.13 + 7.1) * 0.3 + vnoise(p * 4.37 - 3.3) * 0.15; }
float waveH(vec2 q) { return vnoise(q * 0.45 + vec2(u_time * 0.16, u_time * 0.1)) * 0.65 + vnoise(q * 1.3 - vec2(u_time * 0.12, -u_time * 0.2)) * 0.35; }
float shadowAt(vec3 n) {
  vec3 c = v_lpos.xyz / v_lpos.w * 0.5 + 0.5;
  if (c.x < 0.0 || c.x > 1.0 || c.y < 0.0 || c.y > 1.0 || c.z > 1.0) return 1.0;
  float bias = 0.0012 + 0.0025 * (1.0 - max(dot(n, u_light), 0.0));
  vec2 ts = 1.0 / vec2(textureSize(u_shadow, 0));
  float s = 0.0;
  for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) s += texture(u_shadow, vec3(c.xy + vec2(x, y) * ts * 1.2, c.z - bias));
  return s / 9.0;
}
void main() {
  vec3 n = normalize(v_n);
  if (dot(n, u_toCam) < -0.2) n = -n;
  vec3 base = v_color.rgb;
  float emit = v_emit;
  // 3.13: Gelände (5) wird wie Standard beleuchtet, bekommt aber durchgehende Farbvariation über die Zellgrenzen hinweg
  bool terrain = v_mat > 4.5;
  float mt = terrain ? 0.0 : v_mat;
  bool water = mt > 0.5 && mt < 1.5;
  float foam = 0.0;
  if (terrain) {
    vec2 q = v_world.xz;
    // Niedrige Qualität: je eine Rauschabfrage statt fbm
    float big = u_fancy > 0.5 ? fbm(q * 0.18) : vnoise(q * 0.18);
    float mid = u_fancy > 0.5 ? fbm(q * 0.9 + 3.0) : 0.5;
    float fine = u_fancy > 0.5 ? vnoise(q * 6.0) : 0.5;
    float up = clamp(n.y, 0.0, 1.0);
    base *= mix(0.9 + 0.1 * fine, 0.8 + 0.34 * big + 0.12 * mid + 0.06 * fine, up);
    base = mix(base, base * vec3(1.08, 1.04, 0.86), smoothstep(0.55, 0.85, big) * 0.5 * up);
    base = mix(base, base * vec3(0.88, 0.96, 1.06), smoothstep(0.45, 0.15, mid) * 0.35 * up);
    // Seitenflächen: Gesteinsschichten
    base *= mix(0.92 + 0.12 * vnoise(vec2(q.x + q.y, v_world.y * 9.0)), 1.0, up);
  }
  if (water && u_fancy < 0.5) {
    base *= 0.76;
    n = vec3(0.0, 1.0, 0.0);
  } else if (water) {
    vec2 q = v_world.xz;
    float e = 0.12;
    float hx = waveH(q + vec2(e, 0.0)) - waveH(q - vec2(e, 0.0));
    float hz = waveH(q + vec2(0.0, e)) - waveH(q - vec2(0.0, e));
    vec3 wn = normalize(vec3(-hx * 0.55, 2.0 * e * 2.2, -hz * 0.55));
    n = wn;
    float rip = sin(v_world.x * 2.4 + u_time * 1.4) * sin(v_world.z * 2.1 - u_time * 1.1);
    base *= 0.76 + 0.05 * rip;
    // Schaum am Inselufer (Abstand zum Inselrechteck)
    vec2 d2 = abs(q - u_island.xy) - u_island.zw;
    float sd = length(max(d2, 0.0)) + min(max(d2.x, d2.y), 0.0);
    float band = sd - 0.15 - 0.25 * sin(u_time * 1.2 + (q.x + q.y) * 0.7);
    foam = smoothstep(1.4, 0.0, band) * smoothstep(-0.6, 0.1, sd);
    foam *= 0.55 + 0.45 * vnoise(q * 3.0 + u_time * 0.6);
    foam += smoothstep(0.08, 0.0, abs(band - 1.6 - 0.5 * sin(u_time * 0.8))) * 0.35 * smoothstep(-0.2, 0.5, sd);
  } else if (mt > 1.5 && mt < 2.5) {
    vec2 q = v_world.xz;
    float f = 0.5 + 0.5 * sin(q.x * 1.6 + u_time * 0.7) * cos(q.y * 1.2 - u_time * 0.5);
    float g = 0.5 + 0.5 * sin(q.x * 4.1 - q.y * 3.3 + u_time * 1.9);
    float crust = smoothstep(0.42, 0.62, fbm(q * 0.8 + vec2(u_time * 0.04, 0.0)));
    base = mix(base, vec3(1.0, 0.78, 0.25), f * 0.6 + g * 0.15);
    base = mix(base, vec3(0.16, 0.08, 0.07), crust * 0.75);
    emit = max(emit, (0.55 + 0.4 * f) * (1.0 - crust * 0.8));
  }
  float ndl = dot(n, u_light);
  float wrap = max((ndl + 0.3) / 1.3, 0.0);
  float sh = 1.0;
#ifdef SHADOWS
  if (u_shadowOn > 0.5) sh = shadowAt(n);
#endif
  // Wolkenschatten ziehen langsam über die Insel
  if (u_clouds > 0.0 && u_unlit < 0.25 && u_fancy > 0.5) {
    float cl = smoothstep(0.42, 0.78, fbm(v_world.xz * 0.075 + vec2(u_time * 0.022, u_time * 0.013)));
    sh *= 1.0 - cl * u_clouds;
  }
  vec3 amb = mix(u_ground, u_sky, n.y * 0.5 + 0.5);
  float fres = pow(1.0 - max(dot(n, u_toCam), 0.0), 3.0);
  vec3 hv = normalize(u_light + u_toCam);
  float spec = pow(max(dot(n, hv), 0.0), 28.0) * 0.22 * sh;
  if (water) spec = pow(max(dot(n, hv), 0.0), 140.0) * 0.8 * sh + pow(max(dot(n, hv), 0.0), 18.0) * 0.05;
  // Gegenlicht-Randlicht in Lichtfarbe (Silhouetten leuchten zur Sonne hin)
  float back = clamp(dot(-u_toCam, u_light) * 0.5 + 0.5, 0.0, 1.0);
  vec3 rimCol = mix(u_sky, u_lightColor, back * 0.7);
  vec3 lit = base * (amb + u_lightColor * wrap * mix(0.32, 1.0, sh)) + u_lightColor * spec + rimCol * fres * (terrain ? 0.08 : 0.3);
  if (water) {
    vec3 refl = mix(u_fog, u_fogTop, 0.5) * 1.15;
    lit = mix(lit, refl, clamp(0.06 + fres * 0.45, 0.0, 0.5));
    lit = mix(lit, vec3(0.92, 0.96, 1.0) * (amb + u_lightColor * 0.6), clamp(foam, 0.0, 1.0) * 0.8);
  }
  float unlit = clamp(max(u_unlit, (mt > 3.5 && mt < 4.5) ? 1.0 : 0.0), 0.0, 1.0);
  // Emission dezent: Farbe bleibt erkennbar, statt weiß auszubrennen
  vec3 col = mix(lit, base, max(unlit, clamp(emit, 0.0, 1.0) * 0.5)) + base * min(emit, 1.5) * 0.35;
  // Leichte Kantenabdunklung für klarere Silhouetten (nicht bei Wasser/Lava/unbeleuchtet)
  if (mt < 0.5 || (mt > 2.5 && mt < 3.5)) col *= 1.0 - fres * (terrain ? 0.08 : 0.16);
  float below = clamp(-v_world.y * 0.16, 0.0, 0.6);
  col *= 1.0 - below * (mt > 0.5 && mt < 2.5 ? 0.2 : (mt > 3.5 ? 0.0 : 1.0));
  float d = length(v_world.xz - u_fogParams.xy);
  float f = smoothstep(u_fogParams.z, u_fogParams.w, d);
  float vy = clamp(gl_FragCoord.y / max(u_res.y, 1.0), 0.0, 1.0);
  vec3 fogCol = mix(u_fog, u_fogTop, smoothstep(0.2, 1.0, vy));
  col = mix(col, fogCol, f * 0.94);
  o = vec4(col, v_color.a);
}`;

const SHADOW_VS = `#version 300 es
layout(location=0) in vec3 a_pos;
layout(location=2) in vec4 a_m0;
layout(location=3) in vec4 a_m1;
layout(location=4) in vec4 a_m2;
layout(location=5) in vec4 a_m3;
uniform mat4 u_lightVP;
void main() {
  mat4 m = mat4(a_m0, a_m1, a_m2, a_m3);
  gl_Position = u_lightVP * m * vec4(a_pos, 1.0);
}`;

const SHADOW_FS = `#version 300 es
precision mediump float;
void main() {}`;

const SPRITE_VS = `#version 300 es
layout(location=0) in vec2 a_corner;
layout(location=1) in vec3 i_pos;
layout(location=2) in vec3 i_u;
layout(location=3) in vec3 i_v;
layout(location=4) in vec4 i_color;
layout(location=5) in vec4 i_params;
uniform mat4 u_vp;
out vec2 v_uv;
out vec4 v_color;
out vec4 v_params;
void main() {
  vec3 w = i_pos + i_u * a_corner.x + i_v * a_corner.y;
  v_uv = a_corner;
  v_color = i_color;
  v_params = i_params;
  gl_Position = u_vp * vec4(w, 1.0);
}`;

const SPRITE_FS = `#version 300 es
precision highp float;
in vec2 v_uv;
in vec4 v_color;
in vec4 v_params;
uniform float u_add;
out vec4 o;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void main() {
  float r = length(v_uv);
  int shape = int(v_params.x + 0.5);
  float a = 0.0;
  if (shape == 0) a = exp(-r * r * 3.2) * smoothstep(1.0, 0.7, r);
  else if (shape == 1) a = smoothstep(1.0, 1.0 - max(v_params.y, 0.04), r);
  else if (shape == 2) { float w = max(v_params.z, 0.04); a = smoothstep(w, 0.0, abs(r - (1.0 - w))); }
  else if (shape == 3) a = exp(-(v_uv.x * v_uv.x * 2.0 + v_uv.y * v_uv.y * 10.0));
  else if (shape == 4) { float n = noise(v_uv * 2.2 + v_params.w) * 0.6 + noise(v_uv * 4.7 - v_params.w) * 0.4; a = smoothstep(1.0, 0.15, r) * (0.35 + 0.65 * n); }
  else if (shape == 7) { float ang = atan(v_uv.y, v_uv.x); float sw = sin(ang * 3.0 + r * 9.0 - v_params.w * 3.0) * 0.5 + 0.5; a = smoothstep(1.0, 0.25, r) * (0.25 + 0.75 * sw * sw) * smoothstep(0.0, 0.2, r) + exp(-r * r * 14.0) * 0.8; }
  else if (shape == 5) a = exp(-r * r * 7.0) + (max(0.0, 1.0 - abs(v_uv.x) * 9.0) * max(0.0, 1.0 - abs(v_uv.y)) + max(0.0, 1.0 - abs(v_uv.y) * 9.0) * max(0.0, 1.0 - abs(v_uv.x))) * 0.7;
  else { a = (1.0 - smoothstep(0.0, 1.0, abs(v_uv.y))) * smoothstep(1.0, 0.85, abs(v_uv.x)); a = a * a + (1.0 - smoothstep(0.0, 0.25, abs(v_uv.y))) * 0.6; }
  a *= v_color.a;
  if (a < 0.003) discard;
  o = u_add > 0.5 ? vec4(v_color.rgb * a, a) : vec4(v_color.rgb, a);
}`;

const FULL_VS = `#version 300 es
out vec2 v_uv;
void main() {
  vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
  v_uv = p;
  gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}`;

const BRIGHT_FS = `#version 300 es
precision highp float;
in vec2 v_uv;
uniform sampler2D u_tex;
uniform float u_threshold;
out vec4 o;
void main() {
  vec3 c = texture(u_tex, v_uv).rgb;
  float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
  o = vec4(c * smoothstep(u_threshold, u_threshold + 0.25, l), 1.0);
}`;

const BLUR_FS = `#version 300 es
precision highp float;
in vec2 v_uv;
uniform sampler2D u_tex;
uniform vec2 u_dir;
out vec4 o;
void main() {
  vec3 c = texture(u_tex, v_uv).rgb * 0.227027;
  c += texture(u_tex, v_uv + u_dir * 1.3846).rgb * 0.316216;
  c += texture(u_tex, v_uv - u_dir * 1.3846).rgb * 0.316216;
  c += texture(u_tex, v_uv + u_dir * 3.2308).rgb * 0.070270;
  c += texture(u_tex, v_uv - u_dir * 3.2308).rgb * 0.070270;
  o = vec4(c, 1.0);
}`;

const SKY_FS = `#version 300 es
precision highp float;
in vec2 v_uv;
uniform vec3 u_low;
uniform vec3 u_top;
uniform vec3 u_glow;
uniform float u_stars;
uniform float u_time;
out vec4 o;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void main() {
  float y = v_uv.y;
  vec3 c = mix(u_low, u_top, smoothstep(0.2, 1.0, y));
  // Horizontglühen und weiche Wolkenbänder
  c += u_glow * exp(-pow((y - 0.8) * 3.0, 2.0)) * 0.35;
  float cl = vnoise(vec2(v_uv.x * 3.0 + u_time * 0.008, y * 9.0)) * 0.6 + vnoise(vec2(v_uv.x * 7.0 - u_time * 0.012, y * 21.0)) * 0.4;
  c = mix(c, c * 1.18 + u_glow * 0.05, smoothstep(0.55, 0.85, cl) * 0.35);
  if (u_stars > 0.0) {
    vec2 g = floor(gl_FragCoord.xy / 3.0);
    float h = hash(g);
    float tw = 0.6 + 0.4 * sin(u_time * 2.0 + h * 50.0);
    c += vec3(0.9, 0.92, 1.0) * step(0.9965, h) * tw * u_stars * smoothstep(0.3, 0.9, y);
  }
  o = vec4(c, 1.0);
}`;

const COMPOSITE_FS = `#version 300 es
precision highp float;
in vec2 v_uv;
uniform sampler2D u_scene;
uniform sampler2D u_bloom;
uniform sampler2D u_blur;
uniform float u_bloomOn;
uniform float u_bloomStrength;
uniform vec3 u_tint;
uniform float u_saturation;
uniform float u_contrast;
uniform float u_vignette;
uniform float u_flash;
uniform float u_dof;
uniform float u_split;
uniform vec4 u_sun;
uniform vec3 u_sunCol;
uniform float u_time;
uniform vec2 u_res;
out vec4 o;
float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
void main() {
  vec2 uv = v_uv;
  vec2 q = uv - 0.5;
  vec3 c;
  if (u_dof > 0.0) {
    // Leichte Farbsäume zu den Rändern (Objektiv-Look)
    vec2 ca = q * dot(q, q) * 0.012 * u_dof;
    c = vec3(texture(u_scene, uv + ca).r, texture(u_scene, uv).g, texture(u_scene, uv - ca).b);
    // Tilt-Shift: Ferne (oben) stärker, Vordergrund (unten) leichter unscharf – Miniatur-/Diorama-Wirkung
    float dy = uv.y - 0.52;
    float k = dy > 0.0 ? smoothstep(0.2, 0.5, dy) : smoothstep(0.3, 0.55, -dy) * 0.6;
    c = mix(c, texture(u_blur, uv).rgb, clamp(k * u_dof, 0.0, 0.85));
  } else c = texture(u_scene, uv).rgb;
  if (u_bloomOn > 0.5) c += texture(u_bloom, uv).rgb * u_bloomStrength;
  c *= u_tint;
  float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
  c = mix(vec3(l), c, u_saturation);
  // Split-Toning: kühle Schatten, warme Lichter
  c += vec3(-0.025, 0.0, 0.045) * (1.0 - smoothstep(0.0, 0.45, l)) * u_split;
  c += vec3(0.04, 0.02, -0.03) * smoothstep(0.5, 1.0, l) * u_split;
  c = (c - 0.5) * u_contrast + 0.5;
  // Sanfter Schulterverlauf statt hartem Abschneiden der Lichter
  c = mix(c, 1.0 - exp(-c * 1.9) * 1.03 + 0.03 * c, 0.35 * u_split);
  // Sonnendunst aus Richtung der Lichtquelle
  if (u_sun.z > 0.0) {
    vec2 sd = (uv - u_sun.xy) * vec2(u_res.x / max(u_res.y, 1.0), 1.0);
    c += u_sunCol * exp(-dot(sd, sd) * 1.6) * u_sun.z;
  }
  c *= 1.0 - dot(q, q) * u_vignette;
  c += vec3(1.0, 0.95, 0.85) * u_flash;
  float dither = (hash(gl_FragCoord.xy) - 0.5) / 255.0;
  float grain = (hash(gl_FragCoord.xy + fract(u_time * 7.13) * 91.0) - 0.5) * 0.018 * u_split;
  o = vec4(clamp(c, 0.0, 1.0) + dither + grain, 1.0);
}`;

interface Mesh { vbo: WebGLBuffer; count: number }

class Batch {
  data: Float32Array;
  count = 0;
  vao: WebGLVertexArrayObject | null = null;
  buf: WebGLBuffer | null = null;
  capacity = 0;
  dirty = true;
  constructor(readonly floats: number, cap = 64) {
    this.data = new Float32Array(cap * floats);
  }
  reserve(): number {
    if ((this.count + 1) * this.floats > this.data.length) {
      const n = new Float32Array(this.data.length * 2);
      n.set(this.data);
      this.data = n;
    }
    return this.count++ * this.floats;
  }
}

export interface Lighting {
  light: [number, number, number];
  lightColor: [number, number, number];
  sky: [number, number, number];
  ground: [number, number, number];
  fog: [number, number, number];
  fogCenter: [number, number];
  fogStart: number;
  fogEnd: number;
  clear: [number, number, number];
  /** 3.13: Himmels-/Nebelfarbe am oberen Bildrand (Verlauf zum Horizont). */
  fogTop?: [number, number, number];
  /** Horizontglühen im Himmel. */
  skyGlow?: [number, number, number];
  /** Sterne am Himmel (0–1). */
  stars?: number;
  /** Stärke der Wolkenschatten (0–1). */
  clouds?: number;
  /** Inselrechteck für Uferschaum: Mitte x/z, halbe Breite/Tiefe. */
  island?: [number, number, number, number];
}

export interface Grade {
  tint: [number, number, number]; saturation: number; contrast: number; vignette: number; bloom: number; flash: number;
  /** 3.13: Tilt-Shift-Unschärfe (0–1), Split-Toning/Filmkorn (0–1), Sonnendunst (Bildschirm-x/y, Stärke) und Farbe. */
  dof?: number; split?: number; sun?: [number, number, number]; sunColor?: [number, number, number];
}

export interface RenderQuality { msaa: number; shadowSize: number; bloom: boolean; /** 3.13: Tilt-Shift-Unschärfe */ dof?: boolean }

const MESH_FACTORIES: Record<MeshId, () => MeshData> = {
  box: () => box(),
  rbox: () => roundedBox(0.14),
  cyl6: () => cylinder(6),
  cyl8: () => cylinder(10, 0.5, 0.5, true, true),
  cyl12: () => cylinder(14, 0.5, 0.5, true, true),
  cyl16: () => cylinder(20, 0.5, 0.5, true, true),
  cone4: () => pyramid(4),
  cone6: () => cylinder(8, 0.5, 0, true, true),
  cone8: () => cylinder(12, 0.5, 0, true, true),
  cone12: () => cylinder(18, 0.5, 0, true, true),
  octa: () => octahedron(),
  sphere: () => sphere(1, true),
  sphereLo: () => sphere(0, true),
  gem: () => sphere(0, false),
  ring: () => ring(48, 0.88),
  ringThin: () => ring(48, 0.95),
  disc: () => disc(32),
  torus: () => torus(18, 6, 0.12),
  hex: () => cylinder(6, 0.5, 0.5),
  plane: () => plane(32),
  leaf: () => leaf(),
};

export const MESH_IDS = Object.keys(MESH_FACTORIES) as MeshId[];

interface Target { fbo: WebGLFramebuffer; tex: WebGLTexture; w: number; h: number }

export class Renderer {
  readonly gl: WebGL2RenderingContext;
  private prog: WebGLProgram;
  private progShadow: WebGLProgram;
  private progPlain: WebGLProgram;
  private uShadow: Record<string, WebGLUniformLocation | null> = {};
  private uPlain: Record<string, WebGLUniformLocation | null> = {};
  private shadowProg: WebGLProgram;
  private spriteProg: WebGLProgram;
  private brightProg: WebGLProgram;
  private blurProg: WebGLProgram;
  private compProg: WebGLProgram;
  private skyProg: WebGLProgram;
  private dofA: Target | null = null;
  private dofB: Target | null = null;
  private u: Record<string, WebGLUniformLocation | null> = {};
  private us: Record<string, WebGLUniformLocation | null> = {};
  private meshes = new Map<MeshId, Mesh>();
  private dyn: Record<Pass, Map<MeshId, Batch>> = { opaque: new Map(), transparent: new Map(), overlay: new Map() };
  private stat: Record<Pass, Map<MeshId, Batch>> = { opaque: new Map(), transparent: new Map(), overlay: new Map() };
  private spritesAdd = new Batch(SFLOATS, 256);
  private spritesAlpha = new Batch(SFLOATS, 256);
  private spritesAddTop = new Batch(SFLOATS, 64);
  private quadVbo: WebGLBuffer;
  private emptyVao: WebGLVertexArrayObject;
  private staticTarget = false;
  private msaaFbo: WebGLFramebuffer | null = null;
  private msaaColor: WebGLRenderbuffer | null = null;
  private msaaDepth: WebGLRenderbuffer | null = null;
  private scene: Target | null = null;
  private sceneDepth: WebGLRenderbuffer | null = null;
  private bloomA: Target | null = null;
  private bloomB: Target | null = null;
  private shadowFbo: WebGLFramebuffer | null = null;
  private shadowTex: WebGLTexture | null = null;
  private shadowSize = 0;
  private fbW = 0;
  private fbH = 0;
  private maxSamples = 0;
  quality: RenderQuality = { msaa: 4, shadowSize: 2048, bloom: true };
  /** Multiplikator für additive Sprites (Glühen, Ringe, Funken). */
  glowScale = 0.5;
  readonly lightVP = new Float32Array(16);
  camRight: [number, number, number] = [1, 0, 0];
  camUp: [number, number, number] = [0, 1, 0];
  time = 0;
  drawCalls = 0;
  instances = 0;
  lost = false;

  constructor(readonly canvas: HTMLCanvasElement, opts: { preserve?: boolean } = {}) {
    const gl = canvas.getContext('webgl2', { antialias: false, alpha: false, depth: true, powerPreference: 'high-performance', preserveDrawingBuffer: !!opts.preserve });
    if (!gl) throw new Error('WebGL2 wird von diesem Browser nicht unterstützt.');
    this.gl = gl;
    canvas.addEventListener('webglcontextlost', (e) => {
      e.preventDefault();
      this.lost = true;
    });
    this.maxSamples = (gl.getParameter(gl.MAX_SAMPLES) as number) || 0;
    // Zwei Varianten des Hauptshaders: mit und ohne Schattenabfrage (spart auf schwacher Hardware viel Füllrate).
    this.progShadow = this.program(VS, FS.replace('#version 300 es', '#version 300 es\n#define SHADOWS'));
    this.progPlain = this.program(VS, FS);
    for (const [prog, map] of [[this.progShadow, this.uShadow], [this.progPlain, this.uPlain]] as const) {
      for (const n of ['u_vp', 'u_lightVP', 'u_time', 'u_light', 'u_lightColor', 'u_sky', 'u_ground', 'u_toCam', 'u_unlit', 'u_fog', 'u_fogTop', 'u_fogParams', 'u_island', 'u_res', 'u_clouds', 'u_fancy', 'u_shadow', 'u_shadowOn']) {
        map[n] = gl.getUniformLocation(prog, n);
      }
    }
    this.prog = this.progPlain;
    this.u = this.uPlain;
    this.shadowProg = this.program(SHADOW_VS, SHADOW_FS);
    this.us.lightVP = gl.getUniformLocation(this.shadowProg, 'u_lightVP');
    this.spriteProg = this.program(SPRITE_VS, SPRITE_FS);
    this.us.spriteVP = gl.getUniformLocation(this.spriteProg, 'u_vp');
    this.us.spriteAdd = gl.getUniformLocation(this.spriteProg, 'u_add');
    this.brightProg = this.program(FULL_VS, BRIGHT_FS);
    this.blurProg = this.program(FULL_VS, BLUR_FS);
    this.compProg = this.program(FULL_VS, COMPOSITE_FS);
    this.skyProg = this.program(FULL_VS, SKY_FS);
    for (const id of MESH_IDS) this.meshes.set(id, this.createMesh(MESH_FACTORIES[id]()));
    const qb = gl.createBuffer();
    const ev = gl.createVertexArray();
    if (!qb || !ev) throw new Error('GPU-Puffer konnte nicht erstellt werden');
    this.quadVbo = qb;
    this.emptyVao = ev;
    gl.bindBuffer(gl.ARRAY_BUFFER, qb);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1]), gl.STATIC_DRAW);
  }

  private program(vs: string, fs: string): WebGLProgram {
    const gl = this.gl;
    const sh = (type: number, src: string): WebGLShader => {
      const s = gl.createShader(type);
      if (!s) throw new Error('Shader konnte nicht erstellt werden');
      gl.shaderSource(s, src);
      gl.compileShader(s);
      if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(`Shaderfehler: ${gl.getShaderInfoLog(s) ?? ''}`);
      return s;
    };
    const p = gl.createProgram();
    if (!p) throw new Error('Programm konnte nicht erstellt werden');
    gl.attachShader(p, sh(gl.VERTEX_SHADER, vs));
    gl.attachShader(p, sh(gl.FRAGMENT_SHADER, fs));
    gl.linkProgram(p);
    if (!gl.getProgramParameter(p, gl.LINK_STATUS)) throw new Error(`Linkfehler: ${gl.getProgramInfoLog(p) ?? ''}`);
    return p;
  }

  private createMesh(data: MeshData): Mesh {
    const gl = this.gl;
    const vbo = gl.createBuffer();
    if (!vbo) throw new Error('GPU-Puffer konnte nicht erstellt werden');
    const inter = new Float32Array((data.positions.length / 3) * 6);
    for (let i = 0; i < data.positions.length / 3; i++) {
      inter[i * 6] = data.positions[i * 3];
      inter[i * 6 + 1] = data.positions[i * 3 + 1];
      inter[i * 6 + 2] = data.positions[i * 3 + 2];
      inter[i * 6 + 3] = data.normals[i * 3];
      inter[i * 6 + 4] = data.normals[i * 3 + 1];
      inter[i * 6 + 5] = data.normals[i * 3 + 2];
    }
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(gl.ARRAY_BUFFER, inter, gl.STATIC_DRAW);
    return { vbo, count: data.positions.length / 3 };
  }

  /** VAO für eine Batch: Meshgeometrie + eigener Instanzpuffer. */
  private ensureMeshBatch(id: MeshId, b: Batch): void {
    const gl = this.gl;
    if (b.vao && b.buf) return;
    const m = this.meshes.get(id);
    if (!m) return;
    b.vao = gl.createVertexArray();
    b.buf = gl.createBuffer();
    gl.bindVertexArray(b.vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, m.vbo);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 24, 0);
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 3, gl.FLOAT, false, 24, 12);
    gl.bindBuffer(gl.ARRAY_BUFFER, b.buf);
    b.capacity = 64;
    gl.bufferData(gl.ARRAY_BUFFER, b.capacity * FLOATS * 4, gl.DYNAMIC_DRAW);
    const stride = FLOATS * 4;
    for (let i = 0; i < 4; i++) {
      gl.enableVertexAttribArray(2 + i);
      gl.vertexAttribPointer(2 + i, 4, gl.FLOAT, false, stride, i * 16);
      gl.vertexAttribDivisor(2 + i, 1);
    }
    gl.enableVertexAttribArray(6);
    gl.vertexAttribPointer(6, 4, gl.FLOAT, false, stride, 64);
    gl.vertexAttribDivisor(6, 1);
    gl.enableVertexAttribArray(7);
    gl.vertexAttribPointer(7, 2, gl.FLOAT, false, stride, 80);
    gl.vertexAttribDivisor(7, 1);
    gl.bindVertexArray(null);
  }

  private ensureSpriteBatch(b: Batch): void {
    const gl = this.gl;
    if (b.vao && b.buf) return;
    b.vao = gl.createVertexArray();
    b.buf = gl.createBuffer();
    gl.bindVertexArray(b.vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, this.quadVbo);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 8, 0);
    gl.bindBuffer(gl.ARRAY_BUFFER, b.buf);
    b.capacity = 256;
    gl.bufferData(gl.ARRAY_BUFFER, b.capacity * SFLOATS * 4, gl.DYNAMIC_DRAW);
    const stride = SFLOATS * 4;
    const sizes = [3, 3, 3, 4, 4];
    let off = 0;
    for (let i = 0; i < sizes.length; i++) {
      gl.enableVertexAttribArray(1 + i);
      gl.vertexAttribPointer(1 + i, sizes[i], gl.FLOAT, false, stride, off);
      gl.vertexAttribDivisor(1 + i, 1);
      off += sizes[i] * 4;
    }
    gl.bindVertexArray(null);
  }

  private upload(b: Batch, floats: number): void {
    const gl = this.gl;
    if (!b.buf || !b.dirty || b.count === 0) return;
    gl.bindBuffer(gl.ARRAY_BUFFER, b.buf);
    if (b.count > b.capacity) {
      b.capacity = Math.max(b.count, b.capacity * 2);
      gl.bufferData(gl.ARRAY_BUFFER, b.capacity * floats * 4, gl.DYNAMIC_DRAW);
    }
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, b.data, 0, b.count * floats);
    b.dirty = false;
  }

  /** Startet das Befüllen statischer Batches (Terrain) – bleiben bis zum nächsten beginStatic() erhalten. */
  beginStatic(): void {
    for (const p of ['opaque', 'transparent', 'overlay'] as Pass[]) {
      for (const b of this.stat[p].values()) {
        b.count = 0;
        b.dirty = true;
      }
    }
    this.staticTarget = true;
  }

  endStatic(): void {
    this.staticTarget = false;
  }

  /** Fügt eine Mesh-Instanz hinzu. mat: Material (MAT.*). */
  add(
    pass: Pass, mesh: MeshId,
    x: number, y: number, z: number,
    sx: number, sy: number, sz: number,
    color: number, alpha = 1, emit = 0,
    yaw = 0, pitch = 0, roll = 0, mat = 0,
  ): void {
    const store = this.staticTarget ? this.stat[pass] : this.dyn[pass];
    let b = store.get(mesh);
    if (!b) {
      b = new Batch(FLOATS);
      store.set(mesh, b);
    }
    const o = b.reserve();
    b.dirty = true;
    writeTRS(b.data, o, x, y, z, yaw, pitch, roll, sx, sy, sz);
    const [r, g, bl] = hexToRgb(color);
    b.data[o + 16] = r;
    b.data[o + 17] = g;
    b.data[o + 18] = bl;
    b.data[o + 19] = alpha;
    b.data[o + 20] = emit;
    b.data[o + 21] = mat;
  }

  /** Sprite mit expliziten Achsen (Halbgrößen-Vektoren U/V). */
  sprite(
    additive: boolean, shape: SpriteShape,
    x: number, y: number, z: number,
    ux: number, uy: number, uz: number, vx: number, vy: number, vz: number,
    color: number, alpha: number, p1 = 0.3, p2 = 0.15, seed = 0, top = false,
  ): void {
    const b = top ? this.spritesAddTop : additive ? this.spritesAdd : this.spritesAlpha;
    const o = b.reserve();
    b.dirty = true;
    const d = b.data;
    d[o] = x; d[o + 1] = y; d[o + 2] = z;
    d[o + 3] = ux; d[o + 4] = uy; d[o + 5] = uz;
    d[o + 6] = vx; d[o + 7] = vy; d[o + 8] = vz;
    const [r, g, bl] = hexToRgb(color);
    // Additive Leuchteffekte gedämpft (Lesbarkeit vor Glanz)
    d[o + 9] = r; d[o + 10] = g; d[o + 11] = bl; d[o + 12] = additive || top ? alpha * this.glowScale : alpha;
    d[o + 13] = shape; d[o + 14] = p1; d[o + 15] = p2; d[o + 16] = seed;
  }

  /** Kamerazugewandtes Sprite. */
  billboard(additive: boolean, shape: SpriteShape, x: number, y: number, z: number, size: number, color: number, alpha: number, rot = 0, stretch = 1, seed = 0, top = false): void {
    const [rx, ry, rz] = this.camRight;
    const [qx, qy, qz] = this.camUp;
    const c = Math.cos(rot), s = Math.sin(rot);
    const ux = (rx * c + qx * s) * size * stretch, uy = (ry * c + qy * s) * size * stretch, uz = (rz * c + qz * s) * size * stretch;
    const vx = (-rx * s + qx * c) * size, vy = (-ry * s + qy * c) * size, vz = (-rz * s + qz * c) * size;
    this.sprite(additive, shape, x, y, z, ux, uy, uz, vx, vy, vz, color, alpha, 0.3, 0.15, seed, top);
  }

  /** Flach auf dem Boden liegendes Sprite (Dekale, Ringe, Schockwellen). */
  ground(additive: boolean, shape: SpriteShape, x: number, y: number, z: number, size: number, color: number, alpha: number, rot = 0, p1 = 0.3, p2 = 0.15): void {
    const c = Math.cos(rot) * size, s = Math.sin(rot) * size;
    this.sprite(additive, shape, x, y, z, c, 0, s, -s, 0, c, color, alpha, p1, p2);
  }

  /** Leuchtender Strahl zwischen zwei Punkten (kamerazugewandt). */
  line(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number, width: number, color: number, alpha: number, additive = true, top = false): void {
    const dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
    const len = Math.hypot(dx, dy, dz);
    if (len < 1e-4) return;
    // Senkrechte zur Linie und zur Blickrichtung
    const [tx, ty, tz] = this.toCam;
    let px = dy * tz - dz * ty, py = dz * tx - dx * tz, pz = dx * ty - dy * tx;
    const pl = Math.hypot(px, py, pz) || 1;
    px = (px / pl) * width; py = (py / pl) * width; pz = (pz / pl) * width;
    this.sprite(additive, 6, (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2, dx / 2, dy / 2, dz / 2, px, py, pz, color, alpha, 0, 0, 0, top);
  }

  toCam: [number, number, number] = [0, 1, 0];

  setQuality(q: RenderQuality): void {
    this.quality = { ...q, msaa: Math.min(q.msaa, this.maxSamples) };
    this.fbW = 0; // Ziele neu anlegen
  }

  resize(w: number, h: number, dpr: number): void {
    const cw = Math.max(1, Math.round(w * dpr));
    const ch = Math.max(1, Math.round(h * dpr));
    if (this.canvas.width !== cw || this.canvas.height !== ch) {
      this.canvas.width = cw;
      this.canvas.height = ch;
    }
  }

  private makeTarget(w: number, h: number, old: Target | null): Target {
    const gl = this.gl;
    if (old) {
      gl.deleteFramebuffer(old.fbo);
      gl.deleteTexture(old.tex);
    }
    const tex = gl.createTexture();
    const fbo = gl.createFramebuffer();
    if (!tex || !fbo) throw new Error('Framebuffer konnte nicht erstellt werden');
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
    return { fbo, tex, w, h };
  }

  private ensureTargets(): void {
    const gl = this.gl;
    const w = this.canvas.width;
    const h = this.canvas.height;
    if (w === this.fbW && h === this.fbH && this.scene) return;
    this.fbW = w;
    this.fbH = h;
    this.scene = this.makeTarget(w, h, this.scene);
    if (this.sceneDepth) gl.deleteRenderbuffer(this.sceneDepth);
    this.sceneDepth = gl.createRenderbuffer();
    gl.bindRenderbuffer(gl.RENDERBUFFER, this.sceneDepth);
    gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT24, w, h);
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.scene.fbo);
    gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, this.sceneDepth);
    if (this.msaaFbo) gl.deleteFramebuffer(this.msaaFbo);
    if (this.msaaColor) gl.deleteRenderbuffer(this.msaaColor);
    if (this.msaaDepth) gl.deleteRenderbuffer(this.msaaDepth);
    this.msaaFbo = null;
    const samples = Math.min(this.quality.msaa, this.maxSamples);
    if (samples > 1) {
      this.msaaFbo = gl.createFramebuffer();
      this.msaaColor = gl.createRenderbuffer();
      this.msaaDepth = gl.createRenderbuffer();
      gl.bindRenderbuffer(gl.RENDERBUFFER, this.msaaColor);
      gl.renderbufferStorageMultisample(gl.RENDERBUFFER, samples, gl.RGBA8, w, h);
      gl.bindRenderbuffer(gl.RENDERBUFFER, this.msaaDepth);
      gl.renderbufferStorageMultisample(gl.RENDERBUFFER, samples, gl.DEPTH_COMPONENT24, w, h);
      gl.bindFramebuffer(gl.FRAMEBUFFER, this.msaaFbo);
      gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.RENDERBUFFER, this.msaaColor);
      gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, this.msaaDepth);
      if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) this.msaaFbo = null;
    }
    const bw = Math.max(1, w >> 1);
    const bh = Math.max(1, h >> 1);
    this.bloomA = this.makeTarget(bw, bh, this.bloomA);
    this.bloomB = this.makeTarget(bw, bh, this.bloomB);
    const dw = Math.max(1, w >> 2);
    const dh = Math.max(1, h >> 2);
    this.dofA = this.makeTarget(dw, dh, this.dofA);
    this.dofB = this.makeTarget(dw, dh, this.dofB);
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  }

  private ensureShadow(): void {
    const gl = this.gl;
    const size = this.quality.shadowSize;
    if (size === this.shadowSize) return;
    if (this.shadowTex) gl.deleteTexture(this.shadowTex);
    if (this.shadowFbo) gl.deleteFramebuffer(this.shadowFbo);
    this.shadowTex = null;
    this.shadowFbo = null;
    this.shadowSize = size;
    if (size <= 0) return;
    const tex = gl.createTexture();
    const fbo = gl.createFramebuffer();
    if (!tex || !fbo) return;
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.DEPTH_COMPONENT24, size, size, 0, gl.DEPTH_COMPONENT, gl.UNSIGNED_INT, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_COMPARE_MODE, gl.COMPARE_REF_TO_TEXTURE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_COMPARE_FUNC, gl.LEQUAL);
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.TEXTURE_2D, tex, 0);
    if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
      gl.deleteFramebuffer(fbo);
      gl.deleteTexture(tex);
      this.shadowSize = 0;
    } else {
      this.shadowTex = tex;
      this.shadowFbo = fbo;
    }
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  }

  private uploadAll(): void {
    for (const p of ['opaque', 'transparent', 'overlay'] as Pass[]) {
      for (const [id, b] of this.stat[p]) {
        this.ensureMeshBatch(id, b);
        this.upload(b, FLOATS);
      }
      for (const [id, b] of this.dyn[p]) {
        this.ensureMeshBatch(id, b);
        this.upload(b, FLOATS);
      }
    }
    for (const b of [this.spritesAdd, this.spritesAlpha, this.spritesAddTop]) {
      this.ensureSpriteBatch(b);
      this.upload(b, SFLOATS);
    }
  }

  render(vp: Float32Array, toCam: [number, number, number], L: Lighting, grade: Grade): void {
    const gl = this.gl;
    if (this.lost || gl.isContextLost()) return;
    this.drawCalls = 0;
    this.instances = 0;
    this.ensureTargets();
    this.ensureShadow();
    this.uploadAll();
    gl.disable(gl.CULL_FACE);

    // 1) Schattenkarte
    const shadowOn = !!(this.shadowFbo && this.shadowTex);
    if (shadowOn && this.shadowFbo) {
      gl.bindFramebuffer(gl.FRAMEBUFFER, this.shadowFbo);
      gl.viewport(0, 0, this.shadowSize, this.shadowSize);
      gl.clear(gl.DEPTH_BUFFER_BIT);
      gl.enable(gl.DEPTH_TEST);
      gl.depthFunc(gl.LEQUAL);
      gl.depthMask(true);
      gl.disable(gl.BLEND);
      gl.enable(gl.POLYGON_OFFSET_FILL);
      gl.polygonOffset(1.5, 2);
      gl.useProgram(this.shadowProg);
      gl.uniformMatrix4fv(this.us.lightVP, false, this.lightVP);
      for (const id of MESH_IDS) {
        if (id === 'plane' || id === 'ring' || id === 'ringThin' || id === 'disc') continue;
        const s = this.stat.opaque.get(id);
        const d = this.dyn.opaque.get(id);
        if (s && s.count) this.drawMeshBatch(id, s);
        if (d && d.count) this.drawMeshBatch(id, d);
      }
      gl.disable(gl.POLYGON_OFFSET_FILL);
    }

    this.prog = shadowOn ? this.progShadow : this.progPlain;
    this.u = shadowOn ? this.uShadow : this.uPlain;

    // 2) Szene (MSAA, falls verfügbar)
    const sceneFbo = this.msaaFbo ?? this.scene?.fbo ?? null;
    gl.bindFramebuffer(gl.FRAMEBUFFER, sceneFbo);
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
    gl.clearColor(L.clear[0], L.clear[1], L.clear[2], 1);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
    // 3.13: Himmelsverlauf als Hintergrund (passt zur Nebelfarbe am jeweiligen Bildrand)
    const fogTop = L.fogTop ?? L.fog;
    gl.disable(gl.DEPTH_TEST);
    gl.depthMask(false);
    gl.disable(gl.BLEND);
    gl.useProgram(this.skyProg);
    gl.bindVertexArray(this.emptyVao);
    gl.uniform3fv(gl.getUniformLocation(this.skyProg, 'u_low'), L.fog);
    gl.uniform3fv(gl.getUniformLocation(this.skyProg, 'u_top'), fogTop);
    gl.uniform3fv(gl.getUniformLocation(this.skyProg, 'u_glow'), L.skyGlow ?? [0, 0, 0]);
    gl.uniform1f(gl.getUniformLocation(this.skyProg, 'u_stars'), L.stars ?? 0);
    gl.uniform1f(gl.getUniformLocation(this.skyProg, 'u_time'), this.time);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    gl.bindVertexArray(null);
    gl.depthMask(true);
    gl.useProgram(this.prog);
    gl.uniformMatrix4fv(this.u.u_vp, false, vp);
    gl.uniformMatrix4fv(this.u.u_lightVP, false, this.lightVP);
    gl.uniform1f(this.u.u_time, this.time);
    gl.uniform3fv(this.u.u_light, L.light);
    gl.uniform3fv(this.u.u_lightColor, L.lightColor);
    gl.uniform3fv(this.u.u_sky, L.sky);
    gl.uniform3fv(this.u.u_ground, L.ground);
    gl.uniform3fv(this.u.u_toCam, toCam);
    gl.uniform3fv(this.u.u_fog, L.fog);
    gl.uniform3fv(this.u.u_fogTop, fogTop);
    const isl = L.island ?? [0, 0, -100, -100];
    gl.uniform4f(this.u.u_island, isl[0], isl[1], isl[2], isl[3]);
    gl.uniform2f(this.u.u_res, this.canvas.width, this.canvas.height);
    gl.uniform1f(this.u.u_clouds, L.clouds ?? 0);
    gl.uniform1f(this.u.u_fancy, this.quality.bloom ? 1 : 0);
    gl.uniform4f(this.u.u_fogParams, L.fogCenter[0], L.fogCenter[1], L.fogStart, L.fogEnd);
    gl.uniform1f(this.u.u_shadowOn, shadowOn ? 1 : 0);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, shadowOn ? this.shadowTex : null);
    gl.uniform1i(this.u.u_shadow, 0);

    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);
    gl.depthMask(true);
    gl.disable(gl.BLEND);
    gl.uniform1f(this.u.u_unlit, 0);
    this.drawPass('opaque');

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    gl.depthMask(false);
    gl.uniform1f(this.u.u_unlit, 0.5);
    this.drawPass('transparent');

    // Sprites: Alpha (Rauch, Dekale) dann additiv (Glühen, Funken)
    gl.useProgram(this.spriteProg);
    gl.uniformMatrix4fv(this.us.spriteVP, false, vp);
    gl.uniform1f(this.us.spriteAdd, 0);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    this.drawSprites(this.spritesAlpha);
    gl.uniform1f(this.us.spriteAdd, 1);
    gl.blendFunc(gl.ONE, gl.ONE);
    this.drawSprites(this.spritesAdd);

    gl.disable(gl.DEPTH_TEST);
    this.drawSprites(this.spritesAddTop);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    gl.useProgram(this.prog);
    gl.uniform1f(this.u.u_unlit, 1);
    gl.uniform1f(this.u.u_shadowOn, 0);
    this.drawPass('overlay');
    gl.depthMask(true);
    gl.disable(gl.BLEND);

    // 3) MSAA auflösen
    if (this.msaaFbo && this.scene) {
      gl.bindFramebuffer(gl.READ_FRAMEBUFFER, this.msaaFbo);
      gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, this.scene.fbo);
      gl.blitFramebuffer(0, 0, this.canvas.width, this.canvas.height, 0, 0, this.canvas.width, this.canvas.height, gl.COLOR_BUFFER_BIT, gl.NEAREST);
    }

    // 4) Glühen
    const bloom = this.quality.bloom && this.scene && this.bloomA && this.bloomB;
    gl.bindVertexArray(this.emptyVao);
    if (bloom && this.scene && this.bloomA && this.bloomB) {
      gl.bindFramebuffer(gl.FRAMEBUFFER, this.bloomA.fbo);
      gl.viewport(0, 0, this.bloomA.w, this.bloomA.h);
      gl.useProgram(this.brightProg);
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, this.scene.tex);
      gl.uniform1i(gl.getUniformLocation(this.brightProg, 'u_tex'), 0);
      gl.uniform1f(gl.getUniformLocation(this.brightProg, 'u_threshold'), 0.86);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
      gl.useProgram(this.blurProg);
      const dirLoc = gl.getUniformLocation(this.blurProg, 'u_dir');
      gl.uniform1i(gl.getUniformLocation(this.blurProg, 'u_tex'), 0);
      for (let i = 0; i < 3; i++) {
        const sp = 1 + i;
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.bloomB.fbo);
        gl.bindTexture(gl.TEXTURE_2D, this.bloomA.tex);
        gl.uniform2f(dirLoc, sp / this.bloomA.w, 0);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.bloomA.fbo);
        gl.bindTexture(gl.TEXTURE_2D, this.bloomB.tex);
        gl.uniform2f(dirLoc, 0, sp / this.bloomA.h);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
      }
    }

    // 4b) Tilt-Shift: verkleinerte, weichgezeichnete Kopie der Szene
    const dof = !!(this.quality.dof && (grade.dof ?? 0) > 0 && this.scene && this.dofA && this.dofB);
    if (dof && this.scene && this.dofA && this.dofB) {
      gl.useProgram(this.blurProg);
      const dirLoc = gl.getUniformLocation(this.blurProg, 'u_dir');
      gl.uniform1i(gl.getUniformLocation(this.blurProg, 'u_tex'), 0);
      gl.activeTexture(gl.TEXTURE0);
      gl.viewport(0, 0, this.dofA.w, this.dofA.h);
      let src = this.scene.tex;
      for (let i = 0; i < 2; i++) {
        const sp = 1 + i * 1.5;
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.dofA.fbo);
        gl.bindTexture(gl.TEXTURE_2D, src);
        gl.uniform2f(dirLoc, (i === 0 ? 2 : sp) / (i === 0 ? this.canvas.width : this.dofA.w), 0);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.dofB.fbo);
        gl.bindTexture(gl.TEXTURE_2D, this.dofA.tex);
        gl.uniform2f(dirLoc, 0, sp / this.dofA.h);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        src = this.dofB.tex;
      }
    }

    // 5) Zusammensetzen auf den Bildschirm
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
    gl.useProgram(this.compProg);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, this.scene ? this.scene.tex : null);
    gl.uniform1i(gl.getUniformLocation(this.compProg, 'u_scene'), 0);
    gl.activeTexture(gl.TEXTURE1);
    gl.bindTexture(gl.TEXTURE_2D, this.bloomA ? this.bloomA.tex : null);
    gl.uniform1i(gl.getUniformLocation(this.compProg, 'u_bloom'), 1);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_bloomOn'), bloom ? 1 : 0);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_bloomStrength'), grade.bloom);
    gl.uniform3fv(gl.getUniformLocation(this.compProg, 'u_tint'), grade.tint);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_saturation'), grade.saturation);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_contrast'), grade.contrast);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_vignette'), grade.vignette);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_flash'), grade.flash);
    gl.activeTexture(gl.TEXTURE2);
    gl.bindTexture(gl.TEXTURE_2D, dof && this.dofB ? this.dofB.tex : null);
    gl.uniform1i(gl.getUniformLocation(this.compProg, 'u_blur'), 2);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_dof'), dof ? grade.dof ?? 0 : 0);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_split'), grade.split ?? 0);
    const sun = grade.sun ?? [0, 0, 0];
    gl.uniform4f(gl.getUniformLocation(this.compProg, 'u_sun'), sun[0], sun[1], this.quality.bloom ? sun[2] : sun[2] * 0.6, 0);
    gl.uniform3fv(gl.getUniformLocation(this.compProg, 'u_sunCol'), grade.sunColor ?? [1, 0.9, 0.7]);
    gl.uniform1f(gl.getUniformLocation(this.compProg, 'u_time'), this.time);
    gl.uniform2f(gl.getUniformLocation(this.compProg, 'u_res'), this.canvas.width, this.canvas.height);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    gl.activeTexture(gl.TEXTURE2);
    gl.bindTexture(gl.TEXTURE_2D, null);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindVertexArray(null);

    for (const p of ['opaque', 'transparent', 'overlay'] as Pass[]) for (const b of this.dyn[p].values()) b.count = 0;
    this.spritesAdd.count = 0;
    this.spritesAlpha.count = 0;
    this.spritesAddTop.count = 0;
  }

  private drawPass(pass: Pass): void {
    for (const id of MESH_IDS) {
      const s = this.stat[pass].get(id);
      const d = this.dyn[pass].get(id);
      if (s && s.count > 0) this.drawMeshBatch(id, s);
      if (d && d.count > 0) this.drawMeshBatch(id, d);
    }
  }

  private drawMeshBatch(id: MeshId, b: Batch): void {
    const gl = this.gl;
    const m = this.meshes.get(id);
    if (!m || !b.vao) return;
    gl.bindVertexArray(b.vao);
    gl.drawArraysInstanced(gl.TRIANGLES, 0, m.count, b.count);
    this.drawCalls++;
    this.instances += b.count;
  }

  private drawSprites(b: Batch): void {
    if (b.count === 0 || !b.vao) return;
    const gl = this.gl;
    gl.bindVertexArray(b.vao);
    gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, b.count);
    this.drawCalls++;
    this.instances += b.count;
  }

  dispose(): void {
    const gl = this.gl;
    for (const m of this.meshes.values()) gl.deleteBuffer(m.vbo);
    gl.deleteProgram(this.progShadow);
    gl.deleteProgram(this.progPlain);
    gl.deleteProgram(this.skyProg);
    this.meshes.clear();
  }
}

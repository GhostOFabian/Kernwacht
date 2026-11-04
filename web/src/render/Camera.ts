import { invert, lookAt, mat4, multiply, ortho, transformPoint, type Mat4 } from './math3d.ts';

// Isometrische, orthografische Kamera mit begrenztem Schwenken und Zoomen sowie 90°-Rotation.

const PITCH = Math.atan(1 / Math.SQRT2); // echte Isometrie (~35,26°)
/** Steilere Draufsicht (Vogelperspektive), umschaltbar im Helden-Endlosmodus. */
const PITCH_TOP = (62 * Math.PI) / 180;
const PITCH_MIN = (17 * Math.PI) / 180;
const PITCH_MAX = (80 * Math.PI) / 180;

export class IsoCamera {
  tx = 0;
  tz = 0;
  zoom = 8;
  minZoom = 3;
  maxZoom = 16;
  rot = 0;          // Ziel-Rotation in Vierteldrehungen
  /** Aktueller Neigungswinkel (animiert) und Draufsicht-Schalter. */
  pitch = PITCH;
  topDown = false;
  /** Freie Drehung (2.7): zusätzlicher Gierwinkel und Neigungsversatz, vom Spieler per Maus/Touch gesetzt. */
  freeYaw = 0;
  pitchAdj = 0;
  private yawAnim = Math.PI / 4;
  width = 1;
  height = 1;
  mapW = 20;
  mapH = 14;
  readonly view: Mat4 = mat4();
  readonly proj: Mat4 = mat4();
  readonly vp: Mat4 = mat4();
  readonly inv: Mat4 = mat4();
  toCam: [number, number, number] = [0, 1, 0];
  right: [number, number, number] = [1, 0, 0];
  up: [number, number, number] = [0, 1, 0];
  /** Zusätzliche, kontinuierliche Drehung (Menü-Kamerafahrt). */
  spin = 0;
  private spinYaw = 0;
  /** Kamerafahrt zu einem Punkt (Bossauftritt). */
  private focusT = 0;
  private focusDur = 0;
  private focusFrom: [number, number, number] = [0, 0, 0];
  private focusTo: [number, number, number] = [0, 0, 0];

  get yaw(): number {
    return this.yawAnim + this.spinYaw + this.freeYaw;
  }

  /** Freies Drehen: Gier (Bogenmaß) und Neigung (Bogenmaß, begrenzt auf ca. 17°–80°). */
  orbit(dYaw: number, dPitch: number): void {
    this.freeYaw += dYaw;
    const base = this.topDown ? PITCH_TOP : PITCH;
    this.pitchAdj = Math.max(PITCH_MIN - base, Math.min(PITCH_MAX - base, this.pitchAdj + dPitch));
  }

  /** Blickwinkel auf die Standardansicht zurücksetzen. */
  resetView(): void {
    this.rot = Math.round((this.yawAnim - Math.PI / 4) / (Math.PI / 2));
    this.freeYaw = 0;
    this.pitchAdj = 0;
  }

  /** Aktueller Blickwinkel in Grad (für die Anzeige). */
  get yawDeg(): number {
    const d = (((this.yaw * 180) / Math.PI) % 360 + 360) % 360;
    return Math.round(d);
  }

  setMap(w: number, h: number): void {
    this.mapW = w;
    this.mapH = h;
    this.tx = w / 2;
    this.tz = h / 2;
    this.fit();
  }

  /** Zoom so wählen, dass die ganze Karte sichtbar ist. */
  fit(): void {
    const aspect = this.width / Math.max(1, this.height);
    const diag = (this.mapW + this.mapH) / Math.SQRT2;
    const horiz = diag / 2;
    const vert = (diag * Math.sin(this.pitch)) / 2 + 1.2;
    this.maxZoom = Math.max(horiz / aspect, vert) * 1.25;
    this.zoom = Math.max(horiz / aspect, vert) * 1.04;
    this.minZoom = 2.6;
    this.clamp();
  }

  resize(w: number, h: number): void {
    const changed = w !== this.width || h !== this.height;
    this.width = Math.max(1, w);
    this.height = Math.max(1, h);
    if (changed) this.fit();
  }

  /** Fährt für dur Sekunden zu (x, z) mit Zoomfaktor und kehrt dann zurück. */
  focus(x: number, z: number, zoomMul: number, dur: number): void {
    this.focusFrom = [this.tx, this.tz, this.zoom];
    this.focusTo = [x, z, Math.max(this.minZoom, this.zoom * zoomMul)];
    this.focusT = dur;
    this.focusDur = dur;
  }

  get focusing(): boolean {
    return this.focusT > 0;
  }

  /** Schaltet zwischen isometrischer Ansicht und steiler Draufsicht um. */
  toggleTopDown(): boolean {
    this.topDown = !this.topDown;
    return this.topDown;
  }

  update(dt: number): void {
    const wantPitch = Math.max(PITCH_MIN, Math.min(PITCH_MAX, (this.topDown ? PITCH_TOP : PITCH) + this.pitchAdj));
    this.pitch += (wantPitch - this.pitch) * Math.min(1, dt * 6);
    const target = Math.PI / 4 + this.rot * (Math.PI / 2);
    const diff = target - this.yawAnim;
    this.yawAnim += Math.abs(diff) < 0.001 ? diff : diff * Math.min(1, dt * 10);
    this.spinYaw += this.spin * dt;
    if (this.focusT > 0) {
      this.focusT = Math.max(0, this.focusT - dt);
      const p = 1 - this.focusT / this.focusDur;
      // 0..0.25 hin, 0.25..0.8 halten, 0.8..1 zurück
      const k = p < 0.25 ? ease(p / 0.25) : p < 0.8 ? 1 : 1 - ease((p - 0.8) / 0.2);
      this.tx = this.focusFrom[0] + (this.focusTo[0] - this.focusFrom[0]) * k;
      this.tz = this.focusFrom[1] + (this.focusTo[1] - this.focusFrom[1]) * k;
      this.zoom = this.focusFrom[2] + (this.focusTo[2] - this.focusFrom[2]) * k;
    }
    const yaw = this.yawAnim + this.spinYaw + this.freeYaw;
    const aspect = this.width / this.height;
    const z = this.zoom;
    ortho(this.proj, -z * aspect, z * aspect, -z, z, 0.1, 200);
    const d = 60;
    const dx = Math.cos(this.pitch) * Math.sin(yaw);
    const dy = Math.sin(this.pitch);
    const dz = Math.cos(this.pitch) * Math.cos(yaw);
    this.toCam = [dx, dy, dz];
    this.right = [Math.cos(yaw), 0, -Math.sin(yaw)];
    // up = toCam × right … senkrecht zu Blickrichtung und Rechtsvektor
    const r = this.right;
    this.up = [dy * r[2] - dz * r[1], dz * r[0] - dx * r[2], dx * r[1] - dy * r[0]];
    lookAt(this.view, this.tx + dx * d, dy * d, this.tz + dz * d, this.tx, 0, this.tz);
    multiply(this.vp, this.proj, this.view);
    invert(this.inv, this.vp);
  }

  rotate(dir: number): void {
    this.rot += dir;
  }

  pan(dxPix: number, dyPix: number): void {
    const s = (2 * this.zoom) / this.height;
    const yaw = this.yaw;
    const rx = Math.cos(yaw);
    const rz = -Math.sin(yaw);
    const fx = -Math.sin(yaw);
    const fz = -Math.cos(yaw);
    const k = s / Math.sin(this.pitch);
    // Inhalt folgt dem Finger: Kamera bewegt sich entgegengesetzt zur Zeigerbewegung.
    this.tx += -dxPix * s * rx + dyPix * k * fx;
    this.tz += -dxPix * s * rz + dyPix * k * fz;
    this.clamp();
  }

  zoomBy(factor: number, px?: number, py?: number): void {
    const before = px !== undefined && py !== undefined ? this.screenToGround(px, py, 0) : null;
    this.zoom = Math.min(this.maxZoom, Math.max(this.minZoom, this.zoom * factor));
    this.update(0);
    if (before && px !== undefined && py !== undefined) {
      const after = this.screenToGround(px, py, 0);
      if (after) {
        this.tx += before.x - after.x;
        this.tz += before.z - after.z;
      }
    }
    this.clamp();
  }

  clamp(): void {
    const m = 1;
    this.tx = Math.min(this.mapW + m, Math.max(-m, this.tx));
    this.tz = Math.min(this.mapH + m, Math.max(-m, this.tz));
  }

  /** Bildschirmkoordinate (CSS-Pixel) → Schnittpunkt mit Ebene y = planeY. */
  screenToGround(px: number, py: number, planeY: number): { x: number; z: number } | null {
    const nx = (px / this.width) * 2 - 1;
    const ny = 1 - (py / this.height) * 2;
    const a = transformPoint(this.inv, nx, ny, -1);
    const b = transformPoint(this.inv, nx, ny, 1);
    const ax = a[0] / a[3], ay = a[1] / a[3], az = a[2] / a[3];
    const bx = b[0] / b[3], by = b[1] / b[3], bz = b[2] / b[3];
    const dy = by - ay;
    if (Math.abs(dy) < 1e-6) return null;
    const t = (planeY - ay) / dy;
    return { x: ax + (bx - ax) * t, z: az + (bz - az) * t };
  }

  worldToScreen(x: number, y: number, z: number): { x: number; y: number } {
    const p = transformPoint(this.vp, x, y, z);
    return { x: ((p[0] / p[3]) * 0.5 + 0.5) * this.width, y: (1 - ((p[1] / p[3]) * 0.5 + 0.5)) * this.height };
  }

  /** Bildschirmrechts-Vektor in Weltkoordinaten (für Lebensbalken). */
  rightVector(): [number, number] {
    return [Math.cos(this.yaw), -Math.sin(this.yaw)];
  }
}

function ease(t: number): number {
  const c = Math.min(1, Math.max(0, t));
  return c * c * (3 - 2 * c);
}

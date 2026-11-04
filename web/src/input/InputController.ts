// Zeiger-, Touch- und Tastatursteuerung für die Spielfläche.
// Maus: Klick = Aktion, Ziehen (links/rechts/Mitte) = Schwenken, Mausrad = Zoom, Hover = Bauvorschau.
// Touch: Tippen = Aktion, Ein-Finger-Ziehen = Schwenken, Zwei Finger = Zoomen/Schwenken.

export interface InputHandlers {
  /** time = Zeitstempel des auslösenden Ereignisses (für Doppelklicks, unabhängig von Bildrate/Last). */
  tap(px: number, py: number, pointerType: string, time?: number): void;
  hover(px: number, py: number): void;
  hoverEnd(): void;
  pan(dx: number, dy: number): void;
  zoom(factor: number, px: number, py: number): void;
  /** Freies Drehen der Kamera (Gier, Neigung in Bogenmaß). */
  orbit?(dYaw: number, dPitch: number): void;
  key(e: KeyboardEvent): void;
}

interface Ptr { id: number; x: number; y: number; sx: number; sy: number; t: number; type: string; orbit: boolean }

export class InputController {
  private ptrs = new Map<number, Ptr>();
  private dragging = false;
  private pinchDist = 0;
  private pinchMid = { x: 0, y: 0 };
  private pinchAng = 0;
  private readonly listeners: [EventTarget, string, EventListener, AddEventListenerOptions?][] = [];
  enabled = true;

  constructor(private readonly el: HTMLElement, private readonly h: InputHandlers) {
    this.on(el, 'pointerdown', (e) => this.down(e as PointerEvent));
    this.on(el, 'pointermove', (e) => this.move(e as PointerEvent));
    this.on(el, 'pointerup', (e) => this.up(e as PointerEvent));
    this.on(el, 'pointercancel', (e) => this.cancel(e as PointerEvent));
    this.on(el, 'pointerleave', (e) => {
      if ((e as PointerEvent).pointerType === 'mouse') this.h.hoverEnd();
    });
    this.on(el, 'wheel', (e) => {
      const w = e as WheelEvent;
      w.preventDefault();
      const r = this.el.getBoundingClientRect();
      this.h.zoom(Math.exp(w.deltaY * 0.0012), w.clientX - r.left, w.clientY - r.top);
    }, { passive: false });
    this.on(el, 'contextmenu', (e) => e.preventDefault());
    // Mittlere Maustaste: kein Autoscroll des Browsers
    this.on(el, 'auxclick', (e) => e.preventDefault());
    this.on(el, 'mousedown', (e) => { if ((e as MouseEvent).button === 1) e.preventDefault(); });
    this.on(window, 'keydown', (e) => {
      const k = e as KeyboardEvent;
      const t = k.target as HTMLElement | null;
      if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT')) return;
      if (this.enabled) this.h.key(k);
    });
  }

  private on(t: EventTarget, type: string, fn: EventListener, opts?: AddEventListenerOptions): void {
    t.addEventListener(type, fn, opts);
    this.listeners.push([t, type, fn, opts]);
  }

  private local(e: PointerEvent): { x: number; y: number } {
    const r = this.el.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  }

  private down(e: PointerEvent): void {
    if (!this.enabled) return;
    const p = this.local(e);
    try {
      this.el.setPointerCapture(e.pointerId);
    } catch {
      /* Pointer-Capture nicht verfügbar */
    }
    // Mittlere Maustaste oder Strg/Alt + Ziehen: Kamera frei drehen
    const orbit = e.pointerType === 'mouse' && (e.button === 1 || e.ctrlKey || e.altKey);
    if (orbit) e.preventDefault();
    this.ptrs.set(e.pointerId, { id: e.pointerId, x: p.x, y: p.y, sx: p.x, sy: p.y, t: performance.now(), type: e.pointerType, orbit });
    if (this.ptrs.size === 2) {
      const [a, b] = [...this.ptrs.values()];
      this.pinchAng = Math.atan2(b.y - a.y, b.x - a.x);
      this.pinchDist = Math.hypot(a.x - b.x, a.y - b.y);
      this.pinchMid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
      this.dragging = true;
    }
  }

  private move(e: PointerEvent): void {
    const p = this.local(e);
    const ptr = this.ptrs.get(e.pointerId);
    if (!ptr) {
      if (e.pointerType === 'mouse' && this.enabled) this.h.hover(p.x, p.y);
      return;
    }
    const dx = p.x - ptr.x;
    const dy = p.y - ptr.y;
    ptr.x = p.x;
    ptr.y = p.y;
    if (this.ptrs.size >= 2) {
      const [a, b] = [...this.ptrs.values()];
      const d = Math.hypot(a.x - b.x, a.y - b.y);
      const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
      if (this.pinchDist > 0 && d > 0) this.h.zoom(this.pinchDist / d, mid.x, mid.y);
      this.h.pan(mid.x - this.pinchMid.x, mid.y - this.pinchMid.y);
      // Zwei Finger drehen: Kamera frei rotieren
      const ang = Math.atan2(b.y - a.y, b.x - a.x);
      let da = ang - this.pinchAng;
      if (da > Math.PI) da -= Math.PI * 2;
      if (da < -Math.PI) da += Math.PI * 2;
      if (Math.abs(da) < 0.5) this.h.orbit?.(-da, 0);
      this.pinchAng = ang;
      this.pinchDist = d;
      this.pinchMid = mid;
      return;
    }
    const moved = Math.hypot(p.x - ptr.sx, p.y - ptr.sy);
    const threshold = ptr.type === 'mouse' ? 6 : 12;
    if (!this.dragging && moved > threshold) this.dragging = true;
    if (this.dragging && ptr.orbit && this.h.orbit) this.h.orbit(dx * 0.008, dy * 0.006);
    else if (this.dragging) this.h.pan(dx, dy);
    else if (e.pointerType === 'mouse') this.h.hover(p.x, p.y);
  }

  private up(e: PointerEvent): void {
    const ptr = this.ptrs.get(e.pointerId);
    this.ptrs.delete(e.pointerId);
    if (!ptr) return;
    if (!this.dragging && this.ptrs.size === 0 && this.enabled) {
      if (e.pointerType !== 'mouse' || e.button === 0) this.h.tap(ptr.x, ptr.y, ptr.type, e.timeStamp);
    }
    if (this.ptrs.size === 0) this.dragging = false;
  }

  private cancel(e: PointerEvent): void {
    this.ptrs.delete(e.pointerId);
    if (this.ptrs.size === 0) this.dragging = false;
  }

  dispose(): void {
    for (const [t, type, fn, opts] of this.listeners) t.removeEventListener(type, fn, opts);
    this.listeners.length = 0;
  }
}

// Deterministische Zufallszahlen. Alle Generatoren besitzen einen einzigen uint32-Zustand,
// damit sie vollständig serialisierbar sind. Getrennte Ströme werden über hashSeed(seed, label)
// abgeleitet, sodass z. B. kosmetische Zufallsänderungen keine Spielbalance beeinflussen.

/** 32-Bit-Hash (FNV-1a + Avalanche) für Strings. */
export function hashString(str: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return mix32(h >>> 0);
}

/** Avalanche-Mischfunktion (murmur3 fmix32). */
export function mix32(x: number): number {
  x ^= x >>> 16;
  x = Math.imul(x, 0x85ebca6b);
  x ^= x >>> 13;
  x = Math.imul(x, 0xc2b2ae35);
  x ^= x >>> 16;
  return x >>> 0;
}

/** Leitet einen unabhängigen Strom aus Basis-Seed und Bezeichner ab. */
export function hashSeed(base: number, label: string | number): number {
  const l = typeof label === 'number' ? mix32(label + 0x9e3779b9) : hashString(label);
  return mix32((base ^ Math.imul(l, 0x27d4eb2d)) >>> 0) || 0x1234567;
}

/** Mulberry32 – ein Schritt. Gibt [Zufallswert 0..1, neuer Zustand] zurück. */
export function step(state: number): [number, number] {
  const s = (state + 0x6d2b79f5) >>> 0;
  let t = s;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  const v = ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  return [v, s];
}

export class Rng {
  state: number;
  constructor(seed: number) {
    this.state = seed >>> 0;
  }
  next(): number {
    const [v, s] = step(this.state);
    this.state = s;
    return v;
  }
  range(min: number, max: number): number {
    return min + (max - min) * this.next();
  }
  int(min: number, maxInclusive: number): number {
    return min + Math.floor(this.next() * (maxInclusive - min + 1));
  }
  chance(p: number): boolean {
    return this.next() < p;
  }
  pick<T>(arr: readonly T[]): T {
    if (arr.length === 0) throw new Error('Rng.pick: leeres Array');
    return arr[Math.floor(this.next() * arr.length)];
  }
  shuffle<T>(arr: T[]): T[] {
    for (let i = arr.length - 1; i > 0; i--) {
      const j = Math.floor(this.next() * (i + 1));
      const t = arr[i];
      arr[i] = arr[j];
      arr[j] = t;
    }
    return arr;
  }
  weighted<T>(items: readonly T[], weight: (t: T) => number): T {
    let total = 0;
    for (const it of items) total += Math.max(0, weight(it));
    if (total <= 0) return this.pick(items);
    let r = this.next() * total;
    for (const it of items) {
      r -= Math.max(0, weight(it));
      if (r <= 0) return it;
    }
    return items[items.length - 1];
  }
}

const SEED_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

/** Erzeugt einen lesbaren Seed-Text (z. B. "K7QF-M9XA"). */
export function randomSeedText(entropy: number): string {
  const r = new Rng(mix32(entropy >>> 0) || 1);
  let s = '';
  for (let i = 0; i < 8; i++) {
    if (i === 4) s += '-';
    s += SEED_ALPHABET[Math.floor(r.next() * SEED_ALPHABET.length)];
  }
  return s;
}

/** Normalisiert Benutzereingaben für Seeds (Großschreibung, Trimmen, Länge begrenzen). */
export function normalizeSeedText(text: string): string {
  const t = text.trim().toUpperCase().replace(/\s+/g, '-').slice(0, 32);
  return t.length > 0 ? t : 'RIFT-0000';
}

export function dailySeedText(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `DAILY-${y}${m}${d}`;
}

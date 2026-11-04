// Eigene, vollständig synthetische Audioarchitektur (Web Audio API). Keine fremden Assets.
// Getrennte Busse für Musik und Effekte, Gesamtlautstärke, Stummschaltung, Ratenbegrenzung.
// Der AudioContext wird erst nach einer Nutzeraktion gestartet (Autoplay-Richtlinien).

export type SfxId =
  | 'ui' | 'build' | 'sell' | 'upgrade' | 'error' | 'select'
  | 'shot_ranger' | 'shot_ember' | 'shot_ballista' | 'shot_cannon' | 'shot_spore' | 'zap' | 'beam' | 'rift'
  | 'pulse_frost' | 'pulse_sonic' | 'hit' | 'explosion' | 'kill' | 'gold' | 'leak' | 'reaction'
  | 'bossWarn' | 'ability' | 'waveStart' | 'waveClear' | 'victory' | 'defeat' | 'interrupt' | 'teleport' | 'freeze'
  | 'shot_meteor' | 'gravity' | 'bossIntro' | 'heroDeploy' | 'heroDown' | 'heroAbility' | 'levelUp' | 'fusion'
  | 'towerActive' | 'shatter' | 'devour' | 'heal' | 'achievement';

export interface AudioSettings {
  master: number;
  music: number;
  sfx: number;
  muted: boolean;
}

type Osc = OscillatorType;

export class AudioSystem {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private musicBus: GainNode | null = null;
  private sfxBus: GainNode | null = null;
  private noise: AudioBuffer | null = null;
  private reverb: ConvolverNode | null = null;
  private reverbSend: GainNode | null = null;
  private delay: DelayNode | null = null;
  private last = new Map<SfxId, number>();
  private active = 0;
  private musicTimer: number | null = null;
  private nextBeat = 0;
  private beat = 0;
  private scale: number[] = [0, 2, 4, 7, 9];
  private root = 57;
  intensity = 0;   // 0 Ruhe, 1 Kampf, 2 Boss
  settings: AudioSettings = { master: 0.8, music: 0.5, sfx: 0.8, muted: false };
  available = typeof window !== 'undefined' && ('AudioContext' in window || 'webkitAudioContext' in window);

  /** Muss aus einem Nutzer-Event heraus aufgerufen werden. */
  unlock(): void {
    if (!this.available) return;
    try {
      if (!this.ctx) {
        const Ctor = (window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext);
        this.ctx = new Ctor();
        this.master = this.ctx.createGain();
        this.musicBus = this.ctx.createGain();
        this.sfxBus = this.ctx.createGain();
        const comp = this.ctx.createDynamicsCompressor();
        comp.threshold.value = -14;
        comp.ratio.value = 4;
        this.musicBus.connect(this.master);
        this.sfxBus.connect(this.master);
        this.master.connect(comp);
        comp.connect(this.ctx.destination);
        // Hall (erzeugte Impulsantwort) und Echo für einen räumlicheren Klang
        this.reverb = this.ctx.createConvolver();
        this.reverb.buffer = this.impulse(2.4, 2.8);
        this.reverbSend = this.ctx.createGain();
        this.reverbSend.gain.value = 0.32;
        this.reverbSend.connect(this.reverb);
        this.reverb.connect(this.master);
        this.musicBus.connect(this.reverbSend);
        const sfxSend = this.ctx.createGain();
        sfxSend.gain.value = 0.12;
        this.sfxBus.connect(sfxSend);
        sfxSend.connect(this.reverb);
        this.delay = this.ctx.createDelay(1);
        this.delay.delayTime.value = 0.32;
        const fb = this.ctx.createGain();
        fb.gain.value = 0.28;
        const dl = this.ctx.createBiquadFilter();
        dl.type = 'lowpass';
        dl.frequency.value = 2200;
        this.delay.connect(dl);
        dl.connect(fb);
        fb.connect(this.delay);
        dl.connect(this.musicBus);
        const len = this.ctx.sampleRate;
        this.noise = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
        const d = this.noise.getChannelData(0);
        let seed = 12345;
        for (let i = 0; i < len; i++) {
          seed = (seed * 1103515245 + 12345) & 0x7fffffff;
          d[i] = (seed / 0x7fffffff) * 2 - 1;
        }
        this.applyVolumes();
      }
      if (this.ctx.state === 'suspended') void this.ctx.resume();
    } catch (err) {
      console.warn('[Audio] nicht verfügbar', err);
      this.available = false;
    }
  }

  private impulse(seconds: number, decay: number): AudioBuffer | null {
    const ctx = this.ctx;
    if (!ctx) return null;
    const len = Math.floor(ctx.sampleRate * seconds);
    const buf = ctx.createBuffer(2, len, ctx.sampleRate);
    let seed = 987654;
    for (let c = 0; c < 2; c++) {
      const d = buf.getChannelData(c);
      for (let i = 0; i < len; i++) {
        seed = (seed * 1103515245 + 12345) & 0x7fffffff;
        d[i] = ((seed / 0x7fffffff) * 2 - 1) * Math.pow(1 - i / len, decay);
      }
    }
    return buf;
  }

  setSettings(s: AudioSettings): void {
    this.settings = { ...s };
    this.applyVolumes();
  }

  private applyVolumes(): void {
    if (!this.ctx || !this.master || !this.musicBus || !this.sfxBus) return;
    const t = this.ctx.currentTime;
    this.master.gain.setTargetAtTime(this.settings.muted ? 0 : this.settings.master, t, 0.05);
    this.musicBus.gain.setTargetAtTime(this.settings.music * 0.55, t, 0.05);
    this.sfxBus.gain.setTargetAtTime(this.settings.sfx * 0.7, t, 0.05);
  }

  private tone(freq: number, start: number, dur: number, type: Osc, gain: number, bus: AudioNode, o: { attack?: number; slide?: number; filter?: number; q?: number; detune?: number } = {}): void {
    const ctx = this.ctx;
    if (!ctx) return;
    const osc = ctx.createOscillator();
    const g = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, start);
    if (o.detune) osc.detune.setValueAtTime(o.detune, start);
    if (o.slide) osc.frequency.exponentialRampToValueAtTime(Math.max(20, o.slide), start + dur);
    const a = o.attack ?? 0.005;
    g.gain.setValueAtTime(0.0001, start);
    g.gain.exponentialRampToValueAtTime(Math.max(0.0002, gain), start + a);
    g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
    let node: AudioNode = osc;
    if (o.filter) {
      const f = ctx.createBiquadFilter();
      f.type = 'lowpass';
      f.frequency.value = o.filter;
      f.Q.value = o.q ?? 0.7;
      osc.connect(f);
      node = f;
    }
    node.connect(g);
    g.connect(bus);
    osc.start(start);
    osc.stop(start + dur + 0.05);
    this.active++;
    osc.onended = () => {
      this.active--;
      g.disconnect();
    };
  }

  private noiseBurst(start: number, dur: number, gain: number, filterType: BiquadFilterType, freq: number, bus: AudioNode, slide?: number): void {
    const ctx = this.ctx;
    if (!ctx || !this.noise) return;
    const src = ctx.createBufferSource();
    src.buffer = this.noise;
    const f = ctx.createBiquadFilter();
    f.type = filterType;
    f.frequency.setValueAtTime(freq, start);
    if (slide) f.frequency.exponentialRampToValueAtTime(slide, start + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, start);
    g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
    src.connect(f);
    f.connect(g);
    g.connect(bus);
    src.start(start, Math.random() * 0.5);
    src.stop(start + dur + 0.05);
    this.active++;
    src.onended = () => {
      this.active--;
      g.disconnect();
    };
  }

  play(id: SfxId, volume = 1): void {
    const ctx = this.ctx;
    const bus = this.sfxBus;
    if (!ctx || !bus || this.settings.muted || ctx.state !== 'running') return;
    const now = ctx.currentTime;
    const minGap: Partial<Record<SfxId, number>> = { hit: 0.05, shot_ranger: 0.06, zap: 0.07, kill: 0.04, pulse_frost: 0.1, pulse_sonic: 0.12, explosion: 0.08, gold: 0.06, beam: 0.07, reaction: 0.12 };
    const prev = this.last.get(id) ?? -1;
    if (now - prev < (minGap[id] ?? 0.02)) return;
    if (this.active > 48) return;
    this.last.set(id, now);
    const v = volume;
    switch (id) {
      case 'ui': this.tone(880, now, 0.07, 'triangle', 0.15 * v, bus); break;
      case 'select': this.tone(660, now, 0.06, 'triangle', 0.12 * v, bus); this.tone(990, now + 0.04, 0.06, 'triangle', 0.1 * v, bus); break;
      case 'error': this.tone(180, now, 0.18, 'square', 0.12 * v, bus, { filter: 900 }); break;
      case 'build': this.noiseBurst(now, 0.15, 0.3 * v, 'lowpass', 600, bus); this.tone(220, now, 0.15, 'triangle', 0.2 * v, bus, { slide: 330 }); break;
      case 'upgrade': [523, 659, 784].forEach((f, i) => this.tone(f, now + i * 0.06, 0.14, 'triangle', 0.16 * v, bus)); break;
      case 'sell': this.tone(784, now, 0.08, 'sine', 0.15 * v, bus); this.tone(523, now + 0.07, 0.12, 'sine', 0.15 * v, bus); break;
      case 'shot_ranger': this.noiseBurst(now, 0.07, 0.12 * v, 'highpass', 2500, bus); break;
      case 'shot_ember': this.noiseBurst(now, 0.25, 0.18 * v, 'bandpass', 500, bus, 150); break;
      case 'shot_ballista': this.tone(140, now, 0.12, 'sawtooth', 0.15 * v, bus, { filter: 700, slide: 70 }); this.noiseBurst(now, 0.1, 0.15 * v, 'highpass', 1800, bus); break;
      case 'shot_cannon': this.tone(90, now, 0.25, 'sine', 0.35 * v, bus, { slide: 40 }); this.noiseBurst(now, 0.2, 0.25 * v, 'lowpass', 900, bus); break;
      case 'shot_spore': this.tone(300, now, 0.12, 'sine', 0.12 * v, bus, { slide: 180 }); break;
      case 'zap': this.noiseBurst(now, 0.12, 0.18 * v, 'bandpass', 3200, bus, 1200); this.tone(1200, now, 0.08, 'square', 0.05 * v, bus, { slide: 300, filter: 4000 }); break;
      case 'beam': this.tone(740, now, 0.12, 'sine', 0.08 * v, bus, { slide: 1100 }); break;
      case 'rift': this.tone(300, now, 0.3, 'sine', 0.12 * v, bus, { slide: 900, attack: 0.03 }); break;
      case 'pulse_frost': this.tone(1600, now, 0.3, 'sine', 0.06 * v, bus, { slide: 900 }); this.noiseBurst(now, 0.25, 0.06 * v, 'highpass', 5000, bus); break;
      case 'pulse_sonic': this.tone(220, now, 0.35, 'sine', 0.18 * v, bus, { slide: 110 }); this.tone(440, now, 0.3, 'sine', 0.08 * v, bus); break;
      case 'hit': this.noiseBurst(now, 0.05, 0.08 * v, 'bandpass', 1400, bus); break;
      case 'explosion': this.noiseBurst(now, 0.35, 0.3 * v, 'lowpass', 1200, bus, 120); this.tone(70, now, 0.3, 'sine', 0.25 * v, bus, { slide: 35 }); break;
      case 'kill': this.tone(520, now, 0.08, 'triangle', 0.08 * v, bus, { slide: 260 }); break;
      case 'gold': this.tone(1318, now, 0.08, 'sine', 0.1 * v, bus); this.tone(1760, now + 0.05, 0.1, 'sine', 0.08 * v, bus); break;
      case 'leak': this.tone(160, now, 0.4, 'sawtooth', 0.2 * v, bus, { filter: 800, slide: 80 }); break;
      case 'reaction': this.tone(880, now, 0.12, 'triangle', 0.12 * v, bus, { slide: 1320 }); this.noiseBurst(now, 0.15, 0.08 * v, 'bandpass', 2400, bus); break;
      case 'freeze': this.tone(2000, now, 0.2, 'sine', 0.08 * v, bus, { slide: 2600 }); break;
      case 'interrupt': this.tone(600, now, 0.15, 'square', 0.1 * v, bus, { filter: 2000, slide: 300 }); this.tone(300, now + 0.1, 0.2, 'square', 0.08 * v, bus, { filter: 1500 }); break;
      case 'teleport': this.tone(400, now, 0.25, 'sine', 0.1 * v, bus, { slide: 1600 }); break;
      case 'ability': this.tone(330, now, 0.3, 'sawtooth', 0.1 * v, bus, { filter: 1200, slide: 440 }); break;
      case 'bossWarn':
        for (let i = 0; i < 3; i++) this.tone(i % 2 ? 330 : 440, now + i * 0.22, 0.2, 'sawtooth', 0.14 * v, bus, { filter: 1600 });
        break;
      case 'waveStart': this.tone(392, now, 0.18, 'triangle', 0.18 * v, bus); this.tone(523, now + 0.15, 0.25, 'triangle', 0.18 * v, bus); break;
      case 'waveClear': [523, 659, 784, 1046].forEach((f, i) => this.tone(f, now + i * 0.08, 0.22, 'triangle', 0.14 * v, bus)); break;
      case 'victory': [523, 659, 784, 1046, 784, 1046, 1318].forEach((f, i) => this.tone(f, now + i * 0.13, 0.35, 'triangle', 0.18 * v, bus)); break;
      case 'defeat': [392, 349, 311, 262].forEach((f, i) => this.tone(f, now + i * 0.25, 0.5, 'sawtooth', 0.12 * v, bus, { filter: 1200 })); break;
      case 'shot_meteor': this.tone(160, now, 0.5, 'sawtooth', 0.12 * v, bus, { filter: 700, slide: 600, attack: 0.05 }); this.noiseBurst(now, 0.4, 0.1 * v, 'bandpass', 800, bus, 2400); break;
      case 'gravity': this.tone(110, now, 0.45, 'sine', 0.22 * v, bus, { slide: 55, attack: 0.04 }); this.tone(220, now, 0.4, 'triangle', 0.05 * v, bus, { slide: 70 }); break;
      case 'bossIntro':
        this.tone(55, now, 2.2, 'sawtooth', 0.22 * v, bus, { filter: 300, attack: 0.3 });
        this.tone(82.4, now + 0.1, 2.0, 'sawtooth', 0.14 * v, bus, { filter: 380, attack: 0.4 });
        this.noiseBurst(now, 1.6, 0.18 * v, 'lowpass', 400, bus, 80);
        [0, 0.45, 0.9].forEach((d) => this.tone(65, now + d, 0.35, 'sine', 0.4 * v, bus, { slide: 35 }));
        break;
      case 'heroDeploy': this.tone(523, now, 0.3, 'triangle', 0.16 * v, bus, { slide: 1046, attack: 0.02 }); this.noiseBurst(now, 0.4, 0.08 * v, 'highpass', 3000, bus); [784, 1046].forEach((f, i) => this.tone(f, now + 0.18 + i * 0.08, 0.3, 'sine', 0.1 * v, bus)); break;
      case 'heroDown': [440, 370, 294].forEach((f, i) => this.tone(f, now + i * 0.16, 0.4, 'triangle', 0.14 * v, bus, { filter: 1600 })); break;
      case 'heroAbility': this.tone(392, now, 0.25, 'sawtooth', 0.1 * v, bus, { filter: 2400, slide: 784 }); this.noiseBurst(now, 0.25, 0.1 * v, 'bandpass', 1800, bus, 600); break;
      case 'levelUp': [523, 659, 784, 1046, 1318].forEach((f, i) => this.tone(f, now + i * 0.07, 0.3, 'triangle', 0.13 * v, bus)); break;
      case 'fusion':
        [261, 392, 523, 659, 784, 1046].forEach((f, i) => this.tone(f, now + i * 0.09, 0.9, 'triangle', 0.12 * v, bus, { attack: 0.03 }));
        this.noiseBurst(now, 1.2, 0.08 * v, 'highpass', 4000, bus, 9000);
        break;
      case 'towerActive': this.tone(330, now, 0.35, 'square', 0.08 * v, bus, { filter: 2400, slide: 990 }); this.tone(660, now + 0.1, 0.3, 'triangle', 0.1 * v, bus); break;
      case 'shatter': this.noiseBurst(now, 0.5, 0.25 * v, 'highpass', 2500, bus, 6000); [1760, 1480, 2093].forEach((f, i) => this.tone(f, now + i * 0.05, 0.3, 'sine', 0.07 * v, bus)); break;
      case 'devour': this.tone(90, now, 0.8, 'sawtooth', 0.2 * v, bus, { filter: 500, slide: 45 }); this.noiseBurst(now, 0.6, 0.15 * v, 'lowpass', 600, bus, 100); break;
      case 'heal': this.tone(660, now, 0.4, 'sine', 0.07 * v, bus, { slide: 990, attack: 0.05 }); break;
      case 'achievement': [784, 988, 1175, 1568].forEach((f, i) => this.tone(f, now + i * 0.1, 0.5, 'triangle', 0.13 * v, bus)); break;
    }
  }

  // ------------------------------------------------------------ Musik (generativ)

  setMusicTheme(root: number, scale: number[]): void {
    this.root = root;
    this.scale = scale;
  }

  startMusic(): void {
    if (!this.ctx || this.musicTimer !== null) return;
    this.nextBeat = this.ctx.currentTime + 0.1;
    this.musicTimer = window.setInterval(() => this.scheduleMusic(), 100);
  }

  stopMusic(): void {
    if (this.musicTimer !== null) {
      window.clearInterval(this.musicTimer);
      this.musicTimer = null;
    }
  }

  private midi(n: number): number {
    return 440 * Math.pow(2, (n - 69) / 12);
  }

  private deg(d: number, oct = 0): number {
    const s = this.scale;
    const o = Math.floor(d / s.length);
    const i = ((d % s.length) + s.length) % s.length;
    return this.root + s[i] + 12 * (o + oct);
  }

  private scheduleMusic(): void {
    const ctx = this.ctx;
    const bus = this.musicBus;
    if (!ctx || !bus) return;
    if (this.settings.muted || this.settings.music <= 0.001) {
      this.nextBeat = ctx.currentTime + 0.1;
      return;
    }
    const bpm = this.intensity >= 2 ? 104 : this.intensity >= 1 ? 92 : 78;
    const spb = 60 / bpm;
    while (this.nextBeat < ctx.currentTime + 0.3) {
      const t = this.nextBeat;
      const b = this.beat;
      const bar = Math.floor(b / 8) % 4;
      const prog = [0, 3, 4, 2][bar];
      if (b % 8 === 0) {
        // Flächenklang
        for (const k of [0, 2, 4]) this.tone(this.midi(this.deg(prog + k, -1)), t, spb * 8, 'triangle', 0.05, bus, { attack: 0.6, filter: 1200 });
        this.tone(this.midi(this.deg(prog, -2)), t, spb * 4, 'sine', 0.12, bus, { attack: 0.05 });
      }
      if (b % 8 === 4) this.tone(this.midi(this.deg(prog, -2)), t, spb * 4, 'sine', 0.1, bus, { attack: 0.05 });
      if (this.intensity >= 1 && b % 2 === 0) {
        const step = [0, 2, 4, 2, 1, 3, 4, 5][b % 8];
        this.tone(this.midi(this.deg(prog + step, 0)), t, spb * 0.9, 'square', 0.025, bus, { filter: 1800 });
      } else if (this.intensity === 0 && b % 4 === 2) {
        const step = [4, 2, 5, 3][bar];
        this.tone(this.midi(this.deg(prog + step, 1)), t, spb * 2, 'sine', 0.03, bus, { attack: 0.05 });
      }
      if (this.intensity >= 1) {
        if (b % 4 === 0) this.tone(55, t, 0.18, 'sine', 0.2, bus, { slide: 40 });
        if (b % 2 === 1) this.noiseBurst(t, 0.05, 0.03, 'highpass', 6000, bus);
      }
      if (this.intensity >= 2 && b % 8 === 0) this.tone(this.midi(this.deg(prog, -3)), t, spb * 8, 'sawtooth', 0.04, bus, { filter: 400, attack: 1 });
      if (this.intensity >= 2) {
        // Kriegstrommeln im Bosskampf
        if (b % 4 === 0 || b % 8 === 3) this.tone(48, t, 0.3, 'sine', 0.26, bus, { slide: 30 });
        if (b % 4 === 2) this.noiseBurst(t, 0.12, 0.07, 'bandpass', 900, bus);
      }
      // Arpeggio-Glitzer mit Echo (leise, in allen Phasen)
      if (this.delay && b % 4 === 1 && (bar + Math.floor(b / 32)) % 2 === 0) {
        const step = [0, 2, 4, 6][(b >> 2) % 4];
        this.tone(this.midi(this.deg(prog + step, 1)), t, spb * 0.8, 'sine', 0.025, this.delay, { attack: 0.01 });
      }
      this.beat++;
      this.nextBeat += spb / 2;
    }
  }
}

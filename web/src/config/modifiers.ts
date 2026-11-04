import type { DamageType, ModifierId, RunModifier } from '../core/types.ts';

// Roguelite-Modifikatoren. Jeder Eintrag besitzt einen konkret implementierten Effekt,
// der über aggregateModifiers() von den Systemen gelesen wird.

export const MODIFIERS: Record<ModifierId, RunModifier> = {
  chainReaction: { id: 'chainReaction', icon: '⚡', name: 'Kettenreaktion', tags: ['lightning'], requiresTower: ['storm', 'pylon'],
    description: 'Blitzangriffe springen auf 2 zusätzliche Ziele über.' },
  wildfire: { id: 'wildfire', icon: '🔥', name: 'Flächenbrand', tags: ['fire', 'tradeoff'], requiresTower: ['ember'],
    description: 'Feuerschaden +40 %, aber alle Türme −10 % Reichweite.' },
  bounty: { id: 'bounty', icon: '💰', name: 'Kopfgeldjagd', tags: ['economy', 'tradeoff'],
    description: 'Abschüsse geben +30 % Gold, Bosse haben jedoch +25 % Lebenspunkte.' },
  netDischarge: { id: 'netDischarge', icon: '🕸', name: 'Netzentladung', tags: ['network'], requiresTower: ['pylon'],
    description: 'Netzwerkverbindungen fügen Gegnern, die sie kreuzen, jede Sekunde Blitzschaden zu.' },
  eternalIce: { id: 'eternalIce', icon: '❄', name: 'Ewiges Eis', tags: ['frost', 'tradeoff'], requiresTower: ['frost'],
    description: 'Kälte und Einfrieren halten 60 % länger, Frostschaden −40 %.' },
  goldRush: { id: 'goldRush', icon: '⛏', name: 'Goldrausch', tags: ['economy', 'tradeoff'], requiresTower: ['goldmine'],
    description: 'Goldgräber fördern +50 % Gold, kosten aber +40 % mehr.' },
  toxicMist: { id: 'toxicMist', icon: '☠', name: 'Giftnebel', tags: ['poison'], requiresTower: ['spore'],
    description: 'Gift: +3 maximale Stapel und +30 % Dauer.' },
  sharpBlades: { id: 'sharpBlades', icon: '🗡', name: 'Geschärfte Klingen', tags: ['physical'],
    description: 'Physischer Schaden +20 %.' },
  arcaneEcho: { id: 'arcaneEcho', icon: '✦', name: 'Arkanes Echo', tags: ['arcane'], requiresTower: ['rune'],
    description: 'Runenmarkierungen senken Resistenzen um weitere 10 %.' },
  rapidFire: { id: 'rapidFire', icon: '⏩', name: 'Schnellfeuer', tags: ['tradeoff'],
    description: 'Alle Türme +12 % Angriffstempo, Baukosten +10 %.' },
  glassCannon: { id: 'glassCannon', icon: '💥', name: 'Glaskanone', tags: ['tradeoff'], exclusive: ['bulwark'],
    description: 'Gesamter Schaden +25 %, sofort −5 Kernenergie.' },
  bulwark: { id: 'bulwark', icon: '🏰', name: 'Bollwerk', tags: ['defense', 'tradeoff'], exclusive: ['glassCannon'],
    description: '+8 Kernenergie (auch Maximum), Wellenbonus −10 %.' },
  farSight: { id: 'farSight', icon: '🔭', name: 'Weitblick', tags: ['range'], exclusive: ['wildfire'],
    description: 'Alle Türme +12 % Reichweite.' },
  resonanceAmp: { id: 'resonanceAmp', icon: '📡', name: 'Resonanzverstärker', tags: ['network'], requiresTower: ['pylon'],
    description: 'Netzwerkbonus +50 %.' },
  interest: { id: 'interest', icon: '🏦', name: 'Zinsen', tags: ['economy'],
    description: 'Am Wellenende +5 % des aktuellen Goldes (max. 40).' },
  heroism: { id: 'heroism', icon: '🛡', name: 'Heldenmut', tags: ['hero', 'tradeoff'], requiresHeroes: true,
    description: 'Abklingzeit der Helden −30 %, aber alle Türme −5 % Schaden.' },
  riftTap: { id: 'riftTap', icon: '⚡', name: 'Rissanzapfung', tags: ['hero'], requiresHeroes: true,
    description: 'Elementarreaktionen liefern doppelte Rissenergie.' },
};

export const MODIFIER_IDS = Object.keys(MODIFIERS) as ModifierId[];

export interface ModAggregate {
  dmgType: Record<DamageType, number>;
  allDamage: number;
  rangeMul: number;
  attackSpeedMul: number;
  chainsAdd: number;
  chillDurMul: number;
  killGoldMul: number;
  bossHpMul: number;
  incomeMul: number;
  mineCostMul: number;
  towerCostMul: number;
  poisonStacksAdd: number;
  poisonDurMul: number;
  markAdd: number;
  networkBonusMul: number;
  netDischarge: boolean;
  waveGoldMul: number;
  interest: boolean;
  heroCooldownMul: number;
  reactionEnergyMul: number;
}

export function aggregateModifiers(ids: readonly ModifierId[]): ModAggregate {
  const a: ModAggregate = {
    dmgType: { physical: 1, fire: 1, frost: 1, lightning: 1, poison: 1, arcane: 1, sonic: 1 },
    allDamage: 1, rangeMul: 1, attackSpeedMul: 1, chainsAdd: 0, chillDurMul: 1, killGoldMul: 1,
    bossHpMul: 1, incomeMul: 1, mineCostMul: 1, towerCostMul: 1, poisonStacksAdd: 0, poisonDurMul: 1,
    markAdd: 0, networkBonusMul: 1, netDischarge: false, waveGoldMul: 1, interest: false,
    heroCooldownMul: 1, reactionEnergyMul: 1,
  };
  for (const id of ids) {
    switch (id) {
      case 'chainReaction': a.chainsAdd += 2; break;
      case 'wildfire': a.dmgType.fire *= 1.4; a.rangeMul *= 0.9; break;
      case 'bounty': a.killGoldMul *= 1.3; a.bossHpMul *= 1.25; break;
      case 'netDischarge': a.netDischarge = true; break;
      case 'eternalIce': a.chillDurMul *= 1.6; a.dmgType.frost *= 0.6; break;
      case 'goldRush': a.incomeMul *= 1.5; a.mineCostMul *= 1.4; break;
      case 'toxicMist': a.poisonStacksAdd += 3; a.poisonDurMul *= 1.3; break;
      case 'sharpBlades': a.dmgType.physical *= 1.2; break;
      case 'arcaneEcho': a.markAdd += 0.1; break;
      case 'rapidFire': a.attackSpeedMul *= 1.12; a.towerCostMul *= 1.1; break;
      case 'glassCannon': a.allDamage *= 1.25; break;
      case 'bulwark': a.waveGoldMul *= 0.9; break;
      case 'farSight': a.rangeMul *= 1.12; break;
      case 'resonanceAmp': a.networkBonusMul *= 1.5; break;
      case 'interest': a.interest = true; break;
      case 'heroism': a.heroCooldownMul *= 0.7; a.allDamage *= 0.95; break;
      case 'riftTap': a.reactionEnergyMul *= 2; break;
    }
  }
  return a;
}

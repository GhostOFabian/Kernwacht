// Eigene, schlichte SVG-Symbole für Bedienelemente (ersetzen Emojis in der Spieloberfläche).
// Alle Symbole nutzen currentColor und skalieren mit der Schriftgröße.

const P: Record<string, string> = {
  menu: '<path d="M4 6h16M4 12h16M4 18h16"/>',
  pause: '<path d="M8 5v14M16 5v14" stroke-width="3.2"/>',
  play: '<path d="M7 4.5v15l12-7.5z" fill="currentColor" stroke="none"/>',
  speed: '<path d="M4 6l7 6-7 6zM12 6l7 6-7 6z" fill="currentColor" stroke="none"/>',
  coin: '<circle cx="12" cy="12" r="8.5" fill="currentColor" stroke="none"/><circle cx="12" cy="12" r="5.6" fill="none" stroke="#7a5a10" stroke-width="1.6"/><path d="M12 8.6v6.8" stroke="#7a5a10" stroke-width="1.8"/>',
  heart: '<path d="M12 20s-7.5-4.6-7.5-10A4.3 4.3 0 0 1 12 7.4 4.3 4.3 0 0 1 19.5 10c0 5.4-7.5 10-7.5 10z" fill="currentColor" stroke="none"/>',
  wave: '<path d="M2.5 10c2.4-3 4.8-3 7.2 0s4.8 3 7.2 0 3.4-2 4.6-1M2.5 16c2.4-3 4.8-3 7.2 0s4.8 3 7.2 0 3.4-2 4.6-1"/>',
  energy: '<path d="M13.5 2.5L5 13.5h6l-1.5 8 8.5-11h-6z" fill="currentColor" stroke="none"/>',
  rotL: '<path d="M4.5 12a7.5 7.5 0 1 0 2.2-5.3"/><path d="M4 3.5v4.5h4.5"/>',
  rotR: '<path d="M19.5 12a7.5 7.5 0 1 1-2.2-5.3"/><path d="M20 3.5v4.5h-4.5"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  minus: '<path d="M5 12h14"/>',
  fit: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>',
  range: '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="2.5" fill="currentColor"/>',
  help: '<circle cx="12" cy="12" r="9"/><path d="M9.3 9.4a2.8 2.8 0 0 1 5.4 1c0 1.9-2.7 2.3-2.7 4"/><circle cx="12" cy="17.6" r="0.6" fill="currentColor"/>',
  close: '<path d="M6 6l12 12M18 6L6 18"/>',
  map: '<path d="M3.5 6.5l5.5-2 6 2 5.5-2v13l-5.5 2-6-2-5.5 2z"/><path d="M9 4.5v13M15 6.5v13"/>',
  sword: '<path d="M14.5 3.5h6v6L10 20l-3-3z"/><path d="M5 15l4 4M3.5 20.5l2.5-2.5"/>',
  shield: '<path d="M12 3l7.5 3v5.5c0 4.5-3.2 8-7.5 9.5-4.3-1.5-7.5-5-7.5-9.5V6z"/>',
  hold: '<path d="M12 3l7.5 3v5.5c0 4.5-3.2 8-7.5 9.5-4.3-1.5-7.5-5-7.5-9.5V6z"/><path d="M8.5 12h7"/>',
  advance: '<path d="M5 12h12M12 6l6 6-6 6"/>',
  retreat: '<path d="M19 12H7M12 6l-6 6 6 6"/>',
  star: '<path d="M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z" fill="currentColor" stroke="none"/>',
  fuse: '<circle cx="8" cy="12" r="4.5"/><circle cx="16" cy="12" r="4.5"/><path d="M12 8.6v6.8"/>',
  bolt: '<path d="M13.5 2.5L5 13.5h6l-1.5 8 8.5-11h-6z"/>',
  up: '<path d="M12 19V6M6 11l6-6 6 6"/>',
  coins: '<ellipse cx="12" cy="7" rx="7" ry="3"/><path d="M5 7v5c0 1.7 3.1 3 7 3s7-1.3 7-3V7M5 12v5c0 1.7 3.1 3 7 3s7-1.3 7-3v-5"/>',
  trophy: '<path d="M8 4h8v5a4 4 0 0 1-8 0zM8 6H4.5a3 3 0 0 0 3.5 4M16 6h3.5a3 3 0 0 1-3.5 4M12 13v4M8.5 20h7M10 17h4"/>',
  skull: '<path d="M12 3a7.5 7.5 0 0 0-7.5 7.5c0 2.6 1.3 4.4 3 5.4V19h9v-3.1c1.7-1 3-2.8 3-5.4A7.5 7.5 0 0 0 12 3z"/><circle cx="9" cy="11" r="1.6" fill="currentColor"/><circle cx="15" cy="11" r="1.6" fill="currentColor"/>',
  crown: '<path d="M3.5 8l4.2 4 4.3-7 4.3 7 4.2-4-2 11h-13z"/>',
  gear: '<circle cx="12" cy="12" r="3.2"/><path d="M12 2.8v3M12 18.2v3M2.8 12h3M18.2 12h3M5.5 5.5l2.1 2.1M16.4 16.4l2.1 2.1M5.5 18.5l2.1-2.1M16.4 7.6l2.1-2.1"/>',
  book: '<path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5zM4 20.5A2.5 2.5 0 0 0 6.5 21H20"/>',
  scroll: '<path d="M7 3h11a2 2 0 0 1 2 2v2h-4M7 3a2 2 0 0 0-2 2v12M16 7v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-2h11"/>',
  infinity: '<path d="M6.5 8.5C3 8.5 3 15.5 6.5 15.5 10 15.5 14 8.5 17.5 8.5 21 8.5 21 15.5 17.5 15.5 14 15.5 10 8.5 6.5 8.5z"/>',
  calendar: '<rect x="3.5" y="5" width="17" height="15" rx="2"/><path d="M3.5 10h17M8 3v4M16 3v4"/>',
  pen: '<path d="M4 20l4.5-1L19 8.5 15.5 5 5 15.5z"/><path d="M13.5 7l3.5 3.5"/>',
  dice: '<rect x="4" y="4" width="16" height="16" rx="3.5"/><circle cx="9" cy="9" r="1.2" fill="currentColor"/><circle cx="15" cy="15" r="1.2" fill="currentColor"/><circle cx="15" cy="9" r="1.2" fill="currentColor"/><circle cx="9" cy="15" r="1.2" fill="currentColor"/>',
  copy: '<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V5.5A1.5 1.5 0 0 0 14.5 4h-9A1.5 1.5 0 0 0 4 5.5v9A1.5 1.5 0 0 0 5.5 16H8"/>',
  back: '<path d="M19 12H6M11 6l-6 6 6 6"/>',
  lock: '<rect x="5" y="10.5" width="14" height="10" rx="2"/><path d="M8 10.5V8a4 4 0 0 1 8 0v2.5"/>',
  resume: '<path d="M6 4.5v15M10 4.5v15l10-7.5z"/>',
  minimap: '<rect x="3.5" y="3.5" width="17" height="17" rx="2"/><path d="M3.5 14l5-4 4 3 3-2.5 5 4"/>',
};

export function svgIcon(name: string, cls = 'svg-ic'): SVGSVGElement {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('class', cls);
  svg.setAttribute('aria-hidden', 'true');
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '2');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.innerHTML = P[name] ?? P.help;
  return svg;
}

import type { GridPos, MapDefinition } from '../core/types.ts';
import type { IsoCamera } from './Camera.ts';
import { cellTop } from './terrain.ts';

/**
 * Bildschirmpunkt → Feld. Exakter Schnitt des Sichtstrahls mit dem Höhenfeld (inkl. Klippenflächen): Der Strahl
 * wird von oberhalb der höchsten Stufe bis unter die Wege abgetastet; das erste Feld, dessen Oberfläche der Strahl
 * erreicht, ist das sichtbare. Das frühere Näherungsverfahren lieferte an Kanten zwischen hohen und tiefen Feldern
 * oft das falsche Feld – tiefe Felder ließen sich dann manchmal nicht bebauen.
 */
export function pickGridCell(cam: IsoCamera, map: MapDefinition, px: number, py: number): GridPos | null {
  const top = 0.75;
  const bottom = -0.06;
  const a = cam.screenToGround(px, py, top);
  const b = cam.screenToGround(px, py, bottom);
  if (a && b) {
    const steps = 320;
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      const y = top + (bottom - top) * t;
      const cx = Math.floor(a.x + (b.x - a.x) * t);
      const cz = Math.floor(a.z + (b.z - a.z) * t);
      if (cx < 0 || cz < 0 || cx >= map.width || cz >= map.height) continue;
      if (y <= cellTop(map, cx, cz)) return { x: cx, y: cz };
    }
  }
  // Rückfall: Schnitt mit der Grundebene (z. B. Klick neben die Insel)
  const hit = cam.screenToGround(px, py, 0);
  if (!hit) return null;
  const x = Math.floor(hit.x);
  const y = Math.floor(hit.z);
  if (x < 0 || y < 0 || x >= map.width || y >= map.height) return null;
  return { x, y };
}

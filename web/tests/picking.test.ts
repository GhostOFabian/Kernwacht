import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { MapDefinition } from '../src/core/types.ts';
import { IsoCamera } from '../src/render/Camera.ts';
import { pickGridCell } from '../src/render/picking.ts';
import { cellTop } from '../src/render/terrain.ts';
import { makeSim } from './helpers.ts';

/** Referenz: sehr fein abgetasteter Sichtstrahl gegen das Höhenfeld. */
function truth(cam: IsoCamera, map: MapDefinition, px: number, py: number) {
  const a = cam.screenToGround(px, py, 1.0);
  const b = cam.screenToGround(px, py, -0.1);
  if (!a || !b) return null;
  for (let i = 0; i <= 4000; i++) {
    const t = i / 4000;
    const y = 1.0 - 1.1 * t;
    const cx = Math.floor(a.x + (b.x - a.x) * t);
    const cz = Math.floor(a.z + (b.z - a.z) * t);
    if (cx < 0 || cz < 0 || cx >= map.width || cz >= map.height) continue;
    if (y <= cellTop(map, cx, cz)) return { x: cx, y: cz };
  }
  return null;
}

test('Feld-Picking trifft das sichtbare Feld – auch tiefe Felder neben Anhöhen und Kanten (alle Drehungen)', () => {
  let total = 0;
  let wrong = 0;
  for (const seed of ['PICK-A', 'PICK-B', 'PICK-C']) {
    const map = makeSim({ seedText: seed, size: 'large' }).s.map;
    for (const rot of [0, 1, 2, 3]) {
      const cam = new IsoCamera();
      cam.resize(1280, 760);
      cam.setMap(map.width, map.height);
      cam.rot = rot;
      cam.zoom = 7;
      for (let k = 0; k < 120; k++) cam.update(0.05);
      for (let i = 0; i < 2500; i++) {
        const px = 40 + ((i * 7919) % 1200);
        const py = 40 + ((i * 104729) % 680);
        const tr = truth(cam, map, px, py);
        if (!tr) continue;
        total++;
        const h = pickGridCell(cam, map, px, py);
        if (!h || h.x !== tr.x || h.y !== tr.y) wrong++;
      }
    }
  }
  assert.ok(total > 10000);
  assert.ok(wrong / total < 0.01, `Fehlerquote ${(100 * wrong / total).toFixed(2)} % (${wrong}/${total})`);
});

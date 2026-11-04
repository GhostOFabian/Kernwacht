import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFileSync } from 'node:fs';
import { BIOME_ORDER } from '../src/config/biomes.ts';
import { MAT, SHAPE } from '../src/render/Renderer.ts';
import { MOOD } from '../src/render/WorldView.ts';

test('3.13: Stimmung für jedes Biom, Höhle ohne Wolkenschatten aber mit Sternen', () => {
  for (const b of BIOME_ORDER) {
    const m = MOOD[b];
    assert.ok(m, b);
    assert.ok(m.clouds >= 0 && m.clouds <= 0.5, `${b}: Wolkenschatten dezent`);
    assert.ok(m.sun >= 0 && m.sun <= 0.15, `${b}: Sonnendunst dezent (Glühen bleibt reduziert)`);
  }
  assert.equal(MOOD.cavern.clouds, 0);
  assert.ok(MOOD.cavern.stars > 0.5);
});

test('3.13: neues Geländematerial und Wirbel-Sprite; Shader enthält Tilt-Shift, Uferschaum, Wolkenschatten', () => {
  assert.equal(MAT.terrain, 5);
  assert.equal(SHAPE.vortex, 7);
  const src = readFileSync(new URL('../src/render/Renderer.ts', import.meta.url), 'utf8');
  for (const k of ['u_dof', 'u_island', 'u_clouds', 'u_fogTop', 'SKY_FS', 'u_split']) assert.ok(src.includes(k), k);
  const terrain = readFileSync(new URL('../src/render/terrain.ts', import.meta.url), 'utf8');
  assert.ok((terrain.match(/MAT\.terrain/g) ?? []).length >= 8, 'Gelände nutzt das Geländematerial');
});

test('3.13: Glas-Oberfläche im Stylesheet, Animationen respektieren „Bewegung reduzieren“', () => {
  const css = readFileSync(new URL('../src/styles.css', import.meta.url), 'utf8');
  assert.match(css, /backdrop-filter: blur\(14px\)/);
  assert.match(css, /@keyframes sheen/);
  assert.match(css, /\.reduced-motion \.menu::before/);
});

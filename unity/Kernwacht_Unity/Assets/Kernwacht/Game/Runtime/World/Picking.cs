// Übertragung von render/picking.ts: Bildschirmpunkt → Feld.
using Kernwacht.Sim;
using UnityEngine;

namespace Kernwacht.Game
{
    public static class Picking
    {
        /// <summary>
        /// Bildschirmpunkt (Pixel, Ursprung oben links) → Feld. Exakter Schnitt des Sichtstrahls mit dem Höhenfeld (inkl.
        /// Klippenflächen): Der Strahl wird von oberhalb der höchsten Stufe bis unter die Wege abgetastet; das erste Feld,
        /// dessen Oberfläche der Strahl erreicht, ist das sichtbare.
        /// </summary>
        public static GridPos pickGridCell(IsoCamera cam, MapDefinition map, double px, double py)
        {
            const float top = 0.75f;
            const float bottom = -0.06f;
            var sp = new Vector2((float)px, (float)py);
            var a = cam.ScreenToGround(sp, top);
            var b = cam.ScreenToGround(sp, bottom);
            int w = (int)map.width, h = (int)map.height;
            if (a.HasValue && b.HasValue)
            {
                const int steps = 320;
                var A = a.Value;
                var B = b.Value;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float y = top + (bottom - top) * t;
                    int cx = Mathf.FloorToInt(A.x + (B.x - A.x) * t);
                    int cz = Mathf.FloorToInt(A.z + (B.z - A.z) * t);
                    if (cx < 0 || cz < 0 || cx >= w || cz >= h) continue;
                    if (y <= Terrain.cellTop(map, cx, cz)) return new GridPos(cx, cz);
                }
            }
            // Rückfall: Schnitt mit der Grundebene (z. B. Klick neben die Insel)
            var hit = cam.ScreenToGround(sp, 0);
            if (!hit.HasValue) return null;
            int x = Mathf.FloorToInt(hit.Value.x);
            int z = Mathf.FloorToInt(hit.Value.z);
            if (x < 0 || z < 0 || x >= w || z >= h) return null;
            return new GridPos(x, z);
        }
    }
}

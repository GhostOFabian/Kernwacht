// Übertragung von render/palette.ts: Schadensfarben, Schadensnamen, Status-Symbole im 3D-Raum.
using System.Collections.Generic;

namespace Kernwacht.Game
{
    /// <summary>Status-Symbol im 3D-Raum: Form + Farbe (nie nur Farbe). sy &lt; 0 = umgedreht.</summary>
    public class StatusGlyph
    {
        public string mesh;   // MeshId
        public double color;
        public double sy;
        public StatusGlyph(string mesh, double color, double sy) { this.mesh = mesh; this.color = color; this.sy = sy; }
    }

    public static class Palette
    {
        public static readonly Dictionary<string, double> DAMAGE_COLORS = new Dictionary<string, double>
        {
            ["physical"] = 0xe8dcc8,
            ["fire"] = 0xff7030,
            ["frost"] = 0x90d8ff,
            ["lightning"] = 0xffe860,
            ["poison"] = 0x9ae040,
            ["arcane"] = 0xc070ff,
            ["sonic"] = 0x60e8d4,
        };

        public static readonly Dictionary<string, string> DAMAGE_NAMES = new Dictionary<string, string>
        {
            ["physical"] = "Physisch", ["fire"] = "Feuer", ["frost"] = "Frost", ["lightning"] = "Blitz", ["poison"] = "Gift", ["arcane"] = "Arkan", ["sonic"] = "Schall",
        };

        /// <summary>Status-Symbole im 3D-Raum: Form + Farbe (nie nur Farbe).</summary>
        public static readonly Dictionary<string, StatusGlyph> STATUS_GLYPH = new Dictionary<string, StatusGlyph>
        {
            ["burn"] = new StatusGlyph("cone6", 0xff7030, 1.4),
            ["chill"] = new StatusGlyph("octa", 0x90d8ff, 1),
            ["freeze"] = new StatusGlyph("box", 0xe8fbff, 1),
            ["wet"] = new StatusGlyph("sphereLo", 0x4080ff, 1.2),
            ["poison"] = new StatusGlyph("sphere", 0x9ae040, 1),
            ["mark"] = new StatusGlyph("octa", 0xc070ff, 1.6),
            ["brittle"] = new StatusGlyph("cone4", 0xe0e0e0, 1),
            ["silence"] = new StatusGlyph("ringThin", 0x60e8d4, 1),
            ["riftMark"] = new StatusGlyph("torus", 0xff60ff, 1),
            ["stun"] = new StatusGlyph("cone4", 0xffff80, -1),
            ["root"] = new StatusGlyph("box", 0xe060e0, 0.5),
            ["tether"] = new StatusGlyph("torus", 0xff9aff, 1),
            ["reveal"] = new StatusGlyph("ring", 0xfff080, 1),
            ["voidMark"] = new StatusGlyph("gem", 0x8a40ff, 1),
            ["shred"] = new StatusGlyph("cone4", 0xd03030, -1),
            ["pull"] = new StatusGlyph("torus", 0xa080ff, 0.6),
        };

        /// <summary>Schadensfarbe als Ganzzahl (0xRRGGBB); unbekannt = weiß.</summary>
        public static int damageColor(string type)
        {
            return type != null && DAMAGE_COLORS.TryGetValue(type, out var c) ? (int)c : 0xffffff;
        }
    }
}

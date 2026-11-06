// 4.1: Optik-Themen. "fantasy" ist das Original, "scifi" tauscht Namen, Texte, Modelle (sf_-Varianten),
// Biom-Optik, Effekte und Klänge aus. Die Simulation bleibt unverändert (gleiche Werte, gleiche Regeln):
// Themes.Apply ändert nur Anzeigetexte und Farbtabellen, die Spiellogik liest davon nichts.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kernwacht.Game.UI;
using Kernwacht.Sim;

namespace Kernwacht.Game
{
    public static class Themes
    {
        public const string FANTASY = "fantasy", SCIFI = "scifi";
        static string current = FANTASY;
        public static string Current => current;
        public static bool SciFi => current == SCIFI;

        // ------------------------------------------------------------ Modelle

        static readonly Dictionary<string, string> modelMap = new Dictionary<string, string>();

        /// <summary>Modellkennung im aktuellen Thema: im Sci-Fi-Thema "sf_"+id, wenn das Modell existiert.</summary>
        public static string Model(string id)
        {
            if (!SciFi || string.IsNullOrEmpty(id) || id.StartsWith("sf_")) return id;
            if (modelMap.TryGetValue(id, out var m)) return m;
            var cat = ModelPool.Catalog;
            string sf = "sf_" + id;
            m = cat != null && cat.Prefab(sf) != null ? sf : id;
            if (cat != null) modelMap[id] = m;
            return m;
        }

        // ------------------------------------------------------------ Texte

        public static readonly Dictionary<string, string> DMG_SCIFI = new Dictionary<string, string>
        {
            ["physical"] = "Kinetisch", ["fire"] = "Plasma", ["frost"] = "Kryo", ["lightning"] = "Elektro", ["poison"] = "Toxin", ["arcane"] = "Psi", ["sonic"] = "Schall",
        };

        /// <summary>Turm → (Name, Beschreibung oder null = Fantasy-Text mit Wortersetzung).</summary>
        static readonly Dictionary<string, string[]> TOWER = new Dictionary<string, string[]>
        {
            ["ranger"] = new[] { "Pulsgeschütz", "Günstiges Schnellfeuergeschütz mit Pulsladungen. Trifft Boden- und Luftziele." },
            ["ember"] = new[] { "Plasmaspeier", "Schleudert Plasmakugeln, die beim Aufprall explodieren und Ziele in Brand setzen." },
            ["frost"] = new[] { "Kryoemitter", "Stößt Kryopulse aus, die alle Gegner in Reichweite verlangsamen." },
            ["storm"] = new[] { "Lichtbogenmast", "Entlädt Lichtbögen, die zwischen mehreren Gegnern überspringen." },
            ["spore"] = new[] { "Toxinwerfer", "Verschießt Nanotoxin-Kapseln, die stapelbares Gift auftragen. Gift unterbindet Regeneration." },
            ["ballista"] = new[] { "Railgun", "Beschleunigt Wolframgeschosse magnetisch auf große Reichweite – sie ignorieren Panzerung teilweise." },
            ["rune"] = new[] { "Psi-Emitter", "Psi-Strahlen markieren Gegner und senken alle ihre Resistenzen." },
            ["resonator"] = new[] { "Schallkanone", "Gebündelte Schallwellen unterbrechen gegnerische Fähigkeiten und erschüttern die Standfestigkeit von Bossen." },
            ["goldmine"] = new[] { "Erzförderer", "Fördert nach jeder überstandenen Welle Gold. Auf Erzadern +50 %. Zahlt erst ab der Welle nach dem Bau." },
            ["pylon"] = new[] { "Relaisknoten", "Verbindet Türme in Reichweite zu einem Datennetz: verbundene Türme erhalten Schaden und Tempo. Kombinationen lösen Resonanzen aus." },
            ["cannon"] = new[] { "Massetreiber", "Feuert Masseladungen mit großem Explosionsradius." },
            ["riftbinder"] = new[] { "Phasenprojektor", "Setzt Phasenmarken. Nach kurzer Zeit wird das Ziel per Phasensprung auf dem Weg zurückversetzt." },
            ["meteor"] = new[] { "Orbital-Uplink", "Fordert nach kurzer Zielerfassung einen Orbitalschlag an, der eine große Fläche verwüstet und in Brand setzt." },
            ["tesla"] = new[] { "Tesla-Generator", "Zappt nahe Gegner und spannt einen dauerhaften Lichtbogen zu anderen Tesla-Generatoren in Reichweite. Wer den Bogen kreuzt, wird gegrillt." },
            ["gravity"] = new[] { "Gravitonmaschine", "Erzeugt Gravitonwellen, die vorauslaufende Gegner zurückziehen und verlangsamen – die Gegner ballen sich zusammen." },
            ["dragon"] = new[] { "Drohnenhangar", "Eine Kampfdrohne kreist über dem Hangar und feuert Plasma auf Boden- und Luftziele. Greift Flieger zuerst an und verursacht an ihnen 2,5-fachen Schaden." },
            ["laser"] = new[] { "Laserbatterie", "Ein Laserstrahl, der sich mit der Zeit auflädt und alle Gegner in seiner Linie durchdringt. Mehrere Laser dicht beieinander stören sich (−12 % je Nachbarlaser in 3 Feldern), direkt am Portal −35 %." },
            ["totem"] = new[] { "Feldverstärker", "Verstärkt alle Türme in seinem Feld und deckt getarnte Gegner auf. Greift nicht selbst an." },
            ["minelayer"] = new[] { "Minenleger", "Verlegt im Umkreis Haftminen mitten auf die Lane. Sie bleiben liegen (auch über das Wellenende hinaus) und explodieren, sobald ein Bodengegner darauf tritt – Flieger lösen sie nicht aus." },
            ["skyguard"] = new[] { "Flak-Phalanx", "Reine Flugabwehr: Zwillingsgeschütze feuern schnelle Netzgranaten auf Flieger – verlangsamt sie und trifft hart. Bodengegner ignoriert sie vollständig." },
            ["barricade"] = new[] { "Energiebarriere", "Günstige Energiewand zum Bau von Labyrinthen. Greift nicht an. Darf den Weg nie vollständig blockieren." },
            ["lanebarricade"] = new[] { "Sperrfeld", "Wird direkt auf die Lane gesetzt (nicht direkt vor ein Portal). Hält Bodengegner auf, bis sie insgesamt 5 s lang blockiert wurden – dann bricht das Feld zusammen. Bosse werden nur um 50 % verlangsamt. Kostenlos, aber höchstens 2 Sperrfelder je 10 Minuten Spielzeit." },
            ["seal"] = new[] { "Störsender", "Ein Störstrahl trifft bevorzugt Gegner, die gerade eine Fähigkeit vorbereiten: Er bricht sie ab und lässt das Ziel kurz verstummen. Bosse verlieren Standfestigkeit." },
            ["acid"] = new[] { "Säurereaktor", "Sprüht Korrosionssäure auf die Lane. Jeder Spritzer zersetzt das Ziel: −5 % auf alle Resistenzen je Stapel (bis 4 Stapel). Damit treffen alle anderen Türme härter – auch gepanzerte und geschirmte Gegner." },
            ["quake"] = new[] { "Seismohammer", "Ein Hydraulikhammer schlägt auf den Boden: Die Schockwelle trifft alle Bodengegner in Reichweite, lässt sie kurz taumeln – und trifft auch eingegrabene Gegner, die dabei an die Oberfläche geschleudert werden." },
            ["gale"] = new[] { "Druckwellenturbine", "Turbinen erzeugen Druckwellen, die alle Gegner in Reichweite ein Feld zurückwerfen – Flieger zwei Felder. Jeder Gegner kann nur alle 2,5 s zurückgestoßen werden. Bosse werden nur gebremst." },
            ["eyrie"] = new[] { "Scharfschützenkuppel", "Ein Präzisionsgewehr mit riesiger Reichweite. Zielt auf den stärksten Gegner und eliminiert Nicht-Bosse unter 10 % Leben sofort. Bosse unter 30 % Leben erleiden zusätzlichen Schaden." },
            ["barracks"] = new[] { "Klonbunker", "Schickt 2 Klonsoldaten auf die Lane. Jeder hält einen Bodengegner auf und kämpft – bis seine Energie (6 s Halten) erschöpft ist. Nach 10 s ist ein neuer Klon bereit. Eliten zehren doppelt, Bosse werden nicht aufgehalten, sondern nur gebremst." },
            ["bank"] = new[] { "Kreditbörse", "Zahlt nach jeder überstandenen Welle 4 % deines ungenutzten Golds als Zinsen – höchstens 18 Gold plus 0,6 je Welle. Zahlt erst ab der Welle nach dem Bau." },
            ["ward"] = new[] { "Schildgenerator", "Ein Energieschild schützt Türme in 2,5 Feldern: Die nächste Betäubung oder Lähmung durch Gegner, Bosse oder Explosionen prallt ab. Danach lädt der Schutz des betroffenen Turms 14 s nach." },
            // Fusionen (Beschreibung: Fantasy-Text mit Wortersetzung)
            ["plasma"] = new[] { "Fusionsbrenner", null }, ["glacier"] = new[] { "Nullpunktkanone", null }, ["voidlance"] = new[] { "Antimaterielanze", null },
            ["starfall"] = new[] { "Orbitalbombardement", null }, ["thornwood"] = new[] { "Flechette-Batterie", null }, ["thunderspire"] = new[] { "Hochspannungsturm", null },
            ["riftgate"] = new[] { "Wurmlochtor", null }, ["phoenix"] = new[] { "Kampfträger", null }, ["cathedral"] = new[] { "Infraschall-Dom", null },
            ["stormeye"] = new[] { "Ionensturm-Auge", null }, ["skywatch"] = new[] { "Abfangstation", null }, ["sporemine"] = new[] { "Toxinminenfeld", null },
            ["silencehall"] = new[] { "Funkstille-Zitadelle", null }, ["banecircle"] = new[] { "Nullfeld", null }, ["acidmortar"] = new[] { "Korrosionsartillerie", null },
            ["plaguepot"] = new[] { "Nanoseuche-Reaktor", null }, ["quakemaw"] = new[] { "Gravitationshammer", null }, ["cyclone"] = new[] { "Ionensturm-Turbine", null },
            ["stormbattery"] = new[] { "Abfangturbine", null }, ["falcon"] = new[] { "Gauss-Scharfschütze", null }, ["sunlance"] = new[] { "Orbitallaser", null },
            ["order"] = new[] { "Mech-Garnison", null }, ["treasury"] = new[] { "Zentralbank", null }, ["deepdrill"] = new[] { "Kernbohrer", null },
            ["shieldnet"] = new[] { "Schildmatrix", null },
        };

        /// <summary>Ausbaustufen je Turm: Stufe 2, 3, 4, Spezialisierung A, B.</summary>
        static readonly Dictionary<string, string[]> UPG = new Dictionary<string, string[]>
        {
            ["ranger"] = new[] { "Pulsverdichter", "Wolframkerne", "Zielcomputer", "Präzisionsmodul", "Salvenwerfer" },
            ["ember"] = new[] { "Überhitzte Kerne", "Streufokus", "Kühlsystem", "Supernova", "Plasmabrand" },
            ["frost"] = new[] { "Kryokern", "Weitstrahler", "Präzisionsdüsen", "Absoluter Nullpunkt", "Stasisfeld" },
            ["storm"] = new[] { "Leitplasma", "Ionenzelle", "Hochspannung", "Kettenentladung", "Überschlag" },
            ["spore"] = new[] { "Potentes Toxin", "Druckwerfer", "Haftschleim", "Toxinwolke", "Neurotoxin" },
            ["ballista"] = new[] { "Supraleiter", "Wolframkern", "Fragmentierung", "Durchschlag", "Titanenjäger" },
            ["rune"] = new[] { "Tiefenscan", "Fokuslinse", "Zerfallsfeld", "Psi-Bruch", "Psi-Fokus" },
            ["resonator"] = new[] { "Verstärker", "Obertöne", "Taktgeber", "Schockwelle", "Funkstille" },
            ["goldmine"] = new[] { "Stahlstreben", "Förderband", "Sprengladungen", "Tiefenbohrung", "Kopfgeldterminal" },
            ["pylon"] = new[] { "Verstärkte Spulen", "Verteiler", "Resonanzkern", "Weitfunk", "Überladung" },
            ["cannon"] = new[] { "Hochenergieladung", "Langer Lauf", "Autolader", "Streumunition", "Erschütterungsladung" },
            ["riftbinder"] = new[] { "Stabile Phase", "Schnellkopplung", "Zwillingsphase", "Phasenbruch", "Stasisanker" },
            ["meteor"] = new[] { "Schwere Ladung", "Zielsatellit", "Thermitkern", "Orbitalschauer", "Kinetischer Hammer" },
            ["tesla"] = new[] { "Supraleitende Wicklung", "Hochspannungsmast", "Kondensatorbank", "Elektrozaun", "Kugelblitz" },
            ["gravity"] = new[] { "Dichter Kern", "Ereignishorizont", "Pulsar", "Singularität", "Zermalmer" },
            ["dragon"] = new[] { "Upgrade-Chip", "Weite Flügel", "Plasmastrahl", "Alpha-Drohne", "Blitzdrohne" },
            ["laser"] = new[] { "Reiner Fokus", "Linsensystem", "Resonanzgitter", "Prisma", "Sonnenstrahl" },
            ["totem"] = new[] { "Feldspulen", "Verstärkerkanal", "Taktsignal", "Kampfprotokoll", "Sensorfeld" },
            ["minelayer"] = new[] { "Hochexplosiv", "Großes Magazin", "Splitterladung", "Streuminen", "EMP-Minen" },
            ["skyguard"] = new[] { "Zielradar", "Splittergranaten", "Schnelllader", "Flakgewitter", "Fangnetze" },
            ["seal"] = new[] { "Starke Störung", "Blockiercode", "Fernsender", "Störkette", "Totalausfall" },
            ["acid"] = new[] { "Konzentrat", "Breiter Strahl", "Reaktordruck", "Auflösung", "Säurebad" },
            ["quake"] = new[] { "Schwerer Kolben", "Tiefer Stoß", "Hydraulikdruck", "Krustenbrecher", "Nachbeben" },
            ["gale"] = new[] { "Große Turbinen", "Böenkamm", "Turbolader", "Orkan", "Scherwind" },
            ["eyrie"] = new[] { "Zielfernrohr", "Panzerbrechend", "Stabilisator", "Exekutor", "Doppelschuss" },
            ["barracks"] = new[] { "Panzerplatten", "Drill", "Dritte Klonreihe", "Schildwall", "Klingenmeister" },
            ["bank"] = new[] { "Tresor", "Algorithmus", "Handelslizenz", "Großbank", "Wechselstube" },
            ["ward"] = new[] { "Verstärkter Schild", "Schnellladung", "Kristallgitter", "Bollwerk", "Spiegelschild" },
        };

        static readonly Dictionary<string, string[]> ACTIVE = new Dictionary<string, string[]>
        {
            ["deepfreeze"] = new[] { "Kryoschock", null },
            ["overload"] = new[] { "Ionensturm", null },
            ["goldrush"] = new[] { "Förderschub", null },
            ["surge"] = new[] { "Datenflut", null },
            ["rewind"] = new[] { "Phasensprung", null },
            ["meteorstorm"] = new[] { "Orbitalsalve", "5 zusätzliche Orbitalschläge auf Gegner in Reichweite." },
            ["nova"] = new[] { "Psi-Nova", "Entlädt eine Psi-Nova: vierfacher Schaden an allen Gegnern in Reichweite." },
        };

        /// <summary>Wortersetzungen für die übrigen Texte (ganze Wörter; längere zuerst).</summary>
        static readonly (string, string)[] VOCAB =
        {
            ("Glockenschläge", "Infraschallstöße"), ("Glockenschlag", "Infraschallstoß"), ("Glutkugeln", "Plasmakugeln"), ("Eisgranaten", "Kryogranaten"),
            ("arkane Lanzen", "Antimaterie-Lanzen"), ("Sternensplittern", "Orbitalgeschossen"), ("Dornenpfeile", "Flechettes"), ("Eisflak", "Kryoflak"),
            ("Eisexplosionen", "Kryoexplosionen"), ("eisige Kettenblitze", "Kryo-Kettenblitze"), ("Sporenminen", "Toxinminen"), ("Sporenwolke", "Toxinwolke"),
            ("Sporenkapseln", "Toxinkapseln"), ("Runenkreis", "Nullfeld"), ("Säuregranaten", "Korrosionsgranaten"), ("Seuchenschleim", "Nanoseuche"),
            ("Gewitterböen", "Ionenböen"), ("Fallwind-Granaten", "Abfanggranaten"), ("Phönix", "Kampfträger"), ("Horst", "Hangar"), ("Drachen", "Drohnen"),
            ("Drache", "Drohne"), ("Soldaten", "Klonsoldaten"), ("Rittern", "Mechs"), ("Ritter", "Mechs"), ("Meteore", "Orbitalschläge"), ("Meteor", "Orbitalschlag"),
            ("Rissmale", "Phasenmarken"), ("Risse", "Portale"), ("Riss", "Portal"), ("Pfeile", "Geschosse"), ("Bolzen", "Wolframbolzen"), ("Ordensritter", "Kampfmechs"), ("Goldader", "Erzader"), ("Runenmarkierungen", "Psi-Markierungen"), ("Rune", "Psi"), ("Armbrust", "Railgun"), ("Kanone", "Massetreiber"), ("Waldläufer", "Pulsgeschütz"), ("Sonnenlicht", "Orbitalenergie"), ("Kristallkuppel", "Schildkuppel"),
            ("Runen", "Psi-Glyphen"), ("arkane", "psionische"), ("Arkan", "Psi"), ("Feuer", "Plasma"), ("Spulen", "Generatoren"), ("Goldadern", "Erzadern"),
        };

        // Originale (einmal gesichert)
        static Dictionary<string, string[]> origTower;
        static Dictionary<string, string[]> origUpg;
        static Dictionary<string, string[]> origActive;
        static Dictionary<string, BiomeDefinition> origBiome;
        static Dictionary<string, MoodDefinition> origMood;
        static Dictionary<string, string> origDmg;
        static List<(Regex, string)> vocab;

        /// <summary>Thema setzen. Kann beliebig oft aufgerufen werden (stellt vorher immer die Originale her).</summary>
        public static void Apply(string theme)
        {
            theme = theme == SCIFI ? SCIFI : FANTASY;
            Backup();
            Restore();
            current = theme;
            modelMap.Clear();
            if (theme != SCIFI) return;

            if (vocab == null)
            {
                // Turmnamen zuerst (lang vor kurz), dann Vokabular
                var pairs = new List<(string, string)>();
                foreach (var kv in TOWER)
                    if (origTower.TryGetValue(kv.Key, out var o) && !string.IsNullOrEmpty(o[0])) pairs.Add((o[0], kv.Value[0]));
                pairs = pairs.OrderByDescending((p) => p.Item1.Length).ToList();
                pairs.AddRange(VOCAB);
                vocab = pairs.Select((p) => (new Regex(@"(?<![\wÄÖÜäöüß])" + Regex.Escape(p.Item1) + @"(?![\wÄÖÜäöüß])"), p.Item2)).ToList();
            }

            foreach (var kv in Towers.TOWERS)
            {
                var d = kv.Value;
                if (TOWER.TryGetValue(d.id, out var t))
                {
                    if (t[0] != null) d.name = t[0];
                    d.description = t[1] ?? Sub(d.description);
                }
                else d.description = Sub(d.description);
                d.role = Role(d.role);
                d.synergies = Sub(d.synergies);
                d.counters = Sub(d.counters);
                UPG.TryGetValue(d.id, out var un);
                int k = 0;
                foreach (var u in d.upgrades.Concat(d.specs))
                {
                    if (un != null && k < un.Length) u.name = un[k];
                    u.description = Sub(u.description);
                    k++;
                }
            }
            foreach (var kv in Towers.TOWER_ACTIVES)
            {
                if (ACTIVE.TryGetValue(kv.Key, out var a))
                {
                    kv.Value.name = a[0];
                    kv.Value.description = a[1] ?? Sub(kv.Value.description);
                }
                else kv.Value.description = Sub(kv.Value.description);
            }
            foreach (var d in DmgDicts()) foreach (var kv in DMG_SCIFI) d[kv.Key] = kv.Value;
            foreach (var kv in SCI_BIOME) Biomes.BIOMES[kv.Key] = SciBiome(origBiome[kv.Key], kv.Value);
            foreach (var kv in SCI_MOOD) WorldView.MOOD[kv.Key] = kv.Value;
        }

        public static string Sub(string s)
        {
            if (string.IsNullOrEmpty(s) || vocab == null) return s;
            foreach (var (re, rep) in vocab) s = re.Replace(s, rep);
            return s;
        }

        static string Role(string r)
        {
            if (string.IsNullOrEmpty(r)) return r;
            foreach (var kv in new[] { ("Feuer", "Plasma"), ("Frost", "Kryo"), ("Blitz", "Elektro"), ("Gift", "Toxin"), ("Arkan", "Psi"), ("Physisch", "Kinetisch"),
                ("Kosmischer Einschlag", "Orbitalschlag"), ("Riesiger Einschlag", "Orbitalschlag"), ("Ritterorden", "Mech-Einheit"), ("Feuersturm", "Plasmasturm"),
                ("Blitznetz", "Elektronetz"), ("Himmelsstrahl", "Orbitalstrahl"), ("Bergbau", "Tiefbau"), ("Seuche", "Nanoseuche"), ("Bann", "Nullfeld") })
                r = Regex.Replace(r, @"(?<![\wÄÖÜäöüß])" + Regex.Escape(kv.Item1) + @"(?![\wÄÖÜäöüß])", kv.Item2);
            return r;
        }

        static IEnumerable<Dictionary<string, string>> DmgDicts()
        {
            yield return TextUtil.DAMAGE_NAMES;
            yield return HudText.DAMAGE_NAMES;
            yield return Palette.DAMAGE_NAMES;
        }

        static void Backup()
        {
            if (origTower != null) return;
            origTower = new Dictionary<string, string[]>();
            origUpg = new Dictionary<string, string[]>();
            foreach (var kv in Towers.TOWERS)
            {
                var d = kv.Value;
                origTower[kv.Key] = new[] { d.name, d.description, d.role, d.synergies, d.counters };
                var list = new List<string>();
                foreach (var u in d.upgrades.Concat(d.specs)) { list.Add(u.name); list.Add(u.description); }
                origUpg[kv.Key] = list.ToArray();
            }
            origActive = Towers.TOWER_ACTIVES.ToDictionary((kv) => kv.Key, (kv) => new[] { kv.Value.name, kv.Value.description });
            origBiome = new Dictionary<string, BiomeDefinition>(Biomes.BIOMES);
            origMood = new Dictionary<string, MoodDefinition>(WorldView.MOOD);
            origDmg = new Dictionary<string, string>(TextUtil.DAMAGE_NAMES);
        }

        static void Restore()
        {
            foreach (var kv in Towers.TOWERS)
            {
                if (!origTower.TryGetValue(kv.Key, out var o)) continue;
                var d = kv.Value;
                d.name = o[0]; d.description = o[1]; d.role = o[2]; d.synergies = o[3]; d.counters = o[4];
                var u = origUpg[kv.Key];
                int k = 0;
                foreach (var up in d.upgrades.Concat(d.specs))
                {
                    if (k * 2 + 1 < u.Length) { up.name = u[k * 2]; up.description = u[k * 2 + 1]; }
                    k++;
                }
            }
            foreach (var kv in Towers.TOWER_ACTIVES)
                if (origActive.TryGetValue(kv.Key, out var a)) { kv.Value.name = a[0]; kv.Value.description = a[1]; }
            foreach (var d in DmgDicts()) foreach (var kv in origDmg) d[kv.Key] = kv.Value;
            foreach (var kv in origBiome) Biomes.BIOMES[kv.Key] = kv.Value;
            foreach (var kv in origMood) WorldView.MOOD[kv.Key] = kv.Value;
        }

        // ------------------------------------------------------------ Biome (nur Optik)

        class SciBiomeSpec
        {
            public string name, description, sea, weather;
            public BiomePalette palette;
            public double[] tint;
            public double saturation, contrast, musicRoot;
            public double[] scale;
        }

        static readonly Dictionary<string, SciBiomeSpec> SCI_BIOME = new Dictionary<string, SciBiomeSpec>
        {
            ["forest"] = new SciBiomeSpec
            {
                name = "Alien-Dschungel", description = "Biolumineszente Riesenpflanzen auf einer fremden Welt.", sea = "water", weather = "fireflies",
                palette = new BiomePalette { ground = 0x2e6a5c, ground2 = 0x377a68, path = 0x6c7a8c, cliff = 0x24303a, high = 0x438a78, obstacle = 0x24586a, obstacle2 = 0x8a5ad0, accent = 0x5affd0, sky = 0x081a26, fog = 0x123038, light = 0xd8fff0 },
                tint = new double[] { 0.94, 1.02, 1.06 }, saturation = 1.12, contrast = 1.08, musicRoot = 50, scale = new double[] { 0, 2, 3, 7, 10 },
            },
            ["volcano"] = new SciBiomeSpec
            {
                name = "Plasmawerk", description = "Ein Industrieplanet mit glühenden Plasmakanälen.", sea = "lava", weather = "embers",
                palette = new BiomePalette { ground = 0x3a3a44, ground2 = 0x45454f, path = 0x6c6c78, cliff = 0x1c1c24, high = 0x52525e, obstacle = 0x2a2a34, obstacle2 = 0xff5a20, accent = 0xff7a30, sky = 0x180c10, fog = 0x281618, light = 0xffd8c0 },
                tint = new double[] { 1.06, 0.98, 0.94 }, saturation = 1.1, contrast = 1.12, musicRoot = 45, scale = new double[] { 0, 1, 3, 6, 7 },
            },
            ["frozen"] = new SciBiomeSpec
            {
                name = "Eismond", description = "Gefrorene Mondoberfläche mit verlassenen Forschungsstationen.", sea = "ice", weather = "snow",
                palette = new BiomePalette { ground = 0xb4c4d6, ground2 = 0xa4b4ca, path = 0x667488, cliff = 0x46566c, high = 0xcedeee, obstacle = 0x88a0c0, obstacle2 = 0x56667c, accent = 0x5ad0ff, sky = 0x060c18, fog = 0x18283e, light = 0xe0f0ff },
                tint = new double[] { 0.92, 1, 1.1 }, saturation = 0.95, contrast = 1.08, musicRoot = 52, scale = new double[] { 0, 2, 5, 7, 11 },
            },
            ["crystal"] = new SciBiomeSpec
            {
                name = "Kristallasteroid", description = "Violette Kristallfelder auf einem treibenden Asteroiden.", sea = "abyss", weather = "sparkles",
                palette = new BiomePalette { ground = 0x4c3c5e, ground2 = 0x56466c, path = 0x7c7292, cliff = 0x2a2238, high = 0x6c5c82, obstacle = 0x9a60e0, obstacle2 = 0x40d0e0, accent = 0xd080ff, sky = 0x080412, fog = 0x1c122e, light = 0xf0e0ff },
                tint = new double[] { 1.02, 0.96, 1.08 }, saturation = 1.15, contrast = 1.08, musicRoot = 49, scale = new double[] { 0, 2, 4, 6, 8 },
            },
            ["sky"] = new SciBiomeSpec
            {
                name = "Orbitalstation", description = "Eine Andockplattform hoch über einem Gasriesen.", sea = "clouds", weather = "sparkles",
                palette = new BiomePalette { ground = 0x5c6672, ground2 = 0x66707c, path = 0x8c98a6, cliff = 0x38404c, high = 0x727e8c, obstacle = 0x3a4656, obstacle2 = 0xe0f0ff, accent = 0x5ae0ff, sky = 0x081226, fog = 0x182846, light = 0xf0f8ff },
                tint = new double[] { 0.96, 1, 1.06 }, saturation = 1.02, contrast = 1.1, musicRoot = 54, scale = new double[] { 0, 2, 4, 7, 9 },
            },
            ["swamp"] = new SciBiomeSpec
            {
                name = "Toxischer Planet", description = "Ätzende Sümpfe unter einem giftgrünen Himmel.", sea = "bog", weather = "spores",
                palette = new BiomePalette { ground = 0x3a4a2c, ground2 = 0x445632, path = 0x6a6c5a, cliff = 0x282a20, high = 0x50642e, obstacle = 0x2a3a20, obstacle2 = 0xb0ff40, accent = 0xa0ff30, sky = 0x0e1806, fog = 0x283a12, light = 0xf0ffd0 },
                tint = new double[] { 0.98, 1.06, 0.92 }, saturation = 1.1, contrast = 1.06, musicRoot = 47, scale = new double[] { 0, 1, 4, 6, 9 },
            },
            ["cavern"] = new SciBiomeSpec
            {
                name = "Mondbasis", description = "Unterirdische Hangars tief im Mondgestein.", sea = "abyss", weather = "sparkles",
                palette = new BiomePalette { ground = 0x34343e, ground2 = 0x3c3c48, path = 0x5c6272, cliff = 0x1c1c24, high = 0x4a4a5a, obstacle = 0x2a2a36, obstacle2 = 0xff40a0, accent = 0x40f0ff, sky = 0x04040a, fog = 0x10101e, light = 0xe8f0ff },
                tint = new double[] { 0.96, 0.98, 1.08 }, saturation = 1.05, contrast = 1.1, musicRoot = 43, scale = new double[] { 0, 3, 5, 6, 10 },
            },
        };

        /// <summary>Name des Bioms im Sci-Fi-Thema (für die Auswahl im Startbildschirm).</summary>
        public static string SciBiomeName(string id) => SCI_BIOME.TryGetValue(id ?? "", out var b) ? b.name : null;

        static BiomeDefinition SciBiome(BiomeDefinition o, SciBiomeSpec s)
        {
            return new BiomeDefinition
            {
                id = o.id, name = s.name, description = s.description, palette = s.palette,
                obstacleDensity = o.obstacleDensity, heightAmp = o.heightAmp, decorKinds = o.decorKinds, events = o.events,
                sea = s.sea, weather = s.weather,
                grade = new BiomeGrade { tint = s.tint, saturation = s.saturation, contrast = s.contrast },
                musicRoot = s.musicRoot, scale = s.scale.ToList(),
            };
        }

        static readonly Dictionary<string, MoodDefinition> SCI_MOOD = new Dictionary<string, MoodDefinition>
        {
            ["forest"] = new MoodDefinition(0x163a46, 0x5affd0, 0.18, 0.55, 0.1),
            ["volcano"] = new MoodDefinition(0x3a161a, 0xff6a30, 0.14, 0.5, 0.11),
            ["frozen"] = new MoodDefinition(0x14223e, 0x9ae0ff, 0.06, 0.85, 0.1),
            ["crystal"] = new MoodDefinition(0x24143e, 0xd080ff, 0, 0.95, 0.1),
            ["sky"] = new MoodDefinition(0x182856, 0x80d0ff, 0.2, 0.8, 0.12),
            ["swamp"] = new MoodDefinition(0x18300e, 0xb0ff40, 0.28, 0.35, 0.07),
            ["cavern"] = new MoodDefinition(0x0e0e2a, 0x40f0ff, 0, 0.95, 0.05),
        };

        /// <summary>Meeresfarben im Sci-Fi-Thema (Alien-Wasser, Plasma, Gasriese, Weltraum …).</summary>
        public static int SeaColor(string kind, int fantasy)
        {
            if (!SciFi) return fantasy;
            switch (kind)
            {
                case "water": return 0x0c5a5c;
                case "lava": return 0xff4a18;
                case "ice": return 0x4c7cae;
                case "bog": return 0x4a8a10;
                case "clouds": return 0xd8a870;
                case "abyss": return 0x02030a;
                default: return fantasy;
            }
        }
    }
}

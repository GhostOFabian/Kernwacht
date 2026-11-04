// Übersetzung von config/items.ts.
// Gemeinsames, datengetriebenes Itemmodell für alle Ausrüstungsarten, dazu Tränke,
// Herstellungsrezepte und das Ladenangebot des Helden-Endlosmodus.
//
// Cost = Partial<Record<ResourceId | 'gold', number>> → Dictionary<string, double>.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public class ItemDefinition
    {
        public string id;   // ItemId
        public string name;
        public string slot;     // EquipSlot
        public string rarity;   // Rarity
        public string icon;
        public string description;
        public Dictionary<string, double> stats = new Dictionary<string, double>();   // Partial<ChampionStats>
        /// <summary>Nur für bestimmte Helden ausrüstbar. ChampionId[] oder null.</summary>
        public List<string> champions;
        /// <summary>Ab dieser Welle als Beute möglich.</summary>
        public double minWave;
        /// <summary>Nur durch Kombination herstellbar (keine Fragmente, kein Drop).</summary>
        public bool? craftOnly;
    }

    /// <summary>TS: inline-Typ <c>SLOTS[i]: { slot: EquipSlot; name: string; icon: string }</c>.</summary>
    public class EquipSlotDefinition
    {
        public string slot;   // EquipSlot
        public string name;
        public string icon;
    }

    /// <summary>TS: inline-Typ der Werte von <c>RARITY</c>.</summary>
    public class RarityDefinition
    {
        public string name;
        public string color;
        public double fragments;
        public Dictionary<string, double> craftCost = new Dictionary<string, double>();   // Cost
        public Dictionary<string, double> shopCost = new Dictionary<string, double>();    // Cost
        public double order;
    }

    /// <summary>TS: inline-Typ <c>PotionDefinition.tower: { radius; dmg; speed }</c>.</summary>
    public class PotionTowerEffect
    {
        public double radius;
        public double dmg;
        public double speed;
    }

    public class PotionDefinition
    {
        public string id;   // PotionId
        public string name;
        public string icon;
        public double color;
        public string description;
        /// <summary>Sofortwirkung (Anteil der Max-LP bzw. -Mana).</summary>
        public double? heal;
        public double? mana;
        /// <summary>Zeitlich begrenzte Attribute. Partial&lt;ChampionStats&gt; oder null.</summary>
        public Dictionary<string, double> buff;
        /// <summary>Turmverstärkung im Umkreis. null = keine.</summary>
        public PotionTowerEffect tower;
        public double duration;
        public double cooldown;
        public double stack;
        public string rarity;   // Rarity
    }

    // RecipeKind = 'fragments' | 'combine' | 'upgrade' | 'hero' (string)

    /// <summary>TS: inline-Typ <c>RecipeDefinition.fragments: { item: ItemId; count: number }</c>.</summary>
    public class RecipeFragments
    {
        public string item;   // ItemId
        public double count;
    }

    public class RecipeDefinition
    {
        public string id;
        public string kind;   // RecipeKind
        public string name;
        /// <summary>Ergebnis-Item (bei Fragment- und Kombinationsrezepten).</summary>
        public string result;
        /// <summary>Fragmente eines Items.</summary>
        public RecipeFragments fragments;
        /// <summary>Verbrauchte Items (müssen im Heldeninventar liegen). ItemId[] oder null.</summary>
        public List<string> items;
        public Dictionary<string, double> cost = new Dictionary<string, double>();   // Cost
        public double? minWave;
        public string perk;
        /// <summary>3.2: nur für diesen Helden (Heldenschmiede). ChampionId oder null.</summary>
        public string champion;
    }

    /// <summary>TS: inline-Typ <c>HERO_RECIPE_COST: { epic: Cost; legendary: Cost }</c>.</summary>
    public class HeroRecipeCost
    {
        public Dictionary<string, double> epic;
        public Dictionary<string, double> legendary;
    }

    /// <summary>TS: inline-Objekt <c>HERO_RECIPE_WAVE</c>.</summary>
    public class HeroRecipeWave
    {
        public double epic;
        public double legendary;
    }

    // ShopCategory = 'potions' | 'fragments' | 'materials' | 'collection' | 'special' (string)

    /// <summary>TS: inline-Typ <c>ShopOffer.give</c>.</summary>
    public class ShopOfferGive
    {
        public string potion;                         // PotionId oder null
        public Dictionary<string, double> resource;   // Partial<Record<ResourceId, number>> oder null
        public double? randomFragment;
        public double? skillPoint;
    }

    public class ShopOffer
    {
        public string id;
        public string category;   // ShopCategory
        public string name;
        public string description;
        public Dictionary<string, double> cost = new Dictionary<string, double>();   // Cost
        /// <summary>Bestand je Auffrischung (null = unbegrenzt).</summary>
        public double? stock;
        public string perk;
        public ShopOfferGive give;
    }

    /// <summary>TS: inline-Typ <c>SHOP_CATEGORIES[i]: { id: ShopCategory; name: string }</c>.</summary>
    public class ShopCategoryDefinition
    {
        public string id;   // ShopCategory
        public string name;
    }

    public static partial class Items
    {
        /// <summary>Ausrüstungsslots in fester Reihenfolge (6 Stück). Der Spezialslot nimmt auch einen Trankstapel auf.</summary>
        public static readonly List<EquipSlotDefinition> SLOTS = new List<EquipSlotDefinition>
        {
            new EquipSlotDefinition { slot = "weapon", name = "Waffe", icon = "⚔" },
            new EquipSlotDefinition { slot = "armor", name = "Rüstung", icon = "🛡" },
            new EquipSlotDefinition { slot = "helm", name = "Helm", icon = "⛑" },
            new EquipSlotDefinition { slot = "artifact", name = "Artefakt", icon = "🔮" },
            new EquipSlotDefinition { slot = "accessory", name = "Accessoire", icon = "💍" },
            new EquipSlotDefinition { slot = "special", name = "Spezial", icon = "✦" },
        };

        public static readonly Dictionary<string, RarityDefinition> RARITY = new Dictionary<string, RarityDefinition>
        {
            ["common"] = new RarityDefinition { name = "Gewöhnlich", color = "#c8c8d0", fragments = 3, craftCost = new Dictionary<string, double>(), shopCost = new Dictionary<string, double> { ["metal"] = 8 }, order = 0 },
            ["rare"] = new RarityDefinition { name = "Selten", color = "#5ab0ff", fragments = 4, craftCost = new Dictionary<string, double> { ["crystal"] = 2 }, shopCost = new Dictionary<string, double> { ["metal"] = 14, ["crystal"] = 4 }, order = 1 },
            ["epic"] = new RarityDefinition { name = "Episch", color = "#c070ff", fragments = 5, craftCost = new Dictionary<string, double> { ["essence"] = 3 }, shopCost = new Dictionary<string, double> { ["metal"] = 20, ["crystal"] = 8, ["essence"] = 4 }, order = 2 },
            ["legendary"] = new RarityDefinition { name = "Legendär", color = "#ffb840", fragments = 6, craftCost = new Dictionary<string, double> { ["relic"] = 2 }, shopCost = new Dictionary<string, double> { ["metal"] = 30, ["crystal"] = 12, ["essence"] = 8, ["relic"] = 2 }, order = 3 },
        };

        private static ItemDefinition I(ItemDefinition d) { return d; }

        /// <summary>TS: <c>Object.fromEntries(list.map((d) =&gt; [d.id, d]))</c> – spätere gleiche IDs überschreiben, Position der ersten bleibt.</summary>
        private static Dictionary<string, ItemDefinition> fromEntries(List<ItemDefinition> list)
        {
            Dictionary<string, ItemDefinition> d = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition x in list) d[x.id] = x;
            return d;
        }

        public static readonly Dictionary<string, ItemDefinition> ITEMS = fromEntries(new List<ItemDefinition>
        {
            // Waffen
            I(new ItemDefinition { id = "ironBlade", name = "Eisenklinge", slot = "weapon", rarity = "common", icon = "🗡", minWave = 1, champions = new List<string> { "brann", "vela" }, description = "Solide Klinge.", stats = new Dictionary<string, double> { ["damage"] = 6 } }),
            I(new ItemDefinition { id = "runeStaff", name = "Runenstab", slot = "weapon", rarity = "common", icon = "🪄", minWave = 1, champions = new List<string> { "lyra" }, description = "Bündelt Runenkraft für Turmzauber.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.1, ["supportPower"] = 0.1 } }),
            I(new ItemDefinition { id = "stormBow", name = "Sturmbogen", slot = "weapon", rarity = "rare", icon = "🏹", minWave = 5, champions = new List<string> { "vela", "brann" }, description = "Leicht und schnell.", stats = new Dictionary<string, double> { ["damage"] = 8, ["attackSpeed"] = 0.15 } }),
            I(new ItemDefinition { id = "choirScepter", name = "Chorzepter", slot = "weapon", rarity = "epic", icon = "🎼", minWave = 10, champions = new List<string> { "lyra" }, description = "Verstärkt jede Turmverstärkung.", stats = new Dictionary<string, double> { ["supportPower"] = 0.25, ["supportRadius"] = 0.6 } }),
            I(new ItemDefinition { id = "dragonFang", name = "Drachenzahn", slot = "weapon", rarity = "epic", icon = "🦷", minWave = 12, champions = new List<string> { "brann", "vela" }, description = "Eine Waffe aus einem Drachenzahn.", stats = new Dictionary<string, double> { ["damage"] = 14, ["critChance"] = 0.08 } }),
            I(new ItemDefinition { id = "worldBlade", name = "Weltenklinge", slot = "weapon", rarity = "legendary", icon = "⚔", minWave = 15, craftOnly = true, champions = new List<string> { "brann", "vela" }, description = "Geschmiedet aus Drachenzahn und Eisen.", stats = new Dictionary<string, double> { ["damage"] = 24, ["critChance"] = 0.12, ["critDamage"] = 0.3 } }),
            // Rüstungen
            I(new ItemDefinition { id = "leather", name = "Lederwams", slot = "armor", rarity = "common", icon = "🦺", minWave = 1, description = "Leichter Schutz.", stats = new Dictionary<string, double> { ["maxHp"] = 80, ["armor"] = 0.04 } }),
            I(new ItemDefinition { id = "chainmail", name = "Kettenhemd", slot = "armor", rarity = "rare", icon = "⛓", minWave = 4, description = "Hält auch Bissen stand.", stats = new Dictionary<string, double> { ["maxHp"] = 150, ["armor"] = 0.08 } }),
            I(new ItemDefinition { id = "emberPlate", name = "Glutpanzer", slot = "armor", rarity = "epic", icon = "🔥", minWave = 12, description = "Warmer Stahl, der Wunden schließt.", stats = new Dictionary<string, double> { ["maxHp"] = 220, ["armor"] = 0.1, ["hpRegen"] = 3 } }),
            I(new ItemDefinition { id = "starMantle", name = "Sternenmantel", slot = "armor", rarity = "legendary", icon = "🌌", minWave = 30, description = "Gewebt aus Sternenlicht.", stats = new Dictionary<string, double> { ["maxHp"] = 300, ["armor"] = 0.12, ["cdr"] = 0.08 } }),
            I(new ItemDefinition { id = "citadelPlate", name = "Zitadellenpanzer", slot = "armor", rarity = "legendary", icon = "🏰", minWave = 15, craftOnly = true, description = "Unerschütterlich.", stats = new Dictionary<string, double> { ["maxHp"] = 400, ["armor"] = 0.18, ["hpRegen"] = 5 } }),
            // Helme
            I(new ItemDefinition { id = "ironHelm", name = "Eisenhelm", slot = "helm", rarity = "common", icon = "⛑", minWave = 1, description = "Einfacher Kopfschutz.", stats = new Dictionary<string, double> { ["maxHp"] = 50, ["armor"] = 0.03 } }),
            I(new ItemDefinition { id = "seerCirclet", name = "Seherreif", slot = "helm", rarity = "rare", icon = "👁", minWave = 3, description = "Klärt den Geist.", stats = new Dictionary<string, double> { ["maxMana"] = 40, ["manaRegen"] = 1.5, ["cdr"] = 0.05 } }),
            I(new ItemDefinition { id = "runeCrown", name = "Runenkrone", slot = "helm", rarity = "epic", icon = "👑", minWave = 15, description = "Gekrönte Zauberkraft.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.2, ["cdr"] = 0.1 } }),
            // Artefakte
            I(new ItemDefinition { id = "emberCore", name = "Glutkern", slot = "artifact", rarity = "common", icon = "🔴", minWave = 1, description = "Pulsierende Wärme.", stats = new Dictionary<string, double> { ["damage"] = 3, ["abilityPower"] = 0.08 } }),
            I(new ItemDefinition { id = "manaPrism", name = "Manaprisma", slot = "artifact", rarity = "rare", icon = "🔷", minWave = 4, description = "Speichert Magie.", stats = new Dictionary<string, double> { ["maxMana"] = 60, ["manaRegen"] = 2 } }),
            I(new ItemDefinition { id = "riftShard", name = "Rissscherbe", slot = "artifact", rarity = "epic", icon = "💠", minWave = 14, description = "Ein Splitter des Risses.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.25, ["critDamage"] = 0.25 } }),
            I(new ItemDefinition { id = "coreHeart", name = "Herz des Kerns", slot = "artifact", rarity = "legendary", icon = "❤", minWave = 15, craftOnly = true, description = "Die Kraft des Kerns selbst.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.35, ["supportPower"] = 0.35, ["cdr"] = 0.12 } }),
            // Accessoires
            I(new ItemDefinition { id = "swiftBoots", name = "Flinkstiefel", slot = "accessory", rarity = "common", icon = "👢", minWave = 1, description = "Schneller auf dem Feld.", stats = new Dictionary<string, double> { ["moveSpeed"] = 0.5 } }),
            I(new ItemDefinition { id = "hawkEye", name = "Falkenauge", slot = "accessory", rarity = "rare", icon = "🦅", minWave = 3, description = "Sieht Schwachstellen.", stats = new Dictionary<string, double> { ["range"] = 0.6, ["critChance"] = 0.06 } }),
            I(new ItemDefinition { id = "vampRing", name = "Blutring", slot = "accessory", rarity = "rare", icon = "💍", minWave = 6, description = "Treffer heilen.", stats = new Dictionary<string, double> { ["lifesteal"] = 0.08, ["damage"] = 3 } }),
            I(new ItemDefinition { id = "berserkBand", name = "Berserkerband", slot = "accessory", rarity = "epic", icon = "💢", minWave = 12, champions = new List<string> { "brann" }, description = "Rasende Schläge treffen Umstehende.", stats = new Dictionary<string, double> { ["attackSpeed"] = 0.3, ["cleave"] = 0.2 } }),
            // Spezial
            I(new ItemDefinition { id = "magnetAmulet", name = "Magnetamulett", slot = "special", rarity = "common", icon = "🧲", minWave = 1, description = "Zieht Beute an.", stats = new Dictionary<string, double> { ["pickupRadius"] = 0.8 } }),
            I(new ItemDefinition { id = "luckyCharm", name = "Glücksbringer", slot = "special", rarity = "rare", icon = "🍀", minWave = 6, description = "Mehr Beute.", stats = new Dictionary<string, double> { ["lootBonus"] = 0.3 } }),
            I(new ItemDefinition { id = "towerSeal", name = "Turmsiegel", slot = "special", rarity = "epic", icon = "🗼", minWave = 10, description = "Weitere und stärkere Turmverstärkungen.", stats = new Dictionary<string, double> { ["supportPower"] = 0.2, ["supportRadius"] = 1 } }),
            // 3.2 Heldenschmiede: je Held ein epischer Signaturgegenstand und eine legendäre Signaturwaffe (nur herstellbar)
            I(new ItemDefinition { id = "lyraSpindle", name = "Webspindel", slot = "artifact", rarity = "epic", icon = "🧵", minWave = 8, craftOnly = true, champions = new List<string> { "lyra" }, description = "Spinnt Turmverstärkungen weiter. Signatur von Lyra.", stats = new Dictionary<string, double> { ["supportPower"] = 0.2, ["supportRadius"] = 0.5, ["cdr"] = 0.05 } }),
            I(new ItemDefinition { id = "lyraStaff", name = "Stab der Turmweberin", slot = "weapon", rarity = "legendary", icon = "🪄", minWave = 20, craftOnly = true, champions = new List<string> { "lyra" }, description = "Lyras Meisterstab: jede Verstärkung wird zum Chor. Signaturwaffe von Lyra.", stats = new Dictionary<string, double> { ["supportPower"] = 0.4, ["abilityPower"] = 0.2, ["supportRadius"] = 0.8 } }),
            I(new ItemDefinition { id = "brannSigil", name = "Klingenrufer-Siegel", slot = "accessory", rarity = "epic", icon = "🔱", minWave = 8, craftOnly = true, champions = new List<string> { "brann" }, description = "Jeder Hieb ruft weitere Klingen. Signatur von Brann.", stats = new Dictionary<string, double> { ["damage"] = 8, ["cleave"] = 0.15, ["lifesteal"] = 0.04 } }),
            I(new ItemDefinition { id = "brannBlade", name = "Rufklinge", slot = "weapon", rarity = "legendary", icon = "⚔", minWave = 20, craftOnly = true, champions = new List<string> { "brann" }, description = "Branns Schwert spaltet ganze Reihen. Signaturwaffe von Brann.", stats = new Dictionary<string, double> { ["damage"] = 28, ["cleave"] = 0.25, ["critDamage"] = 0.3 } }),
            I(new ItemDefinition { id = "velaQuiver", name = "Sturmköcher", slot = "accessory", rarity = "epic", icon = "🎐", minWave = 8, craftOnly = true, champions = new List<string> { "vela" }, description = "Pfeile mit Rückenwind. Signatur von Vela.", stats = new Dictionary<string, double> { ["attackSpeed"] = 0.2, ["range"] = 0.4 } }),
            I(new ItemDefinition { id = "velaBow", name = "Velas Sturmbogen", slot = "weapon", rarity = "legendary", icon = "🏹", minWave = 20, craftOnly = true, champions = new List<string> { "vela" }, description = "Ein Bogen, der den Wind spannt. Signaturwaffe von Vela.", stats = new Dictionary<string, double> { ["damage"] = 20, ["attackSpeed"] = 0.3, ["critChance"] = 0.1, ["range"] = 0.5 } }),
            I(new ItemDefinition { id = "kaelaHelm", name = "Wächterhelm", slot = "helm", rarity = "epic", icon = "⛑", minWave = 8, craftOnly = true, champions = new List<string> { "kaela" }, description = "Hält jeden Ansturm. Signatur von Kaela.", stats = new Dictionary<string, double> { ["maxHp"] = 180, ["armor"] = 0.08 } }),
            I(new ItemDefinition { id = "kaelaBlade", name = "Wächterklinge", slot = "weapon", rarity = "legendary", icon = "🗡", minWave = 20, craftOnly = true, champions = new List<string> { "kaela" }, description = "Schwert und Schild zugleich. Signaturwaffe von Kaela.", stats = new Dictionary<string, double> { ["damage"] = 22, ["armor"] = 0.1, ["maxHp"] = 200 } }),
            I(new ItemDefinition { id = "varoEmber", name = "Aschenglut", slot = "artifact", rarity = "epic", icon = "🔥", minWave = 8, craftOnly = true, champions = new List<string> { "varo" }, description = "Glimmt in jedem Bolzen. Signatur von Varo.", stats = new Dictionary<string, double> { ["damage"] = 6, ["critChance"] = 0.08 } }),
            I(new ItemDefinition { id = "varoCrossbow", name = "Aschenjäger-Armbrust", slot = "weapon", rarity = "legendary", icon = "🎯", minWave = 20, craftOnly = true, champions = new List<string> { "varo" }, description = "Bolzen, die nie ihr Ziel verfehlen. Signaturwaffe von Varo.", stats = new Dictionary<string, double> { ["damage"] = 24, ["critChance"] = 0.12, ["critDamage"] = 0.4 } }),
            I(new ItemDefinition { id = "nyxAmulet", name = "Rissamulett", slot = "accessory", rarity = "epic", icon = "🌀", minWave = 8, craftOnly = true, champions = new List<string> { "nyx" }, description = "Ein Schritt durch den Riss. Signatur von Nyx.", stats = new Dictionary<string, double> { ["moveSpeed"] = 0.4, ["critChance"] = 0.06, ["cdr"] = 0.06 } }),
            I(new ItemDefinition { id = "nyxDaggers", name = "Rissdolche", slot = "weapon", rarity = "legendary", icon = "🔪", minWave = 20, craftOnly = true, champions = new List<string> { "nyx" }, description = "Schneiden durch Raum und Rüstung. Signaturwaffe von Nyx.", stats = new Dictionary<string, double> { ["damage"] = 22, ["attackSpeed"] = 0.25, ["critDamage"] = 0.35 } }),
            I(new ItemDefinition { id = "lioraLute", name = "Runenlaute", slot = "artifact", rarity = "epic", icon = "🎻", minWave = 8, craftOnly = true, champions = new List<string> { "liora" }, description = "Klingt in allen Runen nach. Signatur von Liora.", stats = new Dictionary<string, double> { ["supportPower"] = 0.2, ["manaRegen"] = 2, ["cdr"] = 0.06 } }),
            I(new ItemDefinition { id = "lioraHarp", name = "Bardenharfe", slot = "weapon", rarity = "legendary", icon = "🎼", minWave = 20, craftOnly = true, champions = new List<string> { "liora" }, description = "Lioras Lied stärkt jeden Turm. Signaturwaffe von Liora.", stats = new Dictionary<string, double> { ["supportPower"] = 0.35, ["abilityPower"] = 0.25, ["cdr"] = 0.1 } }),
            I(new ItemDefinition { id = "thorgrimHelm", name = "Schmiedehelm", slot = "helm", rarity = "epic", icon = "⛑", minWave = 8, craftOnly = true, champions = new List<string> { "thorgrim" }, description = "Rußgeschwärzt und unverwüstlich. Signatur von Thorgrim.", stats = new Dictionary<string, double> { ["maxHp"] = 150, ["supportPower"] = 0.15, ["armor"] = 0.05 } }),
            I(new ItemDefinition { id = "thorgrimHammer", name = "Meisterhammer", slot = "weapon", rarity = "legendary", icon = "🔨", minWave = 20, craftOnly = true, champions = new List<string> { "thorgrim" }, description = "Formt Türme wie glühenden Stahl. Signaturwaffe von Thorgrim.", stats = new Dictionary<string, double> { ["damage"] = 16, ["supportPower"] = 0.35, ["armor"] = 0.08 } }),
            I(new ItemDefinition { id = "senaCrystal", name = "Wetterkristall", slot = "artifact", rarity = "epic", icon = "🔷", minWave = 8, craftOnly = true, champions = new List<string> { "sena" }, description = "Bündelt Sturm und Regen. Signatur von Sena.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.25, ["maxMana"] = 50 } }),
            I(new ItemDefinition { id = "senaScepter", name = "Sturmzepter", slot = "weapon", rarity = "legendary", icon = "🌩", minWave = 20, craftOnly = true, champions = new List<string> { "sena" }, description = "Befiehlt dem Himmel. Signaturwaffe von Sena.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.45, ["cdr"] = 0.12, ["manaRegen"] = 3 } }),
            I(new ItemDefinition { id = "oranFocus", name = "Leylinienfokus", slot = "artifact", rarity = "epic", icon = "💠", minWave = 8, craftOnly = true, champions = new List<string> { "oran" }, description = "Spürt die Kraftlinien der Welt. Signatur von Oran.", stats = new Dictionary<string, double> { ["supportRadius"] = 0.8, ["supportPower"] = 0.15 } }),
            I(new ItemDefinition { id = "oranStaff", name = "Leystab", slot = "weapon", rarity = "legendary", icon = "🪄", minWave = 20, craftOnly = true, champions = new List<string> { "oran" }, description = "Leitet Leylinien in jeden Turm. Signaturwaffe von Oran.", stats = new Dictionary<string, double> { ["supportPower"] = 0.3, ["supportRadius"] = 1, ["abilityPower"] = 0.2 } }),
            I(new ItemDefinition { id = "miraLamp", name = "Prospektorenlampe", slot = "special", rarity = "epic", icon = "🏮", minWave = 8, craftOnly = true, champions = new List<string> { "mira" }, description = "Findet jedes Vorkommen. Signatur von Mira.", stats = new Dictionary<string, double> { ["lootBonus"] = 0.35, ["pickupRadius"] = 0.8 } }),
            I(new ItemDefinition { id = "miraPick", name = "Goldhacke", slot = "weapon", rarity = "legendary", icon = "⛏", minWave = 20, craftOnly = true, champions = new List<string> { "mira" }, description = "Miras Hacke schlägt Gold aus Stein. Signaturwaffe von Mira.", stats = new Dictionary<string, double> { ["damage"] = 14, ["lootBonus"] = 0.5, ["supportPower"] = 0.2 } }),
            I(new ItemDefinition { id = "ommarHood", name = "Schweigehaube", slot = "helm", rarity = "epic", icon = "🎭", minWave = 8, craftOnly = true, champions = new List<string> { "ommar" }, description = "Stille schärft den Geist. Signatur von Ommar.", stats = new Dictionary<string, double> { ["cdr"] = 0.1, ["maxMana"] = 40, ["armor"] = 0.05 } }),
            I(new ItemDefinition { id = "ommarStaff", name = "Stab der Stille", slot = "weapon", rarity = "legendary", icon = "🪄", minWave = 20, craftOnly = true, champions = new List<string> { "ommar" }, description = "Verstummt Gegner und stärkt Verbündete. Signaturwaffe von Ommar.", stats = new Dictionary<string, double> { ["abilityPower"] = 0.35, ["supportPower"] = 0.25, ["cdr"] = 0.1 } }),
        });

        public static readonly List<string> ITEM_ORDER = new List<string>(ITEMS.Keys);

        /// <summary>Itemwerte inkl. Verstärkungsstufe (je +15 %, max. +3).</summary>
        public const double PLUS_STEP = 0.15;
        public const double MAX_PLUS = 3;

        public static Dictionary<string, double> itemStats(string id, double plus)
        {
            ItemDefinition d;
            if (id == null || !ITEMS.TryGetValue(id, out d) || d == null) return new Dictionary<string, double>();
            double k = 1 + PLUS_STEP * U.Min(MAX_PLUS, U.Max(0, plus));
            Dictionary<string, double> out_ = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> kv in d.stats) out_[kv.Key] = kv.Value * k;
            return out_;
        }

        public static bool canEquipItem(string id, string champion)
        {
            ItemDefinition d;
            if (id == null || !ITEMS.TryGetValue(id, out d) || d == null) return false;
            return d.champions == null || U.Includes(d.champions, champion);
        }

        // ------------------------------------------------------------ Tränke

        public static readonly Dictionary<string, PotionDefinition> POTIONS = new Dictionary<string, PotionDefinition>
        {
            ["heal"] = new PotionDefinition { id = "heal", name = "Heiltrank", icon = "❤", color = 0xff5060, description = "Heilt sofort 40 % der Lebenspunkte.", heal = 0.4, duration = 0, cooldown = 6, stack = 8, rarity = "common" },
            ["mana"] = new PotionDefinition { id = "mana", name = "Manatrank", icon = "💧", color = 0x5090ff, description = "Stellt sofort 50 % Mana wieder her.", mana = 0.5, duration = 0, cooldown = 6, stack = 8, rarity = "common" },
            ["power"] = new PotionDefinition { id = "power", name = "Krafttrank", icon = "💪", color = 0xff8040, description = "+30 % Schaden für 20 s.", duration = 20, cooldown = 2, stack = 5, rarity = "rare" },
            ["haste"] = new PotionDefinition { id = "haste", name = "Eiltrank", icon = "⚡", color = 0xffe060, description = "+35 % Angriffstempo für 20 s.", duration = 20, cooldown = 2, stack = 5, rarity = "rare" },
            ["swift"] = new PotionDefinition { id = "swift", name = "Windtrank", icon = "🌬", color = 0x80ffd0, description = "+1,2 Bewegungstempo für 20 s.", buff = new Dictionary<string, double> { ["moveSpeed"] = 1.2 }, duration = 20, cooldown = 2, stack = 5, rarity = "common" },
            ["stone"] = new PotionDefinition { id = "stone", name = "Steinhaut", icon = "🪨", color = 0xa09080, description = "+20 % Rüstung für 20 s.", buff = new Dictionary<string, double> { ["armor"] = 0.2 }, duration = 20, cooldown = 2, stack = 5, rarity = "rare" },
            ["focus"] = new PotionDefinition { id = "focus", name = "Fokustrank", icon = "🌀", color = 0xc080ff, description = "+25 % Abklingzeitreduktion für 15 s.", buff = new Dictionary<string, double> { ["cdr"] = 0.25 }, duration = 15, cooldown = 2, stack = 5, rarity = "epic" },
            ["towerTonic"] = new PotionDefinition { id = "towerTonic", name = "Turmelixier", icon = "🗼", color = 0xffc040, description = "Türme im Umkreis von 4 Feldern: +25 % Schaden und Tempo für 15 s.", tower = new PotionTowerEffect { radius = 4, dmg = 0.25, speed = 0.25 }, duration = 15, cooldown = 10, stack = 4, rarity = "epic" },
        };
        public static readonly List<string> POTION_ORDER = new List<string> { "heal", "mana", "power", "haste", "swift", "stone", "focus", "towerTonic" };

        /// <summary>Prozentuale Trankwirkungen (abhängig von den Basiswerten des Helden).</summary>
        public static readonly Dictionary<string, Dictionary<string, double>> POTION_PCT = new Dictionary<string, Dictionary<string, double>>
        {
            ["power"] = new Dictionary<string, double> { ["damage"] = 0.3 },
            ["haste"] = new Dictionary<string, double> { ["attackSpeed"] = 0.35 },
        };

        // ------------------------------------------------------------ Herstellung

        private static List<RecipeDefinition> fragmentRecipes()
        {
            List<RecipeDefinition> out_ = new List<RecipeDefinition>();
            foreach (string id in ITEM_ORDER)
            {
                if (ITEMS[id].craftOnly == true) continue;
                out_.Add(new RecipeDefinition
                {
                    id = $"frag:{id}", kind = "fragments", name = ITEMS[id].name, result = id,
                    fragments = new RecipeFragments { item = id, count = RARITY[ITEMS[id].rarity].fragments }, cost = new Dictionary<string, double>(RARITY[ITEMS[id].rarity].craftCost),
                });
            }
            return out_;
        }

        /// <summary>3.2 Heldenschmiede: Kosten und Wellen der Signaturrezepte.</summary>
        public static readonly HeroRecipeCost HERO_RECIPE_COST = new HeroRecipeCost
        {
            epic = new Dictionary<string, double> { ["metal"] = 24, ["crystal"] = 10, ["essence"] = 4 },
            legendary = new Dictionary<string, double> { ["essence"] = 8, ["relic"] = 3 },
        };
        public static readonly HeroRecipeWave HERO_RECIPE_WAVE = new HeroRecipeWave { epic = 8, legendary = 20 };

        // PORT: Die Signaturrezepte teilen sich – wie in TS – dieselben Kosten-Objekte HERO_RECIPE_COST.epic/legendary (Referenz).
        public static readonly List<RecipeDefinition> RECIPES = U.Concat(fragmentRecipes(), new List<RecipeDefinition>
        {
            new RecipeDefinition { id = "hero:lyraSpindle", kind = "hero", name = "Webspindel", result = "lyraSpindle", champion = "lyra", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:lyraStaff", kind = "hero", name = "Stab der Turmweberin", result = "lyraStaff", champion = "lyra", items = new List<string> { "lyraSpindle" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:brannSigil", kind = "hero", name = "Klingenrufer-Siegel", result = "brannSigil", champion = "brann", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:brannBlade", kind = "hero", name = "Rufklinge", result = "brannBlade", champion = "brann", items = new List<string> { "brannSigil" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:velaQuiver", kind = "hero", name = "Sturmköcher", result = "velaQuiver", champion = "vela", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:velaBow", kind = "hero", name = "Velas Sturmbogen", result = "velaBow", champion = "vela", items = new List<string> { "velaQuiver" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:kaelaHelm", kind = "hero", name = "Wächterhelm", result = "kaelaHelm", champion = "kaela", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:kaelaBlade", kind = "hero", name = "Wächterklinge", result = "kaelaBlade", champion = "kaela", items = new List<string> { "kaelaHelm" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:varoEmber", kind = "hero", name = "Aschenglut", result = "varoEmber", champion = "varo", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:varoCrossbow", kind = "hero", name = "Aschenjäger-Armbrust", result = "varoCrossbow", champion = "varo", items = new List<string> { "varoEmber" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:nyxAmulet", kind = "hero", name = "Rissamulett", result = "nyxAmulet", champion = "nyx", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:nyxDaggers", kind = "hero", name = "Rissdolche", result = "nyxDaggers", champion = "nyx", items = new List<string> { "nyxAmulet" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:lioraLute", kind = "hero", name = "Runenlaute", result = "lioraLute", champion = "liora", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:lioraHarp", kind = "hero", name = "Bardenharfe", result = "lioraHarp", champion = "liora", items = new List<string> { "lioraLute" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:thorgrimHelm", kind = "hero", name = "Schmiedehelm", result = "thorgrimHelm", champion = "thorgrim", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:thorgrimHammer", kind = "hero", name = "Meisterhammer", result = "thorgrimHammer", champion = "thorgrim", items = new List<string> { "thorgrimHelm" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:senaCrystal", kind = "hero", name = "Wetterkristall", result = "senaCrystal", champion = "sena", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:senaScepter", kind = "hero", name = "Sturmzepter", result = "senaScepter", champion = "sena", items = new List<string> { "senaCrystal" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:oranFocus", kind = "hero", name = "Leylinienfokus", result = "oranFocus", champion = "oran", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:oranStaff", kind = "hero", name = "Leystab", result = "oranStaff", champion = "oran", items = new List<string> { "oranFocus" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:miraLamp", kind = "hero", name = "Prospektorenlampe", result = "miraLamp", champion = "mira", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:miraPick", kind = "hero", name = "Goldhacke", result = "miraPick", champion = "mira", items = new List<string> { "miraLamp" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "hero:ommarHood", kind = "hero", name = "Schweigehaube", result = "ommarHood", champion = "ommar", cost = HERO_RECIPE_COST.epic, minWave = HERO_RECIPE_WAVE.epic },
            new RecipeDefinition { id = "hero:ommarStaff", kind = "hero", name = "Stab der Stille", result = "ommarStaff", champion = "ommar", items = new List<string> { "ommarHood" }, cost = HERO_RECIPE_COST.legendary, minWave = HERO_RECIPE_WAVE.legendary },
            new RecipeDefinition { id = "combine:worldBlade", kind = "combine", name = "Weltenklinge", result = "worldBlade", items = new List<string> { "dragonFang", "ironBlade" }, cost = new Dictionary<string, double> { ["relic"] = 3 }, minWave = 15, perk = "smith" },
            new RecipeDefinition { id = "combine:citadelPlate", kind = "combine", name = "Zitadellenpanzer", result = "citadelPlate", items = new List<string> { "emberPlate", "chainmail" }, cost = new Dictionary<string, double> { ["relic"] = 3 }, minWave = 15, perk = "smith" },
            new RecipeDefinition { id = "combine:coreHeart", kind = "combine", name = "Herz des Kerns", result = "coreHeart", items = new List<string> { "riftShard", "manaPrism" }, cost = new Dictionary<string, double> { ["relic"] = 2, ["essence"] = 6 }, minWave = 15, perk = "smith" },
        });

        /// <summary>Kosten der Verstärkung von +n auf +n+1.</summary>
        public static Dictionary<string, double> upgradeCost(double plus)
        {
            if (plus <= 0) return new Dictionary<string, double> { ["metal"] = 6, ["crystal"] = 2 };
            if (plus == 1) return new Dictionary<string, double> { ["metal"] = 12, ["crystal"] = 5, ["essence"] = 2 };
            return new Dictionary<string, double> { ["metal"] = 20, ["crystal"] = 10, ["essence"] = 5, ["relic"] = 1 };
        }

        // ------------------------------------------------------------ Laden

        public static readonly List<ShopOffer> SHOP_OFFERS = new List<ShopOffer>
        {
            new ShopOffer { id = "p-heal", category = "potions", name = "Heiltrank", description = POTIONS["heal"].description, cost = new Dictionary<string, double> { ["gold"] = 40 }, give = new ShopOfferGive { potion = "heal" } },
            new ShopOffer { id = "p-mana", category = "potions", name = "Manatrank", description = POTIONS["mana"].description, cost = new Dictionary<string, double> { ["gold"] = 40 }, give = new ShopOfferGive { potion = "mana" } },
            new ShopOffer { id = "p-swift", category = "potions", name = "Windtrank", description = POTIONS["swift"].description, cost = new Dictionary<string, double> { ["metal"] = 3 }, give = new ShopOfferGive { potion = "swift" } },
            new ShopOffer { id = "p-power", category = "potions", name = "Krafttrank", description = POTIONS["power"].description, cost = new Dictionary<string, double> { ["crystal"] = 3 }, give = new ShopOfferGive { potion = "power" } },
            new ShopOffer { id = "p-haste", category = "potions", name = "Eiltrank", description = POTIONS["haste"].description, cost = new Dictionary<string, double> { ["crystal"] = 3 }, give = new ShopOfferGive { potion = "haste" } },
            new ShopOffer { id = "p-stone", category = "potions", name = "Steinhaut", description = POTIONS["stone"].description, cost = new Dictionary<string, double> { ["metal"] = 5 }, give = new ShopOfferGive { potion = "stone" } },
            new ShopOffer { id = "p-focus", category = "potions", name = "Fokustrank", description = POTIONS["focus"].description, cost = new Dictionary<string, double> { ["essence"] = 2 }, perk = "alchemy", give = new ShopOfferGive { potion = "focus" } },
            new ShopOffer { id = "p-tower", category = "potions", name = "Turmelixier", description = POTIONS["towerTonic"].description, cost = new Dictionary<string, double> { ["essence"] = 3, ["gold"] = 30 }, perk = "alchemy", give = new ShopOfferGive { potion = "towerTonic" } },
            new ShopOffer { id = "f-random", category = "fragments", name = "Fragmentbündel", description = "Ein Fragment eines noch nicht freigeschalteten Items (zufällig).", cost = new Dictionary<string, double> { ["crystal"] = 5 }, stock = 3, give = new ShopOfferGive { randomFragment = 1 } },
            new ShopOffer { id = "f-random3", category = "fragments", name = "Großes Bündel", description = "Drei Fragmente noch fehlender Items.", cost = new Dictionary<string, double> { ["crystal"] = 10, ["essence"] = 3 }, stock = 1, give = new ShopOfferGive { randomFragment = 3 } },
            new ShopOffer { id = "m-metal", category = "materials", name = "5 Metall", description = "Gold gegen Metall tauschen.", cost = new Dictionary<string, double> { ["gold"] = 60 }, give = new ShopOfferGive { resource = new Dictionary<string, double> { ["metal"] = 5 } } },
            new ShopOffer { id = "m-crystal", category = "materials", name = "3 Kristall", description = "Gold gegen Kristall tauschen.", cost = new Dictionary<string, double> { ["gold"] = 90 }, give = new ShopOfferGive { resource = new Dictionary<string, double> { ["crystal"] = 3 } } },
            new ShopOffer { id = "m-essence", category = "materials", name = "1 Essenz", description = "Metall und Kristall veredeln.", cost = new Dictionary<string, double> { ["metal"] = 8, ["crystal"] = 3 }, stock = 5, give = new ShopOfferGive { resource = new Dictionary<string, double> { ["essence"] = 1 } } },
            new ShopOffer { id = "m-relic", category = "materials", name = "1 Relikt", description = "Essenz zu einem Relikt verdichten.", cost = new Dictionary<string, double> { ["essence"] = 8 }, stock = 1, perk = "trader", give = new ShopOfferGive { resource = new Dictionary<string, double> { ["relic"] = 1 } } },
            new ShopOffer { id = "s-tome", category = "special", name = "Fertigkeitsfoliant", description = "+1 Fertigkeitspunkt für den Helden.", cost = new Dictionary<string, double> { ["essence"] = 12 }, stock = 1, perk = "trader", give = new ShopOfferGive { skillPoint = 1 } },
        };

        public static readonly List<ShopCategoryDefinition> SHOP_CATEGORIES = new List<ShopCategoryDefinition>
        {
            new ShopCategoryDefinition { id = "potions", name = "Tränke" },
            new ShopCategoryDefinition { id = "collection", name = "Ausrüstung" },
            new ShopCategoryDefinition { id = "fragments", name = "Fragmente" },
            new ShopCategoryDefinition { id = "materials", name = "Materialien" },
            new ShopCategoryDefinition { id = "special", name = "Spezial" },
        };

        /// <summary>Verkaufserlös eines Items (in Metall/Kristall, 40 % des Ladenpreises).</summary>
        public static Dictionary<string, double> sellValue(string id, double plus)
        {
            ItemDefinition d = null;
            if (id != null) ITEMS.TryGetValue(id, out d);
            Dictionary<string, double> r = RARITY[d?.rarity ?? "common"].shopCost;
            return new Dictionary<string, double>
            {
                ["metal"] = Math.Floor((U.GetN(r, "metal") ?? 0) * 0.4) + plus * 2,
                ["crystal"] = Math.Floor((U.GetN(r, "crystal") ?? 0) * 0.4),
            };
        }
    }
}

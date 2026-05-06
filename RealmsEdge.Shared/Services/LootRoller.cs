using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Shared.Services
{
    /// <summary>
    /// Rolls item and currency drops for defeated enemies.
    /// Guaranteed drops always fall through.
    /// Random item generation from tables comes with the shop system.
    /// </summary>
    public static class LootRoller
    {
        private static readonly Random _rng = new();

        // =====================
        // Main Entry Point
        // =====================

        public static LootResult Roll(
            IEnumerable<NpcCharacter> defeatedEnemies)
        {
            var result = new LootResult();

            foreach (var enemy in defeatedEnemies)
            {
                // Always pass through guaranteed drops
                result.Items.AddRange(enemy.GuaranteedDrops);

                // Roll currency based on loot table tier
                var (gold, silver, copper) =
                    RollCurrency(enemy.LootTable, enemy.Level);

                result.Gold   += gold;
                result.Silver += silver;
                result.Copper += copper;

                // Roll for a random item drop
                var drop = RollItemDrop(
                    enemy.LootTable, enemy.Level);

                if (drop != null)
                    result.Items.Add(drop);
            }

            // Normalise currency
            // (carry copper → silver → gold)
            NormaliseCurrency(result);

            return result;
        }

        // =====================
        // Currency Rolling
        // =====================

        private static (int Gold, int Silver, int Copper)
            RollCurrency(LootTable table, int level)
        {
            var lvl = Math.Max(1, level);

            return table switch
            {
                LootTable.None => (0, 0, 0),

                LootTable.Poor => (
                    0,
                    0,
                    Roll(lvl, 3)),          // 1d3 × level copper

                LootTable.Common => (
                    0,
                    Roll(lvl, 4),           // 1d4 × level silver
                    Roll(lvl, 6)),

                LootTable.Uncommon => (
                    Roll(1, 4),             // 1d4 gold
                    Roll(lvl, 6),
                    0),

                LootTable.Rare => (
                    Roll(lvl, 4),           // level × d4 gold
                    Roll(lvl, 6),
                    0),

                LootTable.Elite => (
                    Roll(lvl, 6),           // level × d6 gold
                    Roll(lvl, 8),
                    0),

                LootTable.Boss => (
                    Roll(lvl, 10),          // level × d10 gold
                    Roll(lvl, 6),
                    0),

                LootTable.Legendary => (
                    Roll(lvl, 20),          // level × d20 gold
                    0,
                    0),

                _ => (0, 0, 0)
            };
        }

        // =====================
        // Item Drop Rolling
        // =====================

        private static InventoryItem? RollItemDrop(
            LootTable table, int level)
        {
            // Drop chance per tier
            var chance = table switch
            {
                LootTable.None => 0,
                LootTable.Poor => 5,
                LootTable.Common => 15,
                LootTable.Uncommon => 30,
                LootTable.Rare => 50,
                LootTable.Elite => 70,
                LootTable.Boss => 90,
                LootTable.Legendary => 100,
                _ => 0
            };

            if (_rng.Next(100) >= chance)
                return null;

            // Minimum rarity per tier
            var rarity = table switch
            {
                LootTable.Poor => ItemRarity.Common,
                LootTable.Common => ItemRarity.Common,
                LootTable.Uncommon => ItemRarity.Uncommon,
                LootTable.Rare => ItemRarity.Rare,
                LootTable.Elite => RollRarity(
                    rare: 70, epic: 30),
                LootTable.Boss => RollRarity(
                    rare: 50, epic: 40, legendary: 10),
                LootTable.Legendary => RollRarity(
                    epic: 60, legendary: 40),
                _ => ItemRarity.Common
            };

            // Generic loot item — named items come with
            // the shop/item database in the next feature
            return new InventoryItem
            {
                Name        = GenerateName(rarity, level),
                Description = "Found on a defeated enemy.",
                Rarity      = rarity,
                Type    = RollItemType(),
                BaseValueInCopper       = BaseValue(rarity) * level,
                Weight      = 1f,
                StackSize = 1,
            };
        }

        // =====================
        // Helpers
        // =====================

        private static int Roll(int times, int sides)
        {
            var total = 0;
            for (var i = 0; i < times; i++)
                total += _rng.Next(1, sides + 1);
            return total;
        }

        private static ItemRarity RollRarity(
            int common = 0,
            int uncommon = 0,
            int rare = 0,
            int epic = 0,
            int legendary = 0)
        {
            var roll = _rng.Next(
                common + uncommon + rare + epic + legendary);

            if (roll < common) return ItemRarity.Common;
            if (roll < common + uncommon) return ItemRarity.Uncommon;
            if (roll < common + uncommon + rare) return ItemRarity.Rare;
            if (roll < common + uncommon + rare + epic) return ItemRarity.Epic;
            return ItemRarity.Legendary;
        }

        private static ItemType RollItemType()
        {
            var roll = _rng.Next(100);
            if (roll < 40) return ItemType.Weapon;
            if (roll < 65) return ItemType.Armour;
            if (roll < 80) return ItemType.Potion;
            if (roll < 90) return ItemType.Gem;
            return ItemType.Misc;
        }

        private static string GenerateName(
            ItemRarity rarity, int level)
        {
            var prefix = rarity switch
            {
                ItemRarity.Common => _commonPrefixes[
                    _rng.Next(_commonPrefixes.Length)],
                ItemRarity.Uncommon => _uncommonPrefixes[
                    _rng.Next(_uncommonPrefixes.Length)],
                ItemRarity.Rare => _rarePrefixes[
                    _rng.Next(_rarePrefixes.Length)],
                ItemRarity.Epic => _epicPrefixes[
                    _rng.Next(_epicPrefixes.Length)],
                ItemRarity.Legendary => _legendaryPrefixes[
                    _rng.Next(_legendaryPrefixes.Length)],
                _ => "Worn"
            };

            var item = _itemNames[
                _rng.Next(_itemNames.Length)];

            return level >= 10
                ? $"{prefix} {item} of Power"
                : $"{prefix} {item}";
        }

        private static int BaseValue(ItemRarity rarity) =>
            rarity switch
            {
                ItemRarity.Common => 5,
                ItemRarity.Uncommon => 25,
                ItemRarity.Rare => 100,
                ItemRarity.Epic => 500,
                ItemRarity.Legendary => 2500,
                _ => 1
            };

        private static void NormaliseCurrency(LootResult r)
        {
            r.Silver += r.Copper / 10;
            r.Copper  = r.Copper % 10;
            r.Gold   += r.Silver / 10;
            r.Silver  = r.Silver % 10;
        }

        // =====================
        // Name Tables
        // =====================

        private static readonly string[] _commonPrefixes =
        {
            "Worn", "Battered", "Cracked", "Crude", "Rusty"
        };

        private static readonly string[] _uncommonPrefixes =
        {
            "Sturdy", "Reinforced", "Polished",
            "Engraved", "Balanced"
        };

        private static readonly string[] _rarePrefixes =
        {
            "Masterwork", "Enchanted", "Tempered",
            "Runic", "Shadowforged"
        };

        private static readonly string[] _epicPrefixes =
        {
            "Ancient", "Dread", "Celestial",
            "Infernal", "Abyssal"
        };

        private static readonly string[] _legendaryPrefixes =
        {
            "Mythic", "Godforged", "Eternal",
            "Dragonbone", "Soulbound"
        };

        private static readonly string[] _itemNames =
        {
            "Sword",   "Axe",      "Dagger",   "Mace",
            "Shield",  "Helmet",   "Gauntlets","Boots",
            "Ring",    "Amulet",   "Cloak",    "Belt",
            "Bow",     "Quiver",   "Staff",    "Wand",
            "Tome",    "Vial",     "Pouch",    "Bracer"
        };
    }

    // =====================
    // Result DTO
    // =====================

    public class LootResult
    {
        public int Gold { get; set; }
        public int Silver { get; set; }
        public int Copper { get; set; }
        public List<InventoryItem> Items { get; set; } = new();

        public bool HasAnything =>
            Gold > 0 || Silver > 0 ||
            Copper > 0 || Items.Any();

        public string CurrencyDisplay
        {
            get
            {
                var parts = new List<string>();
                if (Gold   > 0) parts.Add($"{Gold}g");
                if (Silver > 0) parts.Add($"{Silver}s");
                if (Copper > 0) parts.Add($"{Copper}c");
                return parts.Any()
                    ? string.Join(" ", parts)
                    : "Nothing";
            }
        }
    }
}
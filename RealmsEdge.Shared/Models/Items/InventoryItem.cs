using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;

namespace RealmsEdge.Shared.Models.Items
{
    public enum ItemType
    {
        Weapon,         // Swords, axes, bows, staffs
        Armour,         // Plate, leather, robes, shields
        Potion,         // Healing, mana, stat buffs
        Scroll,         // One use spells
        Food,           // Restores stamina, minor healing
        Key,            // Opens doors, chests, gates
        Quest,          // Cannot be dropped or sold
        Gold,           // Currency item
        Gem,            // High value trade item
        Misc            // Everything else
    }

    public enum ItemRarity
    {
        Common,         // Grey  — basic gear, found everywhere
        Uncommon,       // Green — better than average
        Rare,           // Blue  — hard to find, good stats
        Epic,           // Purple — very powerful, named items
        Legendary,      // Orange — unique items, game changers
        Artifact        // Red   — ancient, world altering power
    }

    public enum WeaponType
    {
        None,           // Not a weapon
        Sword,
        Axe,
        Mace,
        Dagger,
        Staff,
        Bow,
        Crossbow,
        Spear,
        Fists,          // Monk unarmed
        Wand            // Caster focus
    }

    public class InventoryItem
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IconPath { get; set; }           // UI icon image path
        public ItemType Type { get; set; }
        public ItemRarity Rarity { get; set; } = ItemRarity.Common;
        public WeaponType WeaponType { get; set; } = WeaponType.None;

        // =====================
        // Physical Properties
        // =====================

        public double Weight { get; set; } = 0.5;      // In kg
        public int StackSize { get; set; } = 1;         // How many in this stack
        public int MaxStackSize { get; set; } = 1;      // Max stack (potions = 10, weapons = 1)
        public bool IsStackable => MaxStackSize > 1;

        // =====================
        // Value
        // =====================

        public int BaseValueInCopper { get; set; } = 1;
        public int SellValueInCopper => (int)(BaseValueInCopper * 0.6);  // 60% sell back

        public string DisplayValue => FormatCurrency(BaseValueInCopper);
        public string DisplaySellValue => FormatCurrency(SellValueInCopper);

        // =====================
        // Combat Stats (Weapons)
        // =====================

        public DiceType? DamageDie { get; set; }        // D6 for sword, D8 for axe etc
        public int DamageBonus { get; set; } = 0;       // Flat damage bonus
        public int AttackBonus { get; set; } = 0;       // To hit bonus
        public int CriticalMultiplier { get; set; } = 2; // Default x2 crit damage
        public bool IsTwoHanded { get; set; } = false;
        public bool IsRanged { get; set; } = false;
        public int Range { get; set; } = 1;             // Tiles, 1 = melee

        // =====================
        // Defence Stats (Armour)
        // =====================

        public int ArmourBonus { get; set; } = 0;
        public int DodgeBonus { get; set; } = 0;

        // =====================
        // Stat Modifiers (Equipment bonuses)
        // =====================

        public int StrengthBonus { get; set; } = 0;
        public int DexterityBonus { get; set; } = 0;
        public int ConstitutionBonus { get; set; } = 0;
        public int IntelligenceBonus { get; set; } = 0;
        public int WisdomBonus { get; set; } = 0;
        public int CharismaBonus { get; set; } = 0;
        public int LuckBonus { get; set; } = 0;

        // =====================
        // Resistance Bonuses
        // =====================

        public int ResistPoisonBonus { get; set; } = 0;
        public int ResistMagicBonus { get; set; } = 0;
        public int ResistFearBonus { get; set; } = 0;

        // =====================
        // Potion / Consumable Effects
        // =====================

        public int HealAmount { get; set; } = 0;
        public int ManaRestoreAmount { get; set; } = 0;
        public int StaminaRestoreAmount { get; set; } = 0;
        public CharacterStatus? AppliesStatus { get; set; }     // Status it applies
        public CharacterStatus? CuresStatus { get; set; }       // Status it cures
        public int StatusDuration { get; set; } = 0;            // Turns status lasts

        // =====================
        // Item Flags
        // =====================

        public bool IsEquipped { get; set; } = false;
        public bool IsQuestItem { get; set; } = false;          // Cannot drop or sell
        public bool IsIdentified { get; set; } = true;          // Unidentified = stats hidden
        public bool IsCursed { get; set; } = false;             // Cannot unequip once equipped
        public bool RequiresTwoHands { get; set; } = false;

        // =====================
        // Class / Race Requirements
        // =====================

        public List<CharacterClass> RequiredClasses { get; set; } = new();
        public List<CharacterRace> RequiredRaces { get; set; } = new();
        public int RequiredLevel { get; set; } = 1;
        public int RequiredStrength { get; set; } = 0;
        public int RequiredDexterity { get; set; } = 0;
        public int RequiredIntelligence { get; set; } = 0;

        // =====================
        // Helpers
        // =====================

        public bool CanBeUsedBy(CharacterBase character)
        {
            if (character.Level < RequiredLevel) return false;
            if (RequiredStrength > 0 && character.Stats.Strength < RequiredStrength) return false;
            if (RequiredDexterity > 0 && character.Stats.Dexterity < RequiredDexterity) return false;
            if (RequiredIntelligence > 0 && character.Stats.Intelligence < RequiredIntelligence) return false;
            if (RequiredClasses.Any() && !RequiredClasses.Contains(character.Class)) return false;
            if (RequiredRaces.Any() && !RequiredRaces.Contains(character.Race)) return false;
            return true;
        }

        public string RarityColour => Rarity switch
        {
            ItemRarity.Common => "#aaaaaa",
            ItemRarity.Uncommon => "#00aa00",
            ItemRarity.Rare => "#0070dd",
            ItemRarity.Epic => "#a335ee",
            ItemRarity.Legendary => "#ff8000",
            ItemRarity.Artifact => "#e6cc80",
            _ => "#ffffff"
        };

        public string DamageDisplay => DamageDie.HasValue
            ? $"{DamageDie}+{DamageBonus}"
            : "—";

        private static string FormatCurrency(int copper)
        {
            var gold = copper / 100;
            var silver = (copper % 100) / 10;
            var cop = copper % 10;

            var parts = new List<string>();
            if (gold > 0) parts.Add($"{gold}g");
            if (silver > 0) parts.Add($"{silver}s");
            if (cop > 0) parts.Add($"{cop}c");

            return parts.Any() ? string.Join(" ", parts) : "0c";
        }
    }
}
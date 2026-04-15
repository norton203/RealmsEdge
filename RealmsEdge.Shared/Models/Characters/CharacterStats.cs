using RealmsEdge.Shared.Enums;

namespace RealmsEdge.Shared.Models.Characters
{
    public class CharacterStats
    {
        // =====================
        // Core Six Stats (Rolled on creation)
        // =====================

        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        // =====================
        // Core Stat Modifiers (Calculated from core stats)
        // D&D formula: (stat - 10) / 2, rounded down
        // =====================

        public int StrengthModifier => CalculateModifier(Strength);
        public int DexterityModifier => CalculateModifier(Dexterity);
        public int ConstitutionModifier => CalculateModifier(Constitution);
        public int IntelligenceModifier => CalculateModifier(Intelligence);
        public int WisdomModifier => CalculateModifier(Wisdom);
        public int CharismaModifier => CalculateModifier(Charisma);

        // =====================
        // Derived Stats (Calculated from modifiers + level)
        // =====================

        public int MaxHitPoints { get; set; }       // Set on level up using hit die
        public int CurrentHitPoints { get; set; }   // Changes during combat
        public int MaxMana { get; set; }             // Set on level up
        public int CurrentMana { get; set; }         // Changes as spells are cast
        public int MaxStamina { get; set; }          // Set on level up
        public int CurrentStamina { get; set; }      // Changes during combat

        public int ArmourClass => 10 + DexterityModifier + ArmorBonus;
        public int Initiative => DexterityModifier;
        public int Speed { get; set; } = 6;  // Tiles per turn

        // =====================
        // Bonus Stats (From equipment, buffs, race, class)
        // =====================

        public int ArmorBonus { get; set; } = 0;
        public int AttackBonus { get; set; } = 0;
        public int RangedBonus { get; set; } = 0;
        public int SpellBonus { get; set; } = 0;
        public int CriticalChanceBonus { get; set; } = 0;
        public int DodgeBonus { get; set; } = 0;
        public int Luck { get; set; } = 0;  // L.O.R.D inspired

        // =====================
        // Resistance Stats (0-100, percentage based)
        // =====================

        public int ResistPoison { get; set; } = 0;
        public int ResistMagic { get; set; } = 0;
        public int ResistFear { get; set; } = 0;

        // =====================
        // Combat Calculated Properties
        // =====================

        public int MeleeAttackBonus => StrengthModifier + AttackBonus;
        public int RangedAttackBonus => DexterityModifier + RangedBonus;
        public int SpellAttackBonus => IntelligenceModifier + WisdomModifier + SpellBonus;
        public int CriticalChance => 5 + CriticalChanceBonus;    // Base 5% + bonuses
        public int DodgeChance => DexterityModifier + DodgeBonus;

        // =====================
        // Health Helpers
        // =====================

        public bool IsAlive => CurrentHitPoints > 0;
        public bool IsUnconscious => CurrentHitPoints <= 0 && CurrentHitPoints > -10;
        public bool IsDead => CurrentHitPoints <= -10;
        public bool IsFullHealth => CurrentHitPoints >= MaxHitPoints;
        public bool IsBloodied => CurrentHitPoints <= MaxHitPoints / 2; // D&D term for half HP

        public double HealthPercent => MaxHitPoints > 0
                                        ? (double)CurrentHitPoints / MaxHitPoints * 100
                                        : 0;

        // =====================
        // Mana Helpers
        // =====================

        public bool HasMana => CurrentMana > 0;
        public double ManaPercent => MaxMana > 0
                                        ? (double)CurrentMana / MaxMana * 100
                                        : 0;

        // =====================
        // Stamina Helpers
        // =====================

        public bool HasStamina => CurrentStamina > 0;
        public double StaminaPercent => MaxStamina > 0
                                        ? (double)CurrentStamina / MaxStamina * 100
                                        : 0;

        // =====================
        // Stat Methods
        // =====================

        // Standard D&D modifier formula
        private static int CalculateModifier(int stat) => (int)Math.Floor((stat - 10) / 2.0);

        // Get any core stat by enum — useful for skill checks
        public int GetStat(CharacterStat stat) => stat switch
        {
            CharacterStat.Strength => Strength,
            CharacterStat.Dexterity => Dexterity,
            CharacterStat.Constitution => Constitution,
            CharacterStat.Intelligence => Intelligence,
            CharacterStat.Wisdom => Wisdom,
            CharacterStat.Charisma => Charisma,
            CharacterStat.HitPoints => CurrentHitPoints,
            CharacterStat.Mana => CurrentMana,
            CharacterStat.Stamina => CurrentStamina,
            CharacterStat.ArmourClass => ArmourClass,
            CharacterStat.Initiative => Initiative,
            CharacterStat.Speed => Speed,
            CharacterStat.Luck => Luck,
            _ => 0
        };

        // Get modifier by enum — useful for dice roll adjustments
        public int GetModifier(CharacterStat stat) => stat switch
        {
            CharacterStat.Strength => StrengthModifier,
            CharacterStat.Dexterity => DexterityModifier,
            CharacterStat.Constitution => ConstitutionModifier,
            CharacterStat.Intelligence => IntelligenceModifier,
            CharacterStat.Wisdom => WisdomModifier,
            CharacterStat.Charisma => CharismaModifier,
            _ => 0
        };

        // Apply damage, respects unconscious threshold
        public void ApplyDamage(int amount)
        {
            CurrentHitPoints = Math.Max(-10, CurrentHitPoints - amount);
        }

        // Apply healing, never exceeds max
        public void ApplyHealing(int amount)
        {
            CurrentHitPoints = Math.Min(MaxHitPoints, CurrentHitPoints + amount);
        }

        // Spend mana, returns false if not enough
        public bool SpendMana(int amount)
        {
            if (CurrentMana < amount) return false;
            CurrentMana -= amount;
            return true;
        }

        // Spend stamina, returns false if not enough
        public bool SpendStamina(int amount)
        {
            if (CurrentStamina < amount) return false;
            CurrentStamina -= amount;
            return true;
        }

        // Restore all resources to max — used on full rest
        public void RestoreFull()
        {
            CurrentHitPoints = MaxHitPoints;
            CurrentMana = MaxMana;
            CurrentStamina = MaxStamina;
        }

        // Restore partial resources — used on short rest
        public void RestorePartial(int hitPoints, int mana, int stamina)
        {
            ApplyHealing(hitPoints);
            CurrentMana     = Math.Min(MaxMana, CurrentMana + mana);
            CurrentStamina  = Math.Min(MaxStamina, CurrentStamina + stamina);
        }
    }
}
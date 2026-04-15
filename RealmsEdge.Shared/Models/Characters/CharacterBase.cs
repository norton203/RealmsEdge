using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Items;
using RealmsEdge.Shared.Services;

namespace RealmsEdge.Shared.Models.Characters
{
    public abstract class CharacterBase
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Title { get; set; }              // e.g. "the Brave", "Shadowbane"
        public string? Description { get; set; }        // Flavour text shown in UI
        public string? PortraitPath { get; set; }       // Path to character portrait image

        public CharacterRace Race { get; set; }
        public CharacterClass Class { get; set; }
        public Alignment Alignment { get; set; }
        public Gender Gender { get; set; }

        // =====================
        // Progression
        // =====================

        public int Level { get; set; } = 1;
        public long ExperiencePoints { get; set; } = 0;
        public long ExperienceToNextLevel => CalculateNextLevelXp();

        // =====================
        // Stats
        // =====================

        public CharacterStats Stats { get; set; } = new();

        // =====================
        // Status Effects
        // =====================

        public List<CharacterStatus> ActiveStatuses { get; set; } = new();

        public bool HasStatus(CharacterStatus status)
            => ActiveStatuses.Contains(status);

        public void ApplyStatus(CharacterStatus status)
        {
            if (!ActiveStatuses.Contains(status))
                ActiveStatuses.Add(status);
        }

        public void RemoveStatus(CharacterStatus status)
            => ActiveStatuses.Remove(status);

        public void ClearAllStatuses()
            => ActiveStatuses.Clear();

        // =====================
        // Inventory
        // =====================

        public List<InventoryItem> Inventory { get; set; } = new();
        public int MaxCarryWeight => 10 + (Stats.StrengthModifier * 5);
        public double CurrentCarryWeight => Inventory.Sum(i => i.Weight);
        public bool IsEncumbered => CurrentCarryWeight > MaxCarryWeight;

        public bool AddItem(InventoryItem item)
        {
            if (CurrentCarryWeight + item.Weight > MaxCarryWeight)
                return false;
            Inventory.Add(item);
            return true;
        }

        public bool RemoveItem(Guid itemId)
        {
            var item = Inventory.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            Inventory.Remove(item);
            return true;
        }

        // =====================
        // Currency
        // =====================

        public int Gold { get; set; } = 0;
        public int Silver { get; set; } = 0;
        public int Copper { get; set; } = 0;

        public int TotalWealthInCopper => (Gold * 100) + (Silver * 10) + Copper;

        public bool CanAfford(int copperCost)
            => TotalWealthInCopper >= copperCost;

        public bool SpendCurrency(int copperCost)
        {
            if (!CanAfford(copperCost)) return false;
            var remaining = TotalWealthInCopper - copperCost;
            Gold   = remaining / 100;
            Silver = (remaining % 100) / 10;
            Copper = remaining % 10;
            return true;
        }

        public void AddCurrency(int gold, int silver, int copper)
        {
            var total = TotalWealthInCopper + (gold * 100) + (silver * 10) + copper;
            Gold   = total / 100;
            Silver = (total % 100) / 10;
            Copper = total % 10;
        }

        // =====================
        // Pronoun Helper
        // =====================

        public string PronounSubject => Gender switch
        {
            Gender.Male => "He",
            Gender.Female => "She",
            Gender.NonBinary => "They",
            _ => Name
        };

        public string PronounObject => Gender switch
        {
            Gender.Male => "Him",
            Gender.Female => "Her",
            Gender.NonBinary => "Them",
            _ => Name
        };

        public string PronounPossessive => Gender switch
        {
            Gender.Male => "His",
            Gender.Female => "Her",
            Gender.NonBinary => "Their",
            _ => $"{Name}'s"
        };

        // =====================
        // Display Helpers
        // =====================

        public string FullTitle => string.IsNullOrEmpty(Title)
            ? Name
            : $"{Name} {Title}";

        public string RaceClassName => $"{Race} {Class}";

        public string HealthStatus => Stats.HealthPercent switch
        {
            >= 100 => "Unharmed",
            >= 75 => "Lightly Wounded",
            >= 50 => "Wounded",
            >= 25 => "Heavily Wounded",
            > 0 => "Near Death",
            _ => Stats.IsUnconscious ? "Unconscious" : "Dead"
        };

        // =====================
        // Levelling
        // =====================

        public bool CanLevelUp => ExperiencePoints >= ExperienceToNextLevel;

        public virtual void LevelUp(DiceService diceService)
        {
            if (!CanLevelUp) return;
            Level++;

            // Roll hit die for this class
            var hitDie = GetClassHitDie();
            var hpRoll = diceService.Roll(hitDie, 1, Stats.ConstitutionModifier);

            // Minimum 1 HP gain per level
            var hpGain = Math.Max(1, hpRoll.Total);
            Stats.MaxHitPoints += hpGain;
            Stats.CurrentHitPoints += hpGain;

            // Increase mana and stamina on level up
            Stats.MaxMana += Stats.IntelligenceModifier + Stats.WisdomModifier;
            Stats.MaxStamina += Stats.ConstitutionModifier;
        }

        public void AddExperience(long amount)
            => ExperiencePoints += amount;

        // Classic XP curve — each level requires significantly more XP
        private long CalculateNextLevelXp() => Level switch
        {
            1 => 1000,
            2 => 2500,
            3 => 5000,
            4 => 10000,
            5 => 20000,
            6 => 35000,
            7 => 55000,
            8 => 80000,
            9 => 110000,
            10 => 150000,
            11 => 200000,
            12 => 260000,
            13 => 330000,
            14 => 410000,
            15 => 500000,
            16 => 600000,
            17 => 710000,
            18 => 830000,
            19 => 960000,
            _ => long.MaxValue    // Level 20 is max
        };

        // Returns the hit die for each class
        private DiceType GetClassHitDie() => Class switch
        {
            CharacterClass.Barbarian => DiceType.D12,
            CharacterClass.Fighter   or
            CharacterClass.Paladin   or
            CharacterClass.Ranger    or
            CharacterClass.Witchhunter or
            CharacterClass.BountyHunter => DiceType.D10,
            CharacterClass.Rogue     or
            CharacterClass.Bard      or
            CharacterClass.Monk      or
            CharacterClass.Assassin  or
            CharacterClass.Warlock   or
            CharacterClass.Cleric    or
            CharacterClass.Druid     or
            CharacterClass.Shaman    or
            CharacterClass.Chaos => DiceType.D8,
            _ => DiceType.D6
        };

        // =====================
        // Race Stat Bonuses
        // Applied on character creation
        // =====================

        public virtual void ApplyRaceBonuses()
        {
            switch (Race)
            {
                case CharacterRace.Human:
                    // Humans get +1 to all stats — versatile
                    Stats.Strength++;
                    Stats.Dexterity++;
                    Stats.Constitution++;
                    Stats.Intelligence++;
                    Stats.Wisdom++;
                    Stats.Charisma++;
                    Stats.Luck += 2;
                    break;

                case CharacterRace.Elf:
                    Stats.Dexterity += 2;
                    Stats.Intelligence += 2;
                    Stats.Constitution -= 1;
                    Stats.Speed = 7;
                    break;

                case CharacterRace.Dwarf:
                    Stats.Constitution += 2;
                    Stats.Strength += 1;
                    Stats.Charisma -= 1;
                    Stats.ResistPoison += 25;
                    break;

                case CharacterRace.Halfling:
                    Stats.Dexterity += 2;
                    Stats.Charisma += 1;
                    Stats.Strength -= 2;
                    Stats.Luck += 5;
                    break;

                case CharacterRace.Gnome:
                    Stats.Intelligence += 2;
                    Stats.Dexterity += 1;
                    Stats.Strength -= 2;
                    Stats.SpellBonus += 2;
                    break;

                case CharacterRace.HalfElf:
                    Stats.Dexterity += 1;
                    Stats.Intelligence += 1;
                    Stats.Charisma += 2;
                    break;

                case CharacterRace.HalfOrc:
                    Stats.Strength += 2;
                    Stats.Constitution += 1;
                    Stats.Intelligence -= 1;
                    Stats.Charisma -= 1;
                    break;

                case CharacterRace.Orc:
                    Stats.Strength += 4;
                    Stats.Constitution += 2;
                    Stats.Intelligence -= 2;
                    Stats.Charisma -= 2;
                    break;

                case CharacterRace.Goblin:
                    Stats.Dexterity += 3;
                    Stats.Strength -= 2;
                    Stats.Constitution -= 1;
                    Stats.Luck += 3;
                    break;

                case CharacterRace.Dragonborn:
                    Stats.Strength += 2;
                    Stats.Constitution += 1;
                    Stats.ResistMagic += 10;
                    break;

                case CharacterRace.Tiefling:
                    Stats.Intelligence += 2;
                    Stats.Charisma += 1;
                    Stats.ResistMagic += 15;
                    Stats.SpellBonus += 2;
                    break;

                case CharacterRace.Aasimar:
                    Stats.Charisma += 2;
                    Stats.Wisdom += 1;
                    Stats.ResistFear += 25;
                    Stats.SpellBonus += 2;
                    break;

                case CharacterRace.Undead:
                    Stats.Constitution = 0;
                    Stats.ResistPoison = 100;
                    Stats.ResistFear = 100;
                    Stats.SpellBonus += 3;
                    break;

                case CharacterRace.Vampire:
                    Stats.Charisma += 3;
                    Stats.Dexterity += 2;
                    Stats.ResistMagic += 20;
                    Stats.Speed = 8;
                    break;

                case CharacterRace.Demon:
                    Stats.Strength += 3;
                    Stats.Intelligence += 2;
                    Stats.ResistMagic += 30;
                    Stats.ResistFear = 100;
                    break;
            }
        }
    }
}
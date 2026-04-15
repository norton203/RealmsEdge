using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Items;
using RealmsEdge.Shared.Services;

namespace RealmsEdge.Shared.Models.Characters
{
    public class PlayerCharacter : CharacterBase
    {
        // =====================
        // Player Identity
        // =====================

        public string PlayerName { get; set; } = string.Empty;     // Real player name
        public string? AvatarPath { get; set; }                     // Player avatar image
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;

        // =====================
        // Party
        // =====================

        public Guid? PartyId { get; set; }                          // Null if solo
        public bool IsPartyLeader { get; set; } = false;
        public bool IsOnline { get; set; } = false;

        // =====================
        // Skills
        // =====================

        public Dictionary<string, int> Skills { get; set; } = new();
        public int AvailableSkillPoints { get; set; } = 0;

        public bool HasSkill(string skillName)
            => Skills.ContainsKey(skillName) && Skills[skillName] > 0;

        public int GetSkillLevel(string skillName)
            => Skills.TryGetValue(skillName, out var level) ? level : 0;

        public bool LearnSkill(string skillName, int pointCost = 1)
        {
            if (AvailableSkillPoints < pointCost) return false;
            Skills[skillName] = GetSkillLevel(skillName) + 1;
            AvailableSkillPoints -= pointCost;
            return true;
        }

        // =====================
        // Equipment Slots
        // =====================

        public InventoryItem? EquippedWeapon { get; set; }
        public InventoryItem? EquippedOffhand { get; set; }         // Shield or second weapon
        public InventoryItem? EquippedHelmet { get; set; }
        public InventoryItem? EquippedChest { get; set; }
        public InventoryItem? EquippedLegs { get; set; }
        public InventoryItem? EquippedBoots { get; set; }
        public InventoryItem? EquippedGloves { get; set; }
        public InventoryItem? EquippedRing1 { get; set; }
        public InventoryItem? EquippedRing2 { get; set; }
        public InventoryItem? EquippedAmulet { get; set; }

        public IEnumerable<InventoryItem> AllEquipped => new[]
        {
            EquippedWeapon, EquippedOffhand, EquippedHelmet,
            EquippedChest, EquippedLegs, EquippedBoots,
            EquippedGloves, EquippedRing1, EquippedRing2,
            EquippedAmulet
        }.Where(i => i != null)!;

        // =====================
        // Equipment Methods
        // =====================

        public bool EquipItem(InventoryItem item)
        {
            if (!item.CanBeUsedBy(this)) return false;

            switch (item.Type)
            {
                case ItemType.Weapon:
                    UnequipItem(EquippedWeapon);
                    EquippedWeapon = item;
                    break;
                case ItemType.Armour when item.ArmourBonus > 0 && item.DodgeBonus == 0:
                    UnequipItem(EquippedChest);
                    EquippedChest = item;
                    break;
                default:
                    return false;
            }

            item.IsEquipped = true;
            ApplyItemBonuses(item);
            return true;
        }

        public bool UnequipItem(InventoryItem? item)
        {
            if (item == null) return false;
            if (item.IsCursed) return false;            // Cursed items cannot be removed

            RemoveItemBonuses(item);
            item.IsEquipped = false;
            return true;
        }

        private void ApplyItemBonuses(InventoryItem item)
        {
            Stats.Strength      += item.StrengthBonus;
            Stats.Dexterity     += item.DexterityBonus;
            Stats.Constitution  += item.ConstitutionBonus;
            Stats.Intelligence  += item.IntelligenceBonus;
            Stats.Wisdom        += item.WisdomBonus;
            Stats.Charisma      += item.CharismaBonus;
            Stats.ArmorBonus    += item.ArmourBonus;
            Stats.AttackBonus   += item.AttackBonus;
            Stats.DodgeBonus    += item.DodgeBonus;
            Stats.Luck          += item.LuckBonus;
            Stats.ResistPoison  += item.ResistPoisonBonus;
            Stats.ResistMagic   += item.ResistMagicBonus;
            Stats.ResistFear    += item.ResistFearBonus;
        }

        private void RemoveItemBonuses(InventoryItem item)
        {
            Stats.Strength      -= item.StrengthBonus;
            Stats.Dexterity     -= item.DexterityBonus;
            Stats.Constitution  -= item.ConstitutionBonus;
            Stats.Intelligence  -= item.IntelligenceBonus;
            Stats.Wisdom        -= item.WisdomBonus;
            Stats.Charisma      -= item.CharismaBonus;
            Stats.ArmorBonus    -= item.ArmourBonus;
            Stats.AttackBonus   -= item.AttackBonus;
            Stats.DodgeBonus    -= item.DodgeBonus;
            Stats.Luck          -= item.LuckBonus;
            Stats.ResistPoison  -= item.ResistPoisonBonus;
            Stats.ResistMagic   -= item.ResistMagicBonus;
            Stats.ResistFear    -= item.ResistFearBonus;
        }

        // =====================
        // Quests
        // =====================

        public List<Guid> ActiveQuestIds { get; set; } = new();
        public List<Guid> CompletedQuestIds { get; set; } = new();

        public bool HasQuest(Guid questId)
            => ActiveQuestIds.Contains(questId);

        public bool HasCompletedQuest(Guid questId)
            => CompletedQuestIds.Contains(questId);

        public void AcceptQuest(Guid questId)
        {
            if (!HasQuest(questId))
                ActiveQuestIds.Add(questId);
        }

        public void CompleteQuest(Guid questId)
        {
            ActiveQuestIds.Remove(questId);
            if (!CompletedQuestIds.Contains(questId))
                CompletedQuestIds.Add(questId);
        }

        // =====================
        // Statistics Tracking (L.O.R.D inspired)
        // =====================

        public int TotalKills { get; set; } = 0;
        public int TotalDeaths { get; set; } = 0;
        public int TotalQuestsCompleted { get; set; } = 0;
        public long TotalGoldEarned { get; set; } = 0;
        public long TotalDamageDealt { get; set; } = 0;
        public long TotalDamageTaken { get; set; } = 0;
        public long TotalHealingDone { get; set; } = 0;
        public int TotalCriticalHits { get; set; } = 0;
        public int TotalCriticalFails { get; set; } = 0;
        public string? MostKilledEnemy { get; set; }

        public double KillDeathRatio => TotalDeaths > 0
            ? Math.Round((double)TotalKills / TotalDeaths, 2)
            : TotalKills;

        // =====================
        // Level Up Override
        // =====================

        public override void LevelUp(DiceService diceService)
        {
            base.LevelUp(diceService);

            // Grant skill points on level up
            AvailableSkillPoints += Class switch
            {
                CharacterClass.Rogue      or
                CharacterClass.Bard       or
                CharacterClass.Ranger => 4,     // Skill heavy classes
                CharacterClass.Fighter    or
                CharacterClass.Barbarian => 2,     // Combat focused classes
                _ => 3      // Everyone else
            };
        }

        // =====================
        // Character Creation
        // =====================

        public static PlayerCharacter Create(
            string playerName,
            string characterName,
            CharacterRace race,
            CharacterClass charClass,
            Alignment alignment,
            Gender gender,
            List<DiceRoll> statRolls,
            DiceService diceService)
        {
            var character = new PlayerCharacter
            {
                PlayerName  = playerName,
                Name        = characterName,
                Race        = race,
                Class       = charClass,
                Alignment   = alignment,
                Gender      = gender,
            };

            // Assign rolled stats in classic order
            // Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma
            if (statRolls.Count >= 6)
            {
                character.Stats.Strength        = statRolls[0].Total;
                character.Stats.Dexterity       = statRolls[1].Total;
                character.Stats.Constitution    = statRolls[2].Total;
                character.Stats.Intelligence    = statRolls[3].Total;
                character.Stats.Wisdom          = statRolls[4].Total;
                character.Stats.Charisma        = statRolls[5].Total;
            }

            // Apply race bonuses on top of rolled stats
            character.ApplyRaceBonuses();

            // Calculate starting HP using class hit die
            var hitDie = charClass switch
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

            var startingHp = diceService.Roll(hitDie, 1, character.Stats.ConstitutionModifier);
            character.Stats.MaxHitPoints        = Math.Max(1, startingHp.Total);
            character.Stats.CurrentHitPoints    = character.Stats.MaxHitPoints;

            // Starting mana and stamina
            character.Stats.MaxMana             = Math.Max(0,
                (character.Stats.IntelligenceModifier + character.Stats.WisdomModifier) * 5);
            character.Stats.CurrentMana         = character.Stats.MaxMana;

            character.Stats.MaxStamina          = Math.Max(5,
                character.Stats.ConstitutionModifier * 3 + 10);
            character.Stats.CurrentStamina      = character.Stats.MaxStamina;

            // Starting skill points
            character.AvailableSkillPoints = charClass switch
            {
                CharacterClass.Rogue  or
                CharacterClass.Bard   or
                CharacterClass.Ranger => 8,
                CharacterClass.Fighter or
                CharacterClass.Barbarian => 4,
                _ => 6
            };

            return character;
        }
    }
}
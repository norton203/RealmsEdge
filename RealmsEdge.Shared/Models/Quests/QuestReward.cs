using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Shared.Models.Quests
{
    public enum ReputationChange
    {
        None,               // No reputation effect
        MinorIncrease,      // +5 reputation with faction
        MajorIncrease,      // +15 reputation with faction
        MinorDecrease,      // -5 reputation with faction
        MajorDecrease,      // -15 reputation with faction
        AlliedStatus,       // Maximum reputation achieved
        EnemyStatus         // Minimum reputation, hostile
    }

    public class FactionReputation
    {
        public string FactionName { get; set; }
            = string.Empty;
        public ReputationChange Change { get; set; }
        public int ReputationValue => Change switch
        {
            ReputationChange.MinorIncrease => 5,
            ReputationChange.MajorIncrease => 15,
            ReputationChange.MinorDecrease => -5,
            ReputationChange.MajorDecrease => -15,
            ReputationChange.AlliedStatus => 100,
            ReputationChange.EnemyStatus => -100,
            _ => 0
        };

        public string Display =>
            $"{FactionName}: " +
            $"{(ReputationValue >= 0 ? "+" : "")}" +
            $"{ReputationValue} reputation";
    }

    public class QuestReward
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsOptionalReward { get; set; } = false;
        public string? RewardTitle { get; set; }

        // =====================
        // Experience
        // =====================

        public long ExperiencePoints { get; set; } = 0;
        public long BonusExperience { get; set; } = 0;   // For optional objectives

        public long TotalExperience =>
            ExperiencePoints + BonusExperience;

        // =====================
        // Currency
        // =====================

        public int GoldReward { get; set; } = 0;
        public int SilverReward { get; set; } = 0;
        public int CopperReward { get; set; } = 0;

        public int TotalValueInCopper =>
            (GoldReward * 100) +
            (SilverReward * 10) +
            CopperReward;

        public string CurrencyDisplay
        {
            get
            {
                var parts = new List<string>();
                if (GoldReward > 0)
                    parts.Add($"{GoldReward}g");
                if (SilverReward > 0)
                    parts.Add($"{SilverReward}s");
                if (CopperReward > 0)
                    parts.Add($"{CopperReward}c");
                return parts.Any()
                    ? string.Join(" ", parts)
                    : "None";
            }
        }

        // =====================
        // Items
        // =====================

        public List<InventoryItem> ItemRewards { get; set; }
            = new();

        public List<InventoryItem> ChoiceItems { get; set; }
            = new();                            // Player picks one

        public bool HasItemChoice => ChoiceItems.Any();

        public InventoryItem? ChosenItem { get; set; }  // What player picked

        public bool ItemChoiceMade =>
            !HasItemChoice || ChosenItem != null;

        public bool ChooseItem(Guid itemId)
        {
            var item = ChoiceItems
                .FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            ChosenItem = item;
            return true;
        }

        // =====================
        // Equipment Reward
        // =====================

        public InventoryItem? GuaranteedEquipment { get; set; }
        public ItemRarity MinimumRewardRarity { get; set; }
            = ItemRarity.Common;

        // =====================
        // Stat Rewards
        // =====================

        public int StrengthBonus { get; set; } = 0;
        public int DexterityBonus { get; set; } = 0;
        public int ConstitutionBonus { get; set; } = 0;
        public int IntelligenceBonus { get; set; } = 0;
        public int WisdomBonus { get; set; } = 0;
        public int CharismaBonus { get; set; } = 0;
        public int LuckBonus { get; set; } = 0;

        public bool HasStatBonuses =>
            StrengthBonus > 0     ||
            DexterityBonus > 0    ||
            ConstitutionBonus > 0 ||
            IntelligenceBonus > 0 ||
            WisdomBonus > 0       ||
            CharismaBonus > 0     ||
            LuckBonus > 0;

        // =====================
        // Status & Skills
        // =====================

        public CharacterStatus? StatusGranted { get; set; }
        public int StatusDuration { get; set; } = 0;
        public string? SkillUnlocked { get; set; }
        public int SkillPointsAwarded { get; set; } = 0;

        // =====================
        // Reputation
        // =====================

        public List<FactionReputation> ReputationChanges
        { get; set; } = new();

        public void AddReputation(
            string factionName,
            ReputationChange change)
        {
            ReputationChanges.Add(new FactionReputation
            {
                FactionName = factionName,
                Change      = change
            });
        }

        // =====================
        // Unlock Rewards
        // =====================

        public Guid? UnlocksQuestId { get; set; }       // Starts follow up quest
        public Guid? UnlocksLocationId { get; set; }    // Opens new area
        public string? UnlocksAbility { get; set; }     // Special ability name
        public string? UnlocksTitle { get; set; }       // Character title
                                                        // e.g. "the Brave"

        // =====================
        // Apply Reward
        // =====================

        public List<string> ApplyTo(
            PlayerCharacter character,
            bool applyOptional = false)
        {
            var log = new List<string>();

            if (IsOptionalReward && !applyOptional)
                return log;

            // Experience
            if (TotalExperience > 0)
            {
                character.AddExperience(TotalExperience);
                log.Add(
                    $"✨ {TotalExperience:N0} XP gained!");
            }

            // Currency
            if (TotalValueInCopper > 0)
            {
                character.AddCurrency(
                    GoldReward,
                    SilverReward,
                    CopperReward);
                log.Add(
                    $"💰 {CurrencyDisplay} received!");
            }

            // Guaranteed items
            foreach (var item in ItemRewards)
            {
                var added = character.AddItem(item);
                log.Add(added
                    ? $"🎁 Received: {item.Name} " +
                      $"({item.Rarity})"
                    : $"⚠️ Could not carry: {item.Name}");
            }

            // Chosen item
            if (ChosenItem != null)
            {
                var added = character.AddItem(ChosenItem);
                log.Add(added
                    ? $"🎁 Chose: {ChosenItem.Name} " +
                      $"({ChosenItem.Rarity})"
                    : $"⚠️ Could not carry: " +
                      $"{ChosenItem.Name}");
            }

            // Guaranteed equipment
            if (GuaranteedEquipment != null)
            {
                var added = character
                    .AddItem(GuaranteedEquipment);
                log.Add(added
                    ? $"⚔️ Received: " +
                      $"{GuaranteedEquipment.Name} " +
                      $"({GuaranteedEquipment.Rarity})"
                    : $"⚠️ Could not carry: " +
                      $"{GuaranteedEquipment.Name}");
            }

            // Stat bonuses
            if (HasStatBonuses)
            {
                character.Stats.Strength     += StrengthBonus;
                character.Stats.Dexterity    += DexterityBonus;
                character.Stats.Constitution += ConstitutionBonus;
                character.Stats.Intelligence += IntelligenceBonus;
                character.Stats.Wisdom       += WisdomBonus;
                character.Stats.Charisma     += CharismaBonus;
                character.Stats.Luck         += LuckBonus;

                log.Add(BuildStatBonusDisplay());
            }

            // Status effect
            if (StatusGranted.HasValue)
            {
                character.ApplyStatus(StatusGranted.Value);
                log.Add(
                    $"✨ {StatusGranted} granted" +
                    $"{(StatusDuration > 0 ? $" for {StatusDuration} rounds" : " permanently")}!");
            }

            // Skill points
            if (SkillPointsAwarded > 0)
            {
                character.AvailableSkillPoints
                    += SkillPointsAwarded;
                log.Add(
                    $"📚 {SkillPointsAwarded} skill " +
                    $"point(s) awarded!");
            }

            // Skill unlock
            if (SkillUnlocked != null)
            {
                character.Skills[SkillUnlocked] = 1;
                log.Add(
                    $"📚 New skill unlocked: " +
                    $"{SkillUnlocked}!");
            }

            // Title unlock
            if (UnlocksTitle != null)
            {
                character.Title = UnlocksTitle;
                log.Add(
                    $"👑 New title earned: " +
                    $"{UnlocksTitle}!");
            }

            // Quest unlock
            if (UnlocksQuestId.HasValue)
            {
                character.AcceptQuest(
                    UnlocksQuestId.Value);
                log.Add("📜 New quest discovered!");
            }

            // Reputation
            foreach (var rep in ReputationChanges)
                log.Add($"🏛️ {rep.Display}");

            return log;
        }

        // =====================
        // Summary Display
        // =====================

        private string BuildStatBonusDisplay()
        {
            var parts = new List<string>();
            if (StrengthBonus > 0)
                parts.Add($"STR +{StrengthBonus}");
            if (DexterityBonus > 0)
                parts.Add($"DEX +{DexterityBonus}");
            if (ConstitutionBonus > 0)
                parts.Add($"CON +{ConstitutionBonus}");
            if (IntelligenceBonus > 0)
                parts.Add($"INT +{IntelligenceBonus}");
            if (WisdomBonus > 0)
                parts.Add($"WIS +{WisdomBonus}");
            if (CharismaBonus > 0)
                parts.Add($"CHA +{CharismaBonus}");
            if (LuckBonus > 0)
                parts.Add($"LCK +{LuckBonus}");
            return $"📊 Stat bonuses: " +
                   $"{string.Join(", ", parts)}";
        }

        public string RewardSummary
        {
            get
            {
                var parts = new List<string>();
                if (TotalExperience > 0)
                    parts.Add(
                        $"{TotalExperience:N0} XP");
                if (TotalValueInCopper > 0)
                    parts.Add(CurrencyDisplay);
                if (ItemRewards.Any())
                    parts.Add(
                        $"{ItemRewards.Count} item(s)");
                if (HasItemChoice)
                    parts.Add("Item choice");
                if (GuaranteedEquipment != null)
                    parts.Add(
                        $"{GuaranteedEquipment.Name}");
                if (HasStatBonuses)
                    parts.Add("Stat bonuses");
                if (SkillUnlocked != null)
                    parts.Add(
                        $"Skill: {SkillUnlocked}");
                if (UnlocksTitle != null)
                    parts.Add(
                        $"Title: {UnlocksTitle}");
                if (ReputationChanges.Any())
                    parts.Add("Reputation");
                return parts.Any()
                    ? string.Join(" | ", parts)
                    : "No reward";
            }
        }

        public override string ToString()
            => RewardSummary;
    }
}
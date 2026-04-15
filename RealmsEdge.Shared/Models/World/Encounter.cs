using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Shared.Models.World
{
    public enum EncounterTrigger
    {
        OnEnter,            // Triggers when player enters the room
        OnSearch,           // Triggers when player searches the room
        OnChest,            // Triggers when player opens a chest
        OnStep,             // Triggers on specific tile (trap)
        OnInteract,         // Triggers when player interacts with object
        OnRest,             // Triggers when party attempts to rest
        OnLowHealth,        // Triggers when party health is low
        Random,             // Random chance each turn in the room
        Scripted            // Triggered by quest or story event
    }

    public enum EncounterOutcome
    {
        Pending,            // Not yet resolved
        Victory,            // Players won combat or succeeded
        Defeat,             // Players lost or failed
        Fled,               // Players escaped combat
        Negotiated,         // Resolved without combat
        Skipped,            // Player bypassed the encounter
        Ignored             // Encounter was not triggered
    }

    public class EncounterReward
    {
        public long ExperiencePoints { get; set; } = 0;
        public int GoldReward { get; set; } = 0;
        public int SilverReward { get; set; } = 0;
        public int CopperReward { get; set; } = 0;
        public List<InventoryItem> ItemRewards { get; set; } = new();
        public CharacterStatus? StatusRemoved { get; set; }     // Cures a status
        public CharacterStatus? StatusGranted { get; set; }     // Grants a status
        public string? LoreText { get; set; }                   // Story text revealed
        public Guid? QuestIdCompleted { get; set; }             // Completes a quest step
        public Guid? QuestIdGranted { get; set; }               // Starts a new quest

        public string RewardSummary
        {
            get
            {
                var parts = new List<string>();
                if (ExperiencePoints > 0) parts.Add($"{ExperiencePoints:N0} XP");
                if (GoldReward > 0) parts.Add($"{GoldReward}g");
                if (SilverReward > 0) parts.Add($"{SilverReward}s");
                if (CopperReward > 0) parts.Add($"{CopperReward}c");
                if (ItemRewards.Any())
                    parts.Add($"{ItemRewards.Count} item(s)");
                return parts.Any()
                    ? string.Join(", ", parts)
                    : "No rewards";
            }
        }
    }

    public class EncounterChoice
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;       // What the player sees
        public string? RequiredSkill { get; set; }              // Skill check needed
        public int SkillDifficulty { get; set; } = 0;          // DC for skill check
        public Alignment? RequiredAlignment { get; set; }       // Alignment restriction
        public CharacterClass? RequiredClass { get; set; }      // Class restriction
        public int RequiredLevel { get; set; } = 1;
        public EncounterOutcome Outcome { get; set; }
        public EncounterReward? Reward { get; set; }
        public string? OutcomeText { get; set; }                // Shown after choice

        public bool IsAvailableTo(PlayerCharacter player)
        {
            if (player.Level < RequiredLevel) return false;
            if (RequiredAlignment.HasValue &&
                player.Alignment != RequiredAlignment.Value) return false;
            if (RequiredClass.HasValue &&
                player.Class != RequiredClass.Value) return false;
            if (RequiredSkill != null &&
                !player.HasSkill(RequiredSkill)) return false;
            return true;
        }

        public string AvailabilityDisplay(PlayerCharacter player)
        {
            if (player.Level < RequiredLevel)
                return $"[Requires Level {RequiredLevel}]";
            if (RequiredAlignment.HasValue &&
                player.Alignment != RequiredAlignment.Value)
                return $"[Requires {RequiredAlignment} alignment]";
            if (RequiredClass.HasValue &&
                player.Class != RequiredClass.Value)
                return $"[Requires {RequiredClass} class]";
            if (RequiredSkill != null &&
                !player.HasSkill(RequiredSkill))
                return $"[Requires {RequiredSkill} skill]";
            return string.Empty;
        }
    }

    public class Encounter
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public EncounterType Type { get; set; }
        public EncounterTrigger Trigger { get; set; }
            = EncounterTrigger.OnEnter;

        // =====================
        // State
        // =====================

        public EncounterOutcome Outcome { get; set; }
            = EncounterOutcome.Pending;
        public bool IsCompleted => Outcome != EncounterOutcome.Pending;
        public bool IsRepeatable { get; set; } = false;        // Can trigger again
        public int TriggerChance { get; set; } = 100;          // % chance to trigger
        public DateTime? CompletedAt { get; set; }

        // =====================
        // Combat Encounter
        // =====================

        public List<NpcCharacter> Enemies { get; set; } = new();
        public bool IsSurpriseRound { get; set; } = false;     // Ambush, enemies go first
        public bool CanFlee { get; set; } = true;              // Players can run
        public int FleeSuccessChance { get; set; } = 50;       // % chance to flee

        public bool HasLivingEnemies => Enemies
            .Any(e => e.Stats.IsAlive);

        public void AddEnemy(NpcCharacter enemy)
            => Enemies.Add(enemy);

        // =====================
        // Choice Encounter
        // =====================

        public string? SetupText { get; set; }                 // Story text before choices
        public List<EncounterChoice> Choices { get; set; } = new();
        public EncounterChoice? ChoiceMade { get; set; }

        public List<EncounterChoice> GetAvailableChoices(
            PlayerCharacter player) => Choices
                .Where(c => c.IsAvailableTo(player))
                .ToList();

        public void AddChoice(string text,
            EncounterOutcome outcome,
            EncounterReward? reward = null,
            string? outcomeText = null)
        {
            Choices.Add(new EncounterChoice
            {
                Text        = text,
                Outcome     = outcome,
                Reward      = reward,
                OutcomeText = outcomeText
            });
        }

        // =====================
        // Trap Encounter
        // =====================

        public int TrapDifficultyClass { get; set; } = 10;     // DC to spot or disarm
        public bool TrapIsSpotted { get; set; } = false;
        public bool TrapIsDisarmed { get; set; } = false;
        public DiceType? TrapDamageDie { get; set; }           // Damage if triggered
        public int TrapDamageBonus { get; set; } = 0;
        public CharacterStatus? TrapAppliesStatus { get; set; } // Poison, paralysis etc
        public CharacterStat? TrapSaveUsing { get; set; }       // Dexterity, Constitution etc

        // =====================
        // Treasure Encounter
        // =====================

        public List<InventoryItem> TreasureItems { get; set; } = new();
        public int TreasureGold { get; set; } = 0;
        public int TreasureSilver { get; set; } = 0;
        public int TreasureCopper { get; set; } = 0;
        public bool TreasureIsLooted { get; set; } = false;
        public bool TreasureIsLocked { get; set; } = false;
        public int TreasureLockDifficulty { get; set; } = 0;

        // =====================
        // Rewards
        // =====================

        public EncounterReward? VictoryReward { get; set; }
        public EncounterReward? DefeatReward { get; set; }      // Rare — lose something
        public EncounterReward? FleeReward { get; set; }        // Usually nothing

        // =====================
        // Random Event
        // (L.O.R.D inspired)
        // =====================

        public string? RandomEventText { get; set; }
        public bool HasPositiveOutcome { get; set; } = true;

        // =====================
        // Lore Entry
        // =====================

        public string? LoreTitle { get; set; }
        public string? LoreContent { get; set; }
        public bool LoreIsOptional { get; set; } = true;       // Skip button available

        // =====================
        // Complete Encounter
        // =====================

        public void Complete(EncounterOutcome outcome)
        {
            Outcome      = outcome;
            CompletedAt  = DateTime.UtcNow;

            if (!IsRepeatable)
            {
                // Mark all enemies as processed
                foreach (var enemy in Enemies)
                    enemy.LastKilledAt = DateTime.UtcNow;
            }
        }

        // =====================
        // Factory Methods
        // =====================

        public static Encounter CreateCombat(
            string name,
            List<NpcCharacter> enemies,
            EncounterReward reward,
            bool canFlee = true,
            bool isSurprise = false)
        {
            return new Encounter
            {
                Name            = name,
                Type            = EncounterType.Combat,
                Trigger         = EncounterTrigger.OnEnter,
                Enemies         = enemies,
                VictoryReward   = reward,
                CanFlee         = canFlee,
                IsSurpriseRound = isSurprise
            };
        }

        public static Encounter CreateTrap(
            string name,
            string description,
            int difficultClass,
            DiceType damageDie,
            CharacterStatus? appliesStatus = null)
        {
            return new Encounter
            {
                Name                 = name,
                Description          = description,
                Type                 = EncounterType.Trap,
                Trigger              = EncounterTrigger.OnStep,
                TrapDifficultyClass  = difficultClass,
                TrapDamageDie        = damageDie,
                TrapAppliesStatus    = appliesStatus,
                TrapSaveUsing        = CharacterStat.Dexterity,
                IsRepeatable         = false
            };
        }

        public static Encounter CreateTreasure(
            string name,
            List<InventoryItem> items,
            int gold = 0,
            int silver = 0,
            int copper = 0,
            bool isLocked = false,
            int lockDifficulty = 0)
        {
            return new Encounter
            {
                Name                   = name,
                Type                   = EncounterType.Treasure,
                Trigger                = EncounterTrigger.OnInteract,
                TreasureItems          = items,
                TreasureGold           = gold,
                TreasureSilver         = silver,
                TreasureCopper         = copper,
                TreasureIsLocked       = isLocked,
                TreasureLockDifficulty = lockDifficulty,
                IsRepeatable           = false
            };
        }

        public static Encounter CreateRandomEvent(
            string name,
            string eventText,
            EncounterReward reward,
            int triggerChance = 25)
        {
            return new Encounter
            {
                Name             = name,
                Type             = EncounterType.RandomEvent,
                Trigger          = EncounterTrigger.Random,
                RandomEventText  = eventText,
                VictoryReward    = reward,
                TriggerChance    = triggerChance,
                IsRepeatable     = true
            };
        }

        public static Encounter CreateLoreEntry(
            string title,
            string content,
            bool isOptional = true)
        {
            return new Encounter
            {
                Name        = title,
                Type        = EncounterType.LoreEntry,
                Trigger     = EncounterTrigger.OnEnter,
                LoreTitle   = title,
                LoreContent = content,
                LoreIsOptional = isOptional,
                IsRepeatable   = false
            };
        }

        // =====================
        // Display Helpers
        // =====================

        public string TypeDisplay => Type switch
        {
            EncounterType.Combat => "⚔️ Combat",
            EncounterType.EliteCombat => "💀 Elite Combat",
            EncounterType.BossCombat => "👑 Boss Fight",
            EncounterType.Ambush => "🗡️ Ambush",
            EncounterType.Trap => "⚠️ Trap",
            EncounterType.Puzzle => "🧩 Puzzle",
            EncounterType.Treasure => "💎 Treasure",
            EncounterType.NpcDialogue => "💬 NPC",
            EncounterType.RandomEvent => "🎲 Random Event",
            EncounterType.LoreEntry => "📖 Lore",
            EncounterType.Choice => "❓ Choice",
            EncounterType.Shrine => "✨ Shrine",
            _ => "📍 Encounter"
        };

        public string OutcomeDisplay => Outcome switch
        {
            EncounterOutcome.Pending => "⏳ Pending",
            EncounterOutcome.Victory => "✅ Victory",
            EncounterOutcome.Defeat => "💀 Defeat",
            EncounterOutcome.Fled => "🏃 Fled",
            EncounterOutcome.Negotiated => "🤝 Negotiated",
            EncounterOutcome.Skipped => "⏭️ Skipped",
            EncounterOutcome.Ignored => "👁️ Ignored",
            _ => "Unknown"
        };

        public override string ToString()
            => $"{TypeDisplay}: {Name} | {OutcomeDisplay}";
    }
}
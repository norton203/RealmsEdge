using RealmsEdge.Shared.Enums;

namespace RealmsEdge.Shared.Models.Quests
{
    public enum ObjectiveType
    {
        // =====================
        // Combat
        // =====================

        KillEnemy,          // Kill a specific enemy type
        KillNamedEnemy,     // Kill a specific named NPC
        KillCount,          // Kill X enemies of any type
        DefeatBoss,         // Defeat a boss encounter

        // =====================
        // Collection
        // =====================

        CollectItem,        // Pick up a specific item
        CollectCount,       // Collect X of an item
        DeliverItem,        // Give item to specific NPC
        CraftItem,          // Craft a specific item

        // =====================
        // Exploration
        // =====================

        VisitLocation,      // Travel to a specific location
        EnterRoom,          // Enter a specific room
        DiscoverSecret,     // Find a hidden area
        ExplorePercent,     // Explore X% of a location

        // =====================
        // Social
        // =====================

        TalkToNpc,          // Speak to a specific NPC
        EscortNpc,          // Keep NPC alive to destination
        RescueNpc,          // Free a captured NPC
        NegotiateWith,      // Resolve encounter without combat

        // =====================
        // Survival
        // =====================

        SurviveRounds,      // Last X rounds in combat
        SurviveWithHp,      // Finish fight above X HP
        CompleteWithoutDying // Finish objective without dying
    }

    public class QuestObjective
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; }
            = string.Empty;
        public ObjectiveType Type { get; set; }
        public int OrderIndex { get; set; } = 0;        // Display order
        public bool IsOptional { get; set; } = false;   // Bonus objective
        public bool IsHidden { get; set; } = false;     // Revealed mid quest

        // =====================
        // Progress Tracking
        // =====================

        public int RequiredCount { get; set; } = 1;     // How many needed
        public int CurrentCount { get; set; } = 0;      // How many done
        public bool IsComplete { get; set; } = false;
        public bool IsFailed { get; set; } = false;
        public DateTime? CompletedAt { get; set; }

        public double ProgressPercent => RequiredCount > 0
            ? Math.Min(100,
                (double)CurrentCount / RequiredCount * 100)
            : IsComplete ? 100 : 0;

        public string ProgressDisplay =>
            RequiredCount > 1
                ? $"{CurrentCount}/{RequiredCount}"
                : IsComplete ? "✅ Done" : "❌ Incomplete";

        // =====================
        // Target Details
        // =====================

        public string? TargetName { get; set; }         // Enemy/NPC/item name
        public Guid? TargetLocationId { get; set; }     // Where to do it
        public Guid? TargetRoomId { get; set; }         // Specific room
        public Guid? TargetNpcId { get; set; }          // Specific NPC
        public Guid? TargetItemId { get; set; }         // Specific item
        public CharacterRace? TargetRace { get; set; }  // Enemy race filter
        public CharacterClass? TargetClass { get; set; }// Enemy class filter

        // =====================
        // Progress Methods
        // =====================

        public bool Advance(int amount = 1)
        {
            if (IsComplete || IsFailed) return false;

            CurrentCount = Math.Min(
                RequiredCount,
                CurrentCount + amount);

            if (CurrentCount >= RequiredCount)
                Complete();

            return IsComplete;
        }

        public void Complete()
        {
            IsComplete   = true;
            CurrentCount = RequiredCount;
            CompletedAt  = DateTime.UtcNow;
        }

        public void Fail()
        {
            IsFailed = true;
        }

        public void Reset()
        {
            IsComplete   = false;
            IsFailed     = false;
            CurrentCount = 0;
            CompletedAt  = null;
        }

        // =====================
        // Validation Checks
        // =====================

        public bool IsKillObjective =>
            Type == ObjectiveType.KillEnemy     ||
            Type == ObjectiveType.KillNamedEnemy ||
            Type == ObjectiveType.KillCount     ||
            Type == ObjectiveType.DefeatBoss;

        public bool IsCollectionObjective =>
            Type == ObjectiveType.CollectItem   ||
            Type == ObjectiveType.CollectCount  ||
            Type == ObjectiveType.DeliverItem   ||
            Type == ObjectiveType.CraftItem;

        public bool IsExplorationObjective =>
            Type == ObjectiveType.VisitLocation  ||
            Type == ObjectiveType.EnterRoom      ||
            Type == ObjectiveType.DiscoverSecret ||
            Type == ObjectiveType.ExplorePercent;

        public bool IsSocialObjective =>
            Type == ObjectiveType.TalkToNpc    ||
            Type == ObjectiveType.EscortNpc    ||
            Type == ObjectiveType.RescueNpc    ||
            Type == ObjectiveType.NegotiateWith;

        // =====================
        // Kill Objective Check
        // (called by combat service)
        // =====================

        public bool CheckKillObjective(
            CharacterRace enemyRace,
            CharacterClass enemyClass,
            string enemyName,
            bool isBoss)
        {
            if (!IsKillObjective || IsComplete) return false;

            return Type switch
            {
                ObjectiveType.KillCount => true,
                ObjectiveType.DefeatBoss => isBoss,
                ObjectiveType.KillNamedEnemy =>
                    TargetName != null &&
                    enemyName.Equals(
                        TargetName,
                        StringComparison.OrdinalIgnoreCase),
                ObjectiveType.KillEnemy =>
                    (TargetRace == null ||
                     TargetRace == enemyRace) &&
                    (TargetClass == null ||
                     TargetClass == enemyClass),
                _ => false
            };
        }

        // =====================
        // Location Objective Check
        // (called by navigation service)
        // =====================

        public bool CheckLocationObjective(
            Guid locationId,
            Guid? roomId = null)
        {
            if (!IsExplorationObjective || IsComplete)
                return false;

            return Type switch
            {
                ObjectiveType.VisitLocation =>
                    TargetLocationId == locationId,
                ObjectiveType.EnterRoom =>
                    TargetRoomId.HasValue &&
                    TargetRoomId == roomId,
                _ => false
            };
        }

        // =====================
        // Item Objective Check
        // (called by inventory system)
        // =====================

        public bool CheckItemObjective(
            Guid itemId,
            string itemName)
        {
            if (!IsCollectionObjective || IsComplete)
                return false;

            return Type switch
            {
                ObjectiveType.CollectItem =>
                    TargetItemId == itemId ||
                    (TargetName != null &&
                     itemName.Equals(
                         TargetName,
                         StringComparison
                             .OrdinalIgnoreCase)),
                ObjectiveType.CollectCount =>
                    TargetItemId == itemId ||
                    (TargetName != null &&
                     itemName.Equals(
                         TargetName,
                         StringComparison
                             .OrdinalIgnoreCase)),
                _ => false
            };
        }

        // =====================
        // NPC Objective Check
        // (called by world service)
        // =====================

        public bool CheckNpcObjective(Guid npcId)
        {
            if (!IsSocialObjective || IsComplete)
                return false;

            return TargetNpcId == npcId;
        }

        // =====================
        // Display Helpers
        // =====================

        public string TypeDisplay => Type switch
        {
            ObjectiveType.KillEnemy => "⚔️ Defeat",
            ObjectiveType.KillNamedEnemy => "🎯 Hunt",
            ObjectiveType.KillCount => "💀 Kill",
            ObjectiveType.DefeatBoss => "👑 Defeat Boss",
            ObjectiveType.CollectItem => "🎒 Collect",
            ObjectiveType.CollectCount => "🎒 Gather",
            ObjectiveType.DeliverItem => "📦 Deliver",
            ObjectiveType.CraftItem => "⚒️ Craft",
            ObjectiveType.VisitLocation => "🗺️ Visit",
            ObjectiveType.EnterRoom => "🚪 Enter",
            ObjectiveType.DiscoverSecret => "🔍 Discover",
            ObjectiveType.ExplorePercent => "🗺️ Explore",
            ObjectiveType.TalkToNpc => "💬 Talk to",
            ObjectiveType.EscortNpc => "🛡️ Escort",
            ObjectiveType.RescueNpc => "⛓️ Rescue",
            ObjectiveType.NegotiateWith => "🤝 Negotiate",
            ObjectiveType.SurviveRounds => "⏱️ Survive",
            ObjectiveType.SurviveWithHp => "❤️ Survive",
            _ => "📋 Complete"
        };

        public string StatusIcon =>
            IsComplete ? "✅" :
            IsFailed ? "❌" :
            IsOptional ? "⭐" : "🔲";

        public string FullDisplay =>
            $"{StatusIcon} {TypeDisplay} " +
            $"{TargetName ?? "objective"}" +
            (RequiredCount > 1
                ? $" ({ProgressDisplay})"
                : string.Empty) +
            (IsOptional ? " [Optional]" : string.Empty);

        public override string ToString() => FullDisplay;
    }
}
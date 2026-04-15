using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;

namespace RealmsEdge.Shared.Models.Quests
{
    public class Quest
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; }
            = string.Empty;
        public string? FlavorText { get; set; }         // Atmospheric intro text
        public string? CompletionText { get; set; }     // Shown on completion
        public string? FailureText { get; set; }        // Shown on failure
        public string? ImagePath { get; set; }
        public QuestType Type { get; set; }
        public QuestStatus Status { get; set; }
            = QuestStatus.Hidden;

        // =====================
        // Source
        // =====================

        public Guid? QuestGiverId { get; set; }         // NPC who gave quest
        public string? QuestGiverName { get; set; }
        public Guid? QuestLocationId { get; set; }      // Where quest takes place
        public Guid? TurnInNpcId { get; set; }          // Who receives completion
        public string? TurnInNpcName { get; set; }
        public Guid? TurnInLocationId { get; set; }     // Where to turn in

        // =====================
        // Requirements
        // =====================

        public int RequiredLevel { get; set; } = 1;
        public int MaxLevel { get; set; } = 99;
        public Alignment? RequiredAlignment { get; set; }
        public List<CharacterClass> RequiredClasses
        { get; set; } = new();
        public List<CharacterRace> RequiredRaces
        { get; set; } = new();
        public Guid? RequiredQuestId { get; set; }      // Must complete first
        public List<Guid> RequiredQuestIds
        { get; set; } = new();                      // All must be complete
        public string? RequiredFaction { get; set; }
        public int RequiredReputation { get; set; } = 0;

        public bool IsAvailableTo(PlayerCharacter player)
        {
            if (player.Level < RequiredLevel) return false;
            if (player.Level > MaxLevel) return false;
            if (RequiredAlignment.HasValue &&
                player.Alignment != RequiredAlignment)
                return false;
            if (RequiredClasses.Any() &&
                !RequiredClasses.Contains(player.Class))
                return false;
            if (RequiredRaces.Any() &&
                !RequiredRaces.Contains(player.Race))
                return false;
            if (RequiredQuestId.HasValue &&
                !player.HasCompletedQuest(
                    RequiredQuestId.Value))
                return false;
            if (RequiredQuestIds.Any() &&
                !RequiredQuestIds.All(id =>
                    player.HasCompletedQuest(id)))
                return false;
            return true;
        }

        public string UnavailableReason(
            PlayerCharacter player)
        {
            if (player.Level < RequiredLevel)
                return $"Requires level {RequiredLevel}.";
            if (player.Level > MaxLevel)
                return $"This quest is for lower level " +
                       $"adventurers.";
            if (RequiredAlignment.HasValue &&
                player.Alignment != RequiredAlignment)
                return $"Requires {RequiredAlignment} " +
                       $"alignment.";
            if (RequiredClasses.Any() &&
                !RequiredClasses.Contains(player.Class))
                return $"Only available to: " +
                       $"{string.Join(", ", RequiredClasses)}.";
            if (RequiredRaces.Any() &&
                !RequiredRaces.Contains(player.Race))
                return $"Only available to: " +
                       $"{string.Join(", ", RequiredRaces)}.";
            if (RequiredQuestId.HasValue &&
                !player.HasCompletedQuest(
                    RequiredQuestId.Value))
                return "You must complete a prerequisite " +
                       "quest first.";
            return string.Empty;
        }

        // =====================
        // Time Limits
        // =====================

        public bool HasTimeLimit { get; set; } = false;
        public int TimeLimitMinutes { get; set; } = 0;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? FailedAt { get; set; }

        public bool IsExpired =>
            HasTimeLimit &&
            ExpiresAt.HasValue &&
            DateTime.UtcNow > ExpiresAt.Value;

        public TimeSpan? TimeRemaining =>
            ExpiresAt.HasValue
                ? ExpiresAt.Value - DateTime.UtcNow
                : null;

        public string TimeRemainingDisplay
        {
            get
            {
                if (!HasTimeLimit) return string.Empty;
                if (IsExpired) return "⏰ Expired!";
                var remaining = TimeRemaining;
                if (!remaining.HasValue) return string.Empty;
                if (remaining.Value.TotalHours >= 1)
                    return $"⏰ {remaining.Value.Hours}h " +
                           $"{remaining.Value.Minutes}m remaining";
                return $"⏰ {remaining.Value.Minutes}m " +
                       $"{remaining.Value.Seconds}s remaining";
            }
        }

        // =====================
        // Repeatable Quests
        // (L.O.R.D inspired)
        // =====================

        public bool IsRepeatable { get; set; } = false;
        public int TimesCompleted { get; set; } = 0;
        public int MaxCompletions { get; set; } = 0;    // 0 = unlimited
        public DateTime? LastCompletedAt { get; set; }
        public DateTime? NextAvailableAt { get; set; }

        public bool CanRepeat =>
            IsRepeatable &&
            (MaxCompletions == 0 ||
             TimesCompleted < MaxCompletions) &&
            (!NextAvailableAt.HasValue ||
             DateTime.UtcNow >= NextAvailableAt.Value);

        // =====================
        // Objectives
        // =====================

        public List<QuestObjective> Objectives
        { get; set; } = new();

        public List<QuestObjective> RequiredObjectives =>
            Objectives.Where(o => !o.IsOptional).ToList();

        public List<QuestObjective> OptionalObjectives =>
            Objectives.Where(o => o.IsOptional).ToList();

        public List<QuestObjective> CompletedObjectives =>
            Objectives.Where(o => o.IsComplete).ToList();

        public bool AllRequiredComplete =>
            RequiredObjectives.All(o => o.IsComplete);

        public bool AllObjectivesComplete =>
            Objectives.All(o => o.IsComplete);

        public double ProgressPercent =>
            RequiredObjectives.Any()
                ? RequiredObjectives
                    .Average(o => o.ProgressPercent)
                : 0;

        public void AddObjective(QuestObjective objective)
        {
            objective.OrderIndex = Objectives.Count;
            Objectives.Add(objective);
        }

        public QuestObjective? GetObjective(Guid objectiveId)
            => Objectives.FirstOrDefault(
                o => o.Id == objectiveId);

        // =====================
        // Rewards
        // =====================

        public QuestReward? MainReward { get; set; }
        public QuestReward? OptionalReward { get; set; }
        public QuestReward? FirstTimeReward { get; set; }// Extra for first completion

        // =====================
        // Quest State
        // =====================

        public void Accept(PlayerCharacter player)
        {
            Status     = QuestStatus.Active;
            AcceptedAt = DateTime.UtcNow;

            if (HasTimeLimit && TimeLimitMinutes > 0)
                ExpiresAt = DateTime.UtcNow
                    .AddMinutes(TimeLimitMinutes);

            player.AcceptQuest(Id);
        }

        public void Complete(PlayerCharacter player)
        {
            Status      = QuestStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            TimesCompleted++;
            LastCompletedAt = DateTime.UtcNow;

            // Set next available for repeatable quests
            if (IsRepeatable)
            {
                Status = QuestStatus.Available;
                NextAvailableAt = Type switch
                {
                    QuestType.Daily =>
                        DateTime.UtcNow.AddDays(1),
                    QuestType.Weekly =>
                        DateTime.UtcNow.AddDays(7),
                    _ =>
                        null
                };
            }

            player.CompleteQuest(Id);
        }

        public void Fail(PlayerCharacter player)
        {
            Status   = QuestStatus.Failed;
            FailedAt = DateTime.UtcNow;
            player.ActiveQuestIds.Remove(Id);
        }

        public void Abandon(PlayerCharacter player)
        {
            Status = QuestStatus.Abandoned;

            // Reset objectives for potential retry
            foreach (var objective in Objectives)
                objective.Reset();

            player.ActiveQuestIds.Remove(Id);
        }

        // =====================
        // Kill Tracking
        // (called by combat service)
        // =====================

        public List<QuestObjective> CheckKillProgress(
            CharacterRace race,
            CharacterClass charClass,
            string name,
            bool isBoss)
        {
            var advanced = new List<QuestObjective>();

            foreach (var obj in Objectives
                .Where(o => o.IsKillObjective &&
                            !o.IsComplete))
            {
                if (obj.CheckKillObjective(
                    race, charClass, name, isBoss))
                {
                    obj.Advance();
                    advanced.Add(obj);
                }
            }

            return advanced;
        }

        // =====================
        // Location Tracking
        // (called by navigation service)
        // =====================

        public List<QuestObjective> CheckLocationProgress(
            Guid locationId,
            Guid? roomId = null)
        {
            var advanced = new List<QuestObjective>();

            foreach (var obj in Objectives
                .Where(o => o.IsExplorationObjective &&
                            !o.IsComplete))
            {
                if (obj.CheckLocationObjective(
                    locationId, roomId))
                {
                    obj.Advance();
                    advanced.Add(obj);
                }
            }

            return advanced;
        }

        // =====================
        // Item Tracking
        // (called by inventory service)
        // =====================

        public List<QuestObjective> CheckItemProgress(
            Guid itemId,
            string itemName)
        {
            var advanced = new List<QuestObjective>();

            foreach (var obj in Objectives
                .Where(o => o.IsCollectionObjective &&
                            !o.IsComplete))
            {
                if (obj.CheckItemObjective(itemId, itemName))
                {
                    obj.Advance();
                    advanced.Add(obj);
                }
            }

            return advanced;
        }

        // =====================
        // NPC Tracking
        // (called by world service)
        // =====================

        public List<QuestObjective> CheckNpcProgress(
            Guid npcId)
        {
            var advanced = new List<QuestObjective>();

            foreach (var obj in Objectives
                .Where(o => o.IsSocialObjective &&
                            !o.IsComplete))
            {
                if (obj.CheckNpcObjective(npcId))
                {
                    obj.Advance();
                    advanced.Add(obj);
                }
            }

            return advanced;
        }

        // =====================
        // Factory Methods
        // =====================

        public static Quest CreateKillQuest(
            string title,
            string description,
            string targetName,
            CharacterRace targetRace,
            int killCount,
            int requiredLevel,
            QuestReward reward)
        {
            var quest = new Quest
            {
                Title         = title,
                Description   = description,
                Type          = QuestType.Kill,
                Status        = QuestStatus.Available,
                RequiredLevel = requiredLevel,
                MainReward    = reward
            };

            quest.AddObjective(new QuestObjective
            {
                Title         = $"Defeat {killCount} " +
                                $"{targetName}",
                Description   = $"Hunt down " +
                                $"{killCount} {targetName} " +
                                $"and defeat them.",
                Type          = ObjectiveType.KillEnemy,
                RequiredCount = killCount,
                TargetName    = targetName,
                TargetRace    = targetRace
            });

            return quest;
        }

        public static Quest CreateFetchQuest(
            string title,
            string description,
            string itemName,
            Guid itemId,
            Guid turnInNpcId,
            string turnInNpcName,
            int requiredLevel,
            QuestReward reward)
        {
            var quest = new Quest
            {
                Title          = title,
                Description    = description,
                Type           = QuestType.Fetch,
                Status         = QuestStatus.Available,
                RequiredLevel  = requiredLevel,
                TurnInNpcId    = turnInNpcId,
                TurnInNpcName  = turnInNpcName,
                MainReward     = reward
            };

            quest.AddObjective(new QuestObjective
            {
                Title         = $"Find the {itemName}",
                Description   = $"Locate and retrieve " +
                                $"the {itemName}.",
                Type          = ObjectiveType.CollectItem,
                RequiredCount = 1,
                TargetName    = itemName,
                TargetItemId  = itemId
            });

            quest.AddObjective(new QuestObjective
            {
                Title         = $"Return to " +
                                $"{turnInNpcName}",
                Description   = $"Deliver the {itemName}" +
                                $" to {turnInNpcName}.",
                Type          = ObjectiveType.TalkToNpc,
                RequiredCount = 1,
                TargetNpcId   = turnInNpcId,
                TargetName    = turnInNpcName
            });

            return quest;
        }

        public static Quest CreateExploreQuest(
            string title,
            string description,
            Guid locationId,
            string locationName,
            int requiredLevel,
            QuestReward reward)
        {
            var quest = new Quest
            {
                Title         = title,
                Description   = description,
                Type          = QuestType.Explore,
                Status        = QuestStatus.Available,
                RequiredLevel = requiredLevel,
                MainReward    = reward
            };

            quest.AddObjective(new QuestObjective
            {
                Title            = $"Discover " +
                                   $"{locationName}",
                Description      = $"Travel to and " +
                                   $"explore {locationName}.",
                Type             = ObjectiveType.VisitLocation,
                RequiredCount    = 1,
                TargetLocationId = locationId,
                TargetName       = locationName
            });

            return quest;
        }

        public static Quest CreateDailyQuest(
            string title,
            string description,
            QuestObjective objective,
            QuestReward reward)
        {
            var quest = new Quest
            {
                Title        = title,
                Description  = description,
                Type         = QuestType.Daily,
                Status       = QuestStatus.Available,
                IsRepeatable = true,
                MaxCompletions = 0,         // Unlimited
                HasTimeLimit   = true,
                TimeLimitMinutes = 1440,    // 24 hours
                MainReward   = reward
            };

            quest.AddObjective(objective);
            return quest;
        }

        // =====================
        // Display Helpers
        // =====================

        public string StatusDisplay => Status switch
        {
            QuestStatus.Hidden => "🔒 Hidden",
            QuestStatus.Available => "📜 Available",
            QuestStatus.Locked => "🔒 Locked",
            QuestStatus.Active => "⚔️ Active",
            QuestStatus.OnHold => "⏸️ On Hold",
            QuestStatus.ReadyToComplete => "✅ Ready!",
            QuestStatus.Completed => "🏆 Completed",
            QuestStatus.Failed => "❌ Failed",
            QuestStatus.Abandoned => "🚫 Abandoned",
            QuestStatus.Expired => "⏰ Expired",
            _ => "Unknown"
        };

        public string TypeDisplay => Type switch
        {
            QuestType.Kill => "⚔️ Kill",
            QuestType.BossKill => "👑 Boss Kill",
            QuestType.Fetch => "🎒 Fetch",
            QuestType.Collect => "🎒 Collect",
            QuestType.Explore => "🗺️ Explore",
            QuestType.Escort => "🛡️ Escort",
            QuestType.Rescue => "⛓️ Rescue",
            QuestType.Story => "📖 Story",
            QuestType.Daily => "📅 Daily",
            QuestType.Weekly => "📅 Weekly",
            QuestType.GuildQuest => "⚔️ Guild",
            QuestType.CharacterQuest => "👤 Personal",
            QuestType.RandomQuest => "🎲 Random",
            _ => "📋 Quest"
        };

        public string DifficultyDisplay =>
            RequiredLevel switch
            {
                <= 5 => "🟢 Novice",
                <= 10 => "🟡 Journeyman",
                <= 15 => "🟠 Veteran",
                <= 18 => "🔴 Master",
                _ => "💀 Legendary"
            };

        public string ProgressBar
        {
            get
            {
                var filled = (int)(ProgressPercent / 10);
                var empty = 10 - filled;
                return $"[{new string('█', filled)}" +
                       $"{new string('░', empty)}] " +
                       $"{ProgressPercent:F0}%";
            }
        }

        public string QuestSummary =>
            $"{TypeDisplay} {Title} | " +
            $"{StatusDisplay} | " +
            $"Level {RequiredLevel}+ | " +
            $"{DifficultyDisplay} | " +
            $"Progress: {ProgressBar}";

        public override string ToString() => QuestSummary;
    }
}
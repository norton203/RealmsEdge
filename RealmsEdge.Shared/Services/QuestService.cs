using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Quests;
using RealmsEdge.Shared.Models.World;

namespace RealmsEdge.Shared.Services
{
    public class QuestUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> EventLog { get; set; } = new();
        public List<Quest> CompletedQuests { get; set; } = new();
        public List<Quest> FailedQuests { get; set; } = new();
        public List<Quest> NewQuestsUnlocked { get; set; } = new();
        public bool AnyQuestCompleted => CompletedQuests.Any();
        public bool AnyQuestFailed => FailedQuests.Any();
        public SoundEffect? SoundToPlay { get; set; }
    }

    public class QuestService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly CharacterService _characterService;
        private readonly DiceService _diceService;

        // Quest registry — all quests in the game
        private readonly Dictionary<Guid, Quest> _quests = new();

        // Player quest tracking
        private readonly Dictionary<Guid, List<Guid>>
            _playerActiveQuests = new();

        public QuestService(
            CharacterService characterService,
            DiceService diceService)
        {
            _characterService = characterService;
            _diceService      = diceService;
            SeedStarterQuests();
        }

        // =====================
        // Quest Registry
        // =====================

        public void RegisterQuest(Quest quest)
            => _quests[quest.Id] = quest;

        public Quest? GetQuest(Guid questId)
            => _quests.TryGetValue(questId, out var quest)
                ? quest : null;

        public List<Quest> GetAllQuests()
            => _quests.Values.ToList();

        public List<Quest> GetQuestsByType(QuestType type)
            => _quests.Values
                .Where(q => q.Type == type)
                .ToList();

        public List<Quest> GetAvailableQuestsForPlayer(
            PlayerCharacter player)
            => _quests.Values
                .Where(q =>
                    q.Status == QuestStatus.Available &&
                    q.IsAvailableTo(player) &&
                    !player.HasQuest(q.Id) &&
                    !player.HasCompletedQuest(q.Id))
                .OrderBy(q => q.RequiredLevel)
                .ToList();

        public List<Quest> GetActiveQuestsForPlayer(
            PlayerCharacter player)
            => _quests.Values
                .Where(q => player.HasQuest(q.Id))
                .ToList();

        public List<Quest> GetCompletedQuestsForPlayer(
            PlayerCharacter player)
            => _quests.Values
                .Where(q => player.HasCompletedQuest(q.Id))
                .ToList();

        public List<Quest> GetReadyToCompleteQuests(
            PlayerCharacter player)
            => GetActiveQuestsForPlayer(player)
                .Where(q => q.AllRequiredComplete)
                .ToList();

        // =====================
        // Accept Quest
        // =====================

        public (bool Success, string Message) AcceptQuest(
            Guid questId,
            PlayerCharacter player)
        {
            var quest = GetQuest(questId);
            if (quest == null)
                return (false, "Quest not found.");

            if (!quest.IsAvailableTo(player))
                return (false,
                    quest.UnavailableReason(player));

            if (player.HasQuest(questId))
                return (false,
                    $"You already have " +
                    $"'{quest.Title}' in your journal.");

            if (player.HasCompletedQuest(questId) &&
                !quest.CanRepeat)
                return (false,
                    $"You have already completed " +
                    $"'{quest.Title}'.");

            quest.Accept(player);
            _characterService.UpdateCharacter(player);

            return (true,
                $"📜 Quest accepted: {quest.Title}. " +
                $"{quest.FlavorText ?? quest.Description}");
        }

        // =====================
        // Complete Quest
        // =====================

        public QuestUpdateResult CompleteQuest(
            Guid questId,
            PlayerCharacter player,
            Guid? chosenItemId = null)
        {
            var result = new QuestUpdateResult();
            var quest = GetQuest(questId);

            if (quest == null)
            {
                result.Message = "Quest not found.";
                return result;
            }

            if (!player.HasQuest(questId))
            {
                result.Message =
                    "You do not have this quest.";
                return result;
            }

            if (!quest.AllRequiredComplete)
            {
                result.Message =
                    $"Not all objectives are complete. " +
                    $"Progress: {quest.ProgressBar}";
                return result;
            }

            // Handle item choice if needed
            if (quest.MainReward?.HasItemChoice == true &&
                !quest.MainReward.ItemChoiceMade)
            {
                if (!chosenItemId.HasValue)
                {
                    result.Message =
                        "Please choose your item reward " +
                        "before completing the quest.";
                    result.EventLog.Add(
                        "Choose one of the following items:");

                    foreach (var item in
                        quest.MainReward.ChoiceItems)
                        result.EventLog.Add(
                            $"  • {item.Name} " +
                            $"({item.Rarity}) — " +
                            $"{item.Description}");

                    return result;
                }

                quest.MainReward.ChooseItem(
                    chosenItemId.Value);
            }

            // Apply main reward
            if (quest.MainReward != null)
            {
                var rewardLog = quest.MainReward
                    .ApplyTo(player);
                result.EventLog.AddRange(rewardLog);
            }

            // Apply optional reward if all complete
            if (quest.OptionalReward != null &&
                quest.AllObjectivesComplete)
            {
                result.EventLog.Add(
                    "⭐ All optional objectives complete! " +
                    "Bonus reward:");
                var optLog = quest.OptionalReward
                    .ApplyTo(player, true);
                result.EventLog.AddRange(optLog);
            }

            // First time completion bonus
            if (quest.FirstTimeReward != null &&
                quest.TimesCompleted == 0)
            {
                result.EventLog.Add(
                    "🌟 First completion bonus!");
                var firstLog = quest.FirstTimeReward
                    .ApplyTo(player);
                result.EventLog.AddRange(firstLog);
            }

            // Complete the quest
            quest.Complete(player);
            result.CompletedQuests.Add(quest);
            result.Success     = true;
            result.SoundToPlay = SoundEffect.QuestComplete;

            result.EventLog.Insert(0,
                $"🏆 Quest Complete: {quest.Title}!");

            if (quest.CompletionText != null)
                result.EventLog.Add(quest.CompletionText);

            // Check for newly unlocked quests
            var unlocked = CheckForUnlockedQuests(
                player, quest);
            result.NewQuestsUnlocked.AddRange(unlocked);

            foreach (var newQuest in unlocked)
                result.EventLog.Add(
                    $"📜 New quest available: " +
                    $"{newQuest.Title}!");

            // Check level up
            if (player.CanLevelUp)
            {
                var levelResult = _characterService
                    .LevelUpCharacter(player.Id);
                if (levelResult.Success)
                    result.EventLog.Add(levelResult.Message);
            }

            result.Message =
                $"Quest '{quest.Title}' completed!";

            _characterService.UpdateCharacter(player);
            return result;
        }

        // =====================
        // Abandon Quest
        // =====================

        public (bool Success, string Message) AbandonQuest(
            Guid questId,
            PlayerCharacter player)
        {
            var quest = GetQuest(questId);
            if (quest == null)
                return (false, "Quest not found.");

            if (!player.HasQuest(questId))
                return (false,
                    "You do not have this quest.");

            if (quest.Type == QuestType.Story ||
                quest.Type == QuestType.ChapterQuest)
                return (false,
                    "Story quests cannot be abandoned.");

            quest.Abandon(player);
            _characterService.UpdateCharacter(player);

            return (true,
                $"Quest '{quest.Title}' abandoned.");
        }

        // =====================
        // Kill Progress
        // =====================

        public QuestUpdateResult NotifyKill(
            PlayerCharacter player,
            CharacterRace enemyRace,
            CharacterClass enemyClass,
            string enemyName,
            bool isBoss)
        {
            var result = new QuestUpdateResult
            { Success = true };
            var active = GetActiveQuestsForPlayer(player);

            foreach (var quest in active)
            {
                var advanced = quest.CheckKillProgress(
                    enemyRace, enemyClass,
                    enemyName, isBoss);

                foreach (var obj in advanced)
                {
                    result.EventLog.Add(
                        $"📋 Quest updated: " +
                        $"{quest.Title} — " +
                        $"{obj.FullDisplay}");

                    if (quest.AllRequiredComplete)
                    {
                        quest.Status =
                            QuestStatus.ReadyToComplete;
                        result.EventLog.Add(
                            $"✅ {quest.Title} is ready " +
                            $"to complete!");
                    }
                }
            }

            // Check for expired quests
            CheckExpiredQuests(player, result);

            _characterService.UpdateCharacter(player);
            return result;
        }

        // =====================
        // Location Progress
        // =====================

        public QuestUpdateResult NotifyLocationVisit(
            PlayerCharacter player,
            Guid locationId,
            Guid? roomId = null)
        {
            var result = new QuestUpdateResult
            { Success = true };
            var active = GetActiveQuestsForPlayer(player);

            foreach (var quest in active)
            {
                var advanced = quest
                    .CheckLocationProgress(
                        locationId, roomId);

                foreach (var obj in advanced)
                {
                    result.EventLog.Add(
                        $"📋 Quest updated: " +
                        $"{quest.Title} — " +
                        $"{obj.FullDisplay}");

                    if (quest.AllRequiredComplete)
                    {
                        quest.Status =
                            QuestStatus.ReadyToComplete;
                        result.EventLog.Add(
                            $"✅ {quest.Title} is ready " +
                            $"to complete!");
                    }
                }
            }

            _characterService.UpdateCharacter(player);
            return result;
        }

        // =====================
        // Item Progress
        // =====================

        public QuestUpdateResult NotifyItemPickup(
            PlayerCharacter player,
            Guid itemId,
            string itemName)
        {
            var result = new QuestUpdateResult
            { Success = true };
            var active = GetActiveQuestsForPlayer(player);

            foreach (var quest in active)
            {
                var advanced = quest.CheckItemProgress(
                    itemId, itemName);

                foreach (var obj in advanced)
                {
                    result.EventLog.Add(
                        $"📋 Quest updated: " +
                        $"{quest.Title} — " +
                        $"{obj.FullDisplay}");

                    if (quest.AllRequiredComplete)
                    {
                        quest.Status =
                            QuestStatus.ReadyToComplete;
                        result.EventLog.Add(
                            $"✅ {quest.Title} ready " +
                            $"to complete!");
                    }
                }
            }

            _characterService.UpdateCharacter(player);
            return result;
        }

        // =====================
        // NPC Progress
        // =====================

        public QuestUpdateResult NotifyNpcInteraction(
            PlayerCharacter player,
            Guid npcId)
        {
            var result = new QuestUpdateResult
            { Success = true };
            var active = GetActiveQuestsForPlayer(player);

            foreach (var quest in active)
            {
                var advanced = quest
                    .CheckNpcProgress(npcId);

                foreach (var obj in advanced)
                {
                    result.EventLog.Add(
                        $"📋 Quest updated: " +
                        $"{quest.Title} — " +
                        $"{obj.FullDisplay}");

                    if (quest.AllRequiredComplete)
                    {
                        quest.Status =
                            QuestStatus.ReadyToComplete;
                        result.EventLog.Add(
                            $"✅ {quest.Title} ready " +
                            $"to complete!");
                    }
                }
            }

            _characterService.UpdateCharacter(player);
            return result;
        }

        // =====================
        // Quest Journal
        // =====================

        public string GetQuestJournal(
            PlayerCharacter player)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"📖 Quest Journal — " +
                $"{player.Name}");
            sb.AppendLine(new string('─', 40));

            var active = GetActiveQuestsForPlayer(player);
            if (active.Any())
            {
                sb.AppendLine("⚔️ Active Quests:");
                foreach (var quest in active
                    .OrderByDescending(q =>
                        q.Status ==
                        QuestStatus.ReadyToComplete)
                    .ThenBy(q => q.RequiredLevel))
                {
                    sb.AppendLine(
                        $"  {quest.StatusDisplay} " +
                        $"{quest.Title}");
                    sb.AppendLine(
                        $"    Progress: {quest.ProgressBar}");

                    foreach (var obj in
                        quest.RequiredObjectives)
                        sb.AppendLine(
                            $"    {obj.FullDisplay}");

                    if (quest.OptionalObjectives.Any())
                    {
                        sb.AppendLine(
                            "    Optional:");
                        foreach (var obj in
                            quest.OptionalObjectives)
                            sb.AppendLine(
                                $"    {obj.FullDisplay}");
                    }

                    if (quest.HasTimeLimit)
                        sb.AppendLine(
                            $"    {quest.TimeRemainingDisplay}");
                }
            }
            else
            {
                sb.AppendLine(
                    "  No active quests. " +
                    "Visit a town to find adventure!");
            }

            sb.AppendLine(new string('─', 40));

            var ready = GetReadyToCompleteQuests(player);
            if (ready.Any())
            {
                sb.AppendLine(
                    "✅ Ready to complete:");
                foreach (var quest in ready)
                    sb.AppendLine(
                        $"  • {quest.Title} — " +
                        $"Return to " +
                        $"{quest.TurnInNpcName ?? "quest giver"}");
            }

            var completed =
                GetCompletedQuestsForPlayer(player);
            sb.AppendLine(
                $"\n🏆 Completed: " +
                $"{completed.Count} quests");

            return sb.ToString().Trim();
        }

        // =====================
        // Daily Quest Reset
        // (L.O.R.D inspired)
        // =====================

        public List<string> ProcessDailyReset()
        {
            var log = new List<string>();

            var dailyQuests = _quests.Values
                .Where(q => q.Type == QuestType.Daily ||
                            q.Type == QuestType.Weekly)
                .ToList();

            foreach (var quest in dailyQuests)
            {
                if (quest.NextAvailableAt.HasValue &&
                    DateTime.UtcNow >=
                    quest.NextAvailableAt.Value)
                {
                    quest.Status =
                        QuestStatus.Available;
                    quest.NextAvailableAt = null;

                    foreach (var obj in quest.Objectives)
                        obj.Reset();

                    log.Add(
                        $"🔄 Daily quest reset: " +
                        $"{quest.Title}");
                }
            }

            return log;
        }

        // =====================
        // Random Quest Generator
        // (L.O.R.D inspired)
        // =====================

        public Quest GenerateRandomQuest(
            PlayerCharacter player,
            WorldLocation location)
        {
            var roll = _diceService
                .Roll(DiceType.D6).Total;

            var reward = new QuestReward
            {
                ExperiencePoints =
                    player.Level * 150,
                GoldReward       =
                    _diceService.Roll(
                        DiceType.D6,
                        player.Level).Total
            };

            return roll switch
            {
                1 or 2 => Quest.CreateKillQuest(
                    $"Clear the {location.Name}",
                    $"Dangerous creatures have been " +
                    $"spotted near {location.Name}. " +
                    $"Clear them out.",
                    "Enemy",
                    CharacterRace.Goblin,
                    _diceService.Roll(
                        DiceType.D4, 1, 2).Total,
                    player.Level,
                    reward),

                3 or 4 => Quest.CreateExploreQuest(
                    $"Scout {location.Name}",
                    $"We need someone brave enough " +
                    $"to explore {location.Name} " +
                    $"and report back.",
                    location.Id,
                    location.Name,
                    player.Level,
                    reward),

                _ => Quest.CreateDailyQuest(
                    $"Daily Bounty: {location.Name}",
                    $"The town notice board has a " +
                    $"bounty posted for work near " +
                    $"{location.Name}.",
                    new QuestObjective
                    {
                        Title         =
                            "Complete the bounty",
                        Type          =
                            ObjectiveType.KillCount,
                        RequiredCount =
                            player.Level * 2,
                        TargetName    = "Enemy"
                    },
                    reward)
            };
        }

        // =====================
        // Starter Quests
        // =====================

        private void SeedStarterQuests()
        {
            // Tutorial kill quest
            var firstBlood = Quest.CreateKillQuest(
                "First Blood",
                "The forest paths near Ashenvale are " +
                "plagued by goblins. Prove yourself " +
                "by defeating your first enemy.",
                "Goblin",
                CharacterRace.Goblin,
                1,
                1,
                new QuestReward
                {
                    ExperiencePoints = 100,
                    GoldReward       = 1,
                    SilverReward     = 5
                });
            firstBlood.FlavorText =
                "Every legend starts with a single " +
                "step. Take yours.";
            firstBlood.Status = QuestStatus.Available;
            RegisterQuest(firstBlood);

            // Tutorial exploration quest
            var explorer = Quest.CreateExploreQuest(
                "Into the Unknown",
                "The world of Realm's Edge awaits. " +
                "Travel to the Sunken Crypt and " +
                "see what lies within.",
                Guid.Empty,             // Set to real ID later
                "The Sunken Crypt",
                1,
                new QuestReward
                {
                    ExperiencePoints = 150,
                    GoldReward       = 2
                });
            explorer.FlavorText =
                "Curiosity is the adventurer's " +
                "greatest weapon.";
            RegisterQuest(explorer);

            // Daily bounty
            var dailyBounty = Quest.CreateDailyQuest(
                "Daily Bounty",
                "The town of Ashenvale always has " +
                "work for capable adventurers. " +
                "Check the notice board each day.",
                new QuestObjective
                {
                    Title         = "Defeat 5 enemies",
                    Type          = ObjectiveType.KillCount,
                    RequiredCount = 5,
                    TargetName    = "Enemy"
                },
                new QuestReward
                {
                    ExperiencePoints = 200,
                    GoldReward       = 3,
                    SilverReward     = 0
                });
            RegisterQuest(dailyBounty);

            // Class specific quest — Rogue
            var rogueBounty = new Quest
            {
                Title          = "Shadow Work",
                Description    =
                    "The Thieves Guild needs a skilled " +
                    "hand. Prove your worth by " +
                    "completing a delicate task.",
                Type           = QuestType.GuildQuest,
                Status         = QuestStatus.Available,
                RequiredLevel  = 1,
                RequiredClasses = new()
                    { CharacterClass.Rogue,
                      CharacterClass.Assassin },
                FlavorText     =
                    "Not all battles are fought " +
                    "in the open.",
                MainReward = new QuestReward
                {
                    ExperiencePoints = 300,
                    GoldReward       = 5,
                    SkillUnlocked    = "Pickpocket",
                    UnlocksTitle     = "the Shadow"
                }
            };
            rogueBounty.AddObjective(
                new QuestObjective
                {
                    Title         = "Speak to the " +
                                    "Guild Contact",
                    Type          = ObjectiveType.TalkToNpc,
                    RequiredCount = 1,
                    TargetName    = "Guild Contact"
                });
            RegisterQuest(rogueBounty);

            // Paladin holy quest
            var holyQuest = new Quest
            {
                Title           = "Purge the Crypt",
                Description     =
                    "The Sunken Crypt is an affront " +
                    "to all that is holy. Cleanse it " +
                    "of the undead within.",
                Type            = QuestType.Exterminate,
                Status          = QuestStatus.Available,
                RequiredLevel   = 2,
                RequiredClasses = new()
                    { CharacterClass.Paladin,
                      CharacterClass.Cleric },
                RequiredAlignment =
                    Alignment.LawfulGood,
                FlavorText      =
                    "The light of righteousness " +
                    "shall purge this darkness.",
                MainReward = new QuestReward
                {
                    ExperiencePoints = 500,
                    GoldReward       = 8,
                    StatusGranted    =
                        CharacterStatus.Blessed,
                    StatusDuration   = 50,
                    UnlocksTitle     = "the Righteous"
                }
            };
            holyQuest.AddObjective(
                new QuestObjective
                {
                    Title         = "Defeat 10 Undead " +
                                    "in the Sunken Crypt",
                    Type          = ObjectiveType.KillEnemy,
                    RequiredCount = 10,
                    TargetRace    = CharacterRace.Undead,
                    TargetName    = "Undead"
                });
            RegisterQuest(holyQuest);
        }

        // =====================
        // Private Helpers
        // =====================

        private List<Quest> CheckForUnlockedQuests(
            PlayerCharacter player,
            Quest completedQuest)
        {
            var unlocked = new List<Quest>();

            foreach (var quest in _quests.Values
                .Where(q =>
                    q.Status == QuestStatus.Locked ||
                    q.Status == QuestStatus.Hidden))
            {
                if (quest.RequiredQuestId ==
                    completedQuest.Id &&
                    quest.IsAvailableTo(player))
                {
                    quest.Status = QuestStatus.Available;
                    unlocked.Add(quest);
                }

                if (quest.RequiredQuestIds.Contains(
                    completedQuest.Id) &&
                    quest.RequiredQuestIds.All(id =>
                        player.HasCompletedQuest(id)) &&
                    quest.IsAvailableTo(player))
                {
                    quest.Status = QuestStatus.Available;
                    unlocked.Add(quest);
                }
            }

            return unlocked;
        }

        private void CheckExpiredQuests(
            PlayerCharacter player,
            QuestUpdateResult result)
        {
            var active =
                GetActiveQuestsForPlayer(player);

            foreach (var quest in active
                .Where(q => q.IsExpired))
            {
                quest.Fail(player);
                result.FailedQuests.Add(quest);
                result.EventLog.Add(
                    $"⏰ Quest expired: {quest.Title}!");

                if (quest.FailureText != null)
                    result.EventLog.Add(
                        quest.FailureText);
            }
        }
    }
}
using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Interfaces;
using RealmsEdge.Shared.Models;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Combat;
using RealmsEdge.Shared.Models.Quests;
using RealmsEdge.Shared.Models.Session;
using RealmsEdge.Shared.Models.World;
using PartyModels = RealmsEdge.Shared.Models.Party;

namespace RealmsEdge.Shared.Services
{
    public class GameStateManager
    {
        // =====================
        // Dependencies
        // =====================

        private readonly CharacterService _characterService;
        private readonly PartyService _partyService;
        private readonly WorldService _worldService;
        private readonly NavigationService _navigationService;
        private readonly CombatService _combatService;
        private readonly EncounterService _encounterService;
        private readonly QuestService _questService;
        private readonly DiceService _diceService;
        private readonly ISoundService _soundService;
        private readonly IDatabaseService _database;
        private readonly ISettingsService _settings;

        // =====================
        // Session
        // =====================

        private GameSession? _session;

        public GameSession Session => _session
            ?? throw new InvalidOperationException(
                "No active game session.");

        public bool HasSession => _session != null;

        public GameState CurrentState =>
            _session?.CurrentState
            ?? GameState.Initialising;

        // =====================
        // Events
        // (UI subscribes to these)
        // =====================

        public event Action? OnStateChanged;
        public event Action<string>? OnLogMessage;
        public event Action<GameNotification>?
            OnNotification;
        public event Action<SoundEffect>? OnPlaySound;
        public event Action? OnSessionUpdated;

        public GameStateManager(
    CharacterService characterService,
    PartyService partyService,
    WorldService worldService,
    NavigationService navigationService,
    CombatService combatService,
    EncounterService encounterService,
    QuestService questService,
    DiceService diceService,
    ISoundService soundService,
    IDatabaseService database,
    ISettingsService settings)
        {
            _characterService  = characterService;
            _partyService      = partyService;
            _worldService      = worldService;
            _navigationService = navigationService;
            _combatService     = combatService;
            _encounterService  = encounterService;
            _questService      = questService;
            _diceService       = diceService;
            _soundService      = soundService;
            _database          = database;
            _settings          = settings;
        }

        // =====================
        // Session Management
        // =====================

        public GameSession StartNewSession(
            string sessionName = "New Game")
        {
            _session = new GameSession
            {
                SessionName = sessionName
            };

            TransitionTo(GameState.MainMenu);
            Log("⚔️ Welcome to Realm's Edge!",
                GameLogType.System);

            return _session;
        }

        public void EndSession()
        {
            if (_session == null) return;
            _session.IsActive = false;
            _session          = null;
        }

        // =====================
        // Character Management
        // =====================

        public async Task<(bool Success,
            string Message,
            PlayerCharacter? Character)>
            CreateCharacter(
                string playerName,
                string characterName,
                CharacterRace race,
                CharacterClass charClass,
                Alignment alignment,
                Gender gender,
                List<DiceRoll> statRolls)
        {
            TransitionTo(GameState.Loading);

            var result = _characterService
                .CreateCharacter(
                    playerName, characterName,
                    race, charClass,
                    alignment, gender, statRolls);

            if (!result.Result.IsValid)
            {
                TransitionTo(GameState.CharacterCreate);
                return (false,
                    result.Result.ErrorSummary, null);
            }

            Session.ActivePlayer = result.Character;
            Session.MarkDirty();

            // Spawn in starting location
            var spawnResult = _worldService
                .SpawnPlayer(result.Character!.Id);

            var location = _worldService
                .GetPlayerLocation(
                    result.Character!.Id);

            if (location != null)
                Session.SetLocation(location);

            Log(
                $"✏️ {result.Character!.Name} " +
                $"enters the world!",
                GameLogType.System);

            Notify(
                $"Welcome, {result.Character.Name}!",
                NotificationType.Success);

            await PlaySound(SoundEffect.MenuSelect);
            TransitionTo(GameState.Exploring);

            await Task.CompletedTask;
            return (true,
                spawnResult.Message,
                result.Character);
        }

        public (bool Success, string Message)
            SelectCharacter(Guid characterId)
        {
            var character = _characterService
                .GetCharacter(characterId);

            if (character == null)
                return (false, "Character not found.");

            Session.ActivePlayer = character;

            var spawnResult = _worldService
                .SpawnPlayer(characterId);

            var location = _worldService
                .GetPlayerLocation(characterId);

            if (location != null)
                Session.SetLocation(location);

            Log(
                $"👤 {character.Name} continues " +
                $"their adventure.",
                GameLogType.System);

            TransitionTo(GameState.Exploring);
            return (true, spawnResult.Message);
        }

        // =====================
        // World Navigation
        // =====================

       
        public async Task<(bool Success, string Message)> TravelTo(Guid destinationId)
        {
            if (!Session.HasActivePlayer)
                return (false, "No active player.");

            TransitionTo(GameState.Travelling);

            var result = await _worldService.TravelTo(
                Session.ActivePlayer!.Id,
                destinationId);

            if (!result.Success)
            {
                TransitionTo(GameState.WorldMap);
                return (false, result.Message);
            }

            if (result.Destination != null)
                Session.SetLocation(
                    result.Destination);

            // Update quest progress
            var questResult =
                _questService.NotifyLocationVisit(
                    Session.ActivePlayer!,
                    destinationId);

            ProcessQuestResult(questResult);

            Log(result.Message, GameLogType.World);
            Session.TotalLocationsVisited++;

            if (result.SoundToPlay.HasValue)
                await PlaySound(result.SoundToPlay.Value);

            // Handle travel encounters
            foreach (var encounter in
                result.TriggeredEncounters)
            {
                var encounterResult = await
                    HandleEncounter(encounter);

                if (encounterResult != null)
                    Log(encounterResult.Message,
                        GameLogType.Combat);
            }

            TransitionTo(GameState.Exploring);
            NotifyStateChanged();
            _ = AutoSaveAsync();
            return (true, result.Message);
        }

        public (bool Success, string Message)
            MoveInDirection(Direction direction)
        {
            if (!Session.HasActivePlayer)
                return (false, "No active player.");

            var result = _navigationService.Move(
                Session.ActivePlayer!.Id, direction);

            if (!result.Success)
                return (false, result.Message);

            if (result.Room != null)
            {
                Session.CurrentRoom = result.Room;
                Session.TotalRoomsExplored++;
            }

            // Log all events
            foreach (var entry in result.EventLog)
                Log(entry, GameLogType.World);

            // Update quest progress for room
            if (result.Room != null &&
                Session.CurrentLocation != null)
            {
                var questResult = _questService
                    .NotifyLocationVisit(
                        Session.ActivePlayer!,
                        Session.CurrentLocation.Id,
                        result.Room.Id);

                ProcessQuestResult(questResult);
            }

            if (result.SoundToPlay.HasValue)
                _ = PlaySound(result.SoundToPlay.Value);

            NotifyStateChanged();
            return (true,
                _navigationService
                    .DescribeCurrentRoom(
                        Session.ActivePlayer!.Id));
        }




        public (bool Success, string Message) Search()
        {
            if (!Session.HasActivePlayer)
                return (false, "No active player.");

            var result = _navigationService
                .SearchRoom(Session.ActivePlayer!.Id);

            foreach (var entry in result.EventLog)
                Log(entry, GameLogType.World);

            return (true, result.Message);
        }

        // =====================
        // Combat Management
        // =====================

        public async Task<CombatResult>
            StartCombatWithEnemies(
            List<NpcCharacter> enemies,
            string battleName = "Encounter",
            bool isSurprise = false)
        {
            if (!Session.HasActivePlayer)
                return new CombatResult
                {
                    IsOver  = true,
                    Message = "No active player."
                };

            Session.TotalCombatsEntered++;

            var combat = _combatService.StartCombat(
                battleName,
                Session.AllPlayers,
                enemies,
                isSurprise,
                Session.ActiveParty);

            Session.StartCombat(combat);

            Log(
                $"⚔️ Combat begins: {battleName}!",
                GameLogType.Combat);

            await PlaySound(SoundEffect.BattleMusic);
            NotifyStateChanged();

            // Process until player input needed
            return await ProcessCombatUntilPlayerTurn(
                combat);
        }

        public async Task<CombatResult>
            ProcessCombatAction(
            CombatActionType actionType,
            int actorCombatId,
            int? targetCombatId = null,
            string? spellName = null,
            Guid? itemId = null)
        {
            if (Session.ActiveCombat == null)
                return new CombatResult
                {
                    IsOver  = true,
                    Message = "No active combat."
                };

            var result = await _combatService
                .ProcessPlayerAction(
                    Session.ActiveCombat,
                    actorCombatId,
                    actionType,
                    targetCombatId,
                    spellName,
                    itemId,
                    Session.ActiveParty);

            // Log combat events
            foreach (var entry in result.EventLog)
                Log(entry, GameLogType.Combat);

            if (result.SoundToPlay.HasValue)
                await PlaySound(result.SoundToPlay.Value);

            // Handle combat end
            if (result.IsOver)
                await HandleCombatEnd(result);
            else
                TransitionTo(
                    GameState.CombatPlayerTurn);

            NotifyStateChanged();
            _ = AutoSaveAsync();
            return result;
        }

        private async Task<CombatResult>
            ProcessCombatUntilPlayerTurn(
            CombatState combat)
        {
            CombatResult? result = null;

            // Keep processing enemy turns until
            // player turn or combat ends
            while (combat.CurrentCombatant != null &&
                   !combat.CurrentCombatant
                       .IsPlayerControlled &&
                   !combat.AllEnemiesDefeated &&
                   !combat.AllPlayersDefeated)
            {
                result = await _combatService
                    .ProcessEnemyTurn(
                        combat,
                        Session.ActiveParty);

                if (result.IsOver)
                {
                    await HandleCombatEnd(result);
                    return result;
                }
            }

            TransitionTo(GameState.CombatPlayerTurn);

            return result ?? new CombatResult
            {
                IsOver  = false,
                Outcome = CombatPhase.PlayerDecision,
                Message = "Your turn!"
            };
        }

        private async Task HandleCombatEnd(
    CombatResult result)
        {
            if (result.Outcome == CombatPhase.Victory)
            {
                Session.TotalCombatsWon++;
                TransitionTo(GameState.CombatVictory);

                if (Session.ActivePlayer != null &&
                    Session.ActiveCombat != null)
                {
                    var player = Session.ActivePlayer;
                    var enemies = Session.ActiveCombat
                        .EnemyCombatants
                        .Where(e => !e.IsAlive)
                        .Select(e => e.Character as NpcCharacter)
                        .Where(e => e != null)
                        .Select(e => e!)
                        .ToList();

                    // =====================
                    // Notify Quest System
                    // =====================

                    foreach (var enemy in enemies)
                    {
                        var questResult = _questService.NotifyKill(
                            player,
                            enemy.Race,
                            enemy.Class,
                            enemy.Name,
                            enemy.IsBoss);

                        ProcessQuestResult(questResult);
                    }

                    // =====================
                    // Apply XP
                    // =====================

                    if (result.TotalXpAwarded > 0)
                    {
                        player.AddExperience(result.TotalXpAwarded);
                        Session.TotalXpEarned += result.TotalXpAwarded;
                        Log($"✨ {player.Name} gains " +
                            $"{result.TotalXpAwarded:N0} XP.",
                            GameLogType.Combat);
                    }

                    // =====================
                    // Apply Gold
                    // =====================

                    if (result.TotalGoldAwarded > 0)
                    {
                        player.AddCurrency(
                            result.TotalGoldAwarded, 0, 0);
                        Session.TotalGoldEarned +=
                            result.TotalGoldAwarded;
                        Log($"💰 Found {result.TotalGoldAwarded}g.",
                            GameLogType.Combat);
                    }

                    // =====================
                    // Roll Item Drops
                    // =====================

                    var loot = LootRoller.Roll(enemies);

                    if (loot.Gold > 0 || loot.Silver > 0 ||
                        loot.Copper > 0)
                    {
                        player.AddCurrency(
                            loot.Gold, loot.Silver, loot.Copper);
                        Session.TotalGoldEarned += loot.Gold;
                        Log($"💰 Looted {loot.CurrencyDisplay}.",
                            GameLogType.Combat);
                    }

                    foreach (var item in loot.Items)
                    {
                        var added = player.AddItem(item);
                        result.LootDrops.Add(item);
                        Session.TotalItemsLooted++;
                        Log(added
                            ? $"🎁 {player.Name} picks up " +
                              $"{item.Name}."
                            : $"⚠️ Inventory full — " +
                              $"{item.Name} left behind.",
                            GameLogType.Combat);
                    }

                    // =====================
                    // Persist & Level Up
                    // =====================

                    _characterService.UpdateCharacter(player);

                    if (player.CanLevelUp)
                        await HandleLevelUp();
                }

                Notify("Victory!",
                    NotificationType.Success);
                Log($"🏆 {result.Message}",
                    GameLogType.Combat);
                await PlaySound(SoundEffect.LevelUp);
                _ = AutoSaveAsync();
            }
            else if (result.Outcome == CombatPhase.Defeat)
            {
                Session.TotalCombatsLost++;
                Session.TotalDeaths++;
                TransitionTo(GameState.CombatDefeat);
                Notify("Defeated...",
                    NotificationType.Danger);
                Log($"💀 {result.Message}",
                    GameLogType.Combat);
                await PlaySound(SoundEffect.CombatDeath);
            }
            else if (result.Outcome == CombatPhase.Fled)
            {
                Session.TotalCombatsFled++;
                TransitionTo(GameState.CombatFled);
                Log($"🏃 {result.Message}",
                    GameLogType.Combat);
            }
        }

        // =====================
        // NPC Interaction
        // =====================

        public (bool Success, string Message)
            TalkToNpc(
            Guid npcId,
            Guid locationId)
        {
            if (!Session.HasActivePlayer)
                return (false, "No active player.");

            var result = _navigationService
                .InteractWithNpc(
                    Session.ActivePlayer!.Id,
                    npcId);

            if (!result.Success)
                return (false, result.Message);

            var npc = _worldService
                .GetNpcAtLocation(locationId, npcId);

            if (npc != null)
            {
                Session.StartDialogue(npc);
                Session.TotalNpcsSpokenTo++;

                // Notify quest system
                var questResult = _questService
                    .NotifyNpcInteraction(
                        Session.ActivePlayer!, npcId);

                ProcessQuestResult(questResult);

                Log(
                    $"💬 {result.Message}",
                    GameLogType.Dialogue);
            }

            NotifyStateChanged();
            return (true, result.Message);
        }

        public void EndDialogue()
        {
            Session.EndDialogue();
            NotifyStateChanged();
        }

        // =====================
        // Rest Management
        // =====================

        public async Task<(bool Success,
            string Message)> TakeShortRest()
        {
            if (!Session.HasActivePlayer)
                return (false, "No active player.");

            if (Session.IsInCombat)
                return (false,
                    "Cannot rest during combat!");

            TransitionTo(GameState.Resting);

            string message;

            if (Session.IsInParty &&
                Session.ActiveParty != null)
            {
                var results = _partyService
                    .PartyShortRest(
                        Session.ActiveParty.Id);
                message = string.Join("\n", results);
            }
            else
            {
                message = _characterService.ShortRest(
                    Session.ActivePlayer!.Id);
            }

            await PlaySound(SoundEffect.HealReceived);
            Log(message, GameLogType.Info);
            Session.MarkDirty();
            TransitionTo(GameState.Exploring);

            await Task.CompletedTask;
            _ = AutoSaveAsync();
            return (true, message);
        }

        public async Task<(bool Success,
            string Message)> TakeLongRest(
            Guid locationId)
        {
            if (!Session.HasActivePlayer)
                return (false, "No active player.");

            if (Session.IsInCombat)
                return (false,
                    "Cannot rest during combat!");

            var innResult = _worldService.UseInn(
                Session.ActivePlayer!.Id, locationId);

            if (!innResult.Success)
                return (false, innResult.Message);

            TransitionTo(GameState.Resting);
            await PlaySound(SoundEffect.HealReceived);
            Log(innResult.Message, GameLogType.Info);
            Session.MarkDirty();
            TransitionTo(GameState.Exploring);

            await Task.CompletedTask;
            _ = AutoSaveAsync();
            return (true, innResult.Message);
        }

        // =====================
        // Quest Management
        // =====================

       

        public async Task<QuestUpdateResult>
            CompleteQuest(
            Guid questId,
            Guid? chosenItemId = null)
        {
            if (!Session.HasActivePlayer)
                return new QuestUpdateResult
                {
                    Message = "No active player."
                };

            var result = _questService.CompleteQuest(
                questId,
                Session.ActivePlayer!,
                chosenItemId);

            foreach (var entry in result.EventLog)
                Log(entry, GameLogType.Quest);

            if (result.Success)
            {
                Session.TotalQuestsCompleted++;
                Session.MarkDirty();

                Notify(
                    $"Quest complete: " +
                    $"{result.CompletedQuests
                        .FirstOrDefault()?.Title}",
                    NotificationType.Quest);

                if (result.SoundToPlay.HasValue)
                    await PlaySound(
                        result.SoundToPlay.Value);

                // Check level up
                if (Session.ActivePlayer!.CanLevelUp)
                    await HandleLevelUp();
            }

            NotifyStateChanged();
            _ = AutoSaveAsync();
            return result;
        }


        // =====================
        // Save / Load
        // =====================

        public bool AutoSaveEnabled
        {
            get => _settings.AutoSaveEnabled;
            set => _settings.AutoSaveEnabled = value;
        }

        public async Task<List<SessionSaveData>> GetSaveSlotsAsync()
            => await _database.GetAllSaveSlotsAsync();

        public async Task DeleteSaveAsync(int slot)
            => await _database.DeleteSaveAsync(slot);

        public async Task<(bool Success, string Message)>
            SaveGameAsync(int slot = 1)
        {
            if (!HasSession || Session.ActivePlayer == null)
                return (false, "No active session to save.");

            try
            {
                var player = Session.ActivePlayer;
                var world = _worldService.GetWorldMap();

                // Build quest progress snapshot
                var questProgress = new List<QuestProgressEntry>();
                foreach (var quest in
                    _questService.GetActiveQuestsForPlayer(player))
                {
                    foreach (var obj in quest.Objectives)
                    {
                        questProgress.Add(new QuestProgressEntry
                        {
                            QuestId      = quest.Id,
                            ObjectiveId  = obj.Id,
                            CurrentCount = obj.CurrentCount
                        });
                    }
                }

                var save = new SessionSaveData
                {
                    SaveSlot    = slot,
                    SessionName = Session.SessionName,
                    DisplayName =
                        $"{player.Name} — " +
                        $"Lv{player.Level} " +
                        $"{player.Race} {player.Class}",
                    StartedAt   = Session.StartedAt,
                    TotalPlayTime = Session.TotalPlayTime,

                    ActivePlayerId    = player.Id,
                    ActivePlayerName  = player.Name,
                    ActivePlayerLevel = player.Level,

                    ActivePartyId  = Session.ActiveParty?.Id,
                    PartyMemberIds = Session.ActiveParty?
                    .Members.Select(m => m.Character.Id).ToList()
                    ?? new(),

                    CurrentLocationId     = Session.CurrentLocation?.Id
                        ?? Guid.Empty,
                    DiscoveredLocationIds = world
                        .GlobalDiscoveredLocations.ToList(),

                    ActiveQuestIds = player.ActiveQuestIds.ToList(),
                    QuestProgress  = questProgress,

                    TotalCombatsEntered   = Session.TotalCombatsEntered,
                    TotalCombatsWon       = Session.TotalCombatsWon,
                    TotalCombatsLost      = Session.TotalCombatsLost,
                    TotalCombatsFled      = Session.TotalCombatsFled,
                    TotalRoomsExplored    = Session.TotalRoomsExplored,
                    TotalLocationsVisited = Session.TotalLocationsVisited,
                    TotalNpcsSpokenTo     = Session.TotalNpcsSpokenTo,
                    TotalItemsLooted      = Session.TotalItemsLooted,
                    TotalGoldEarned       = Session.TotalGoldEarned,
                    TotalXpEarned         = Session.TotalXpEarned,
                    TotalQuestsCompleted  = Session.TotalQuestsCompleted,
                    TotalDeaths           = Session.TotalDeaths,
                };

                await _database.SaveSessionAsync(save);
                Session.MarkSaved();
                NotifyStateChanged();

                return (true, slot == 0
                    ? "Game auto-saved."
                    : $"Game saved to slot {slot}.");
            }
            catch (Exception ex)
            {
                return (false, $"Save failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)>
            LoadGameAsync(int slot)
        {
            try
            {
                var save = await _database.LoadSessionAsync(slot);
                if (save == null)
                    return (false, "No save found in that slot.");

                // Load character from DB
                var player = await _characterService
                    .GetCharacterAsync(save.ActivePlayerId);
                if (player == null)
                    return (false, "Save references a deleted character.");

                // Rebuild session
                _session = new GameSession
                {
                    SessionName           = save.SessionName,
                    StartedAt             = save.StartedAt,
                    TotalPlayTime         = save.TotalPlayTime,
                    ActivePlayer          = player,
                    TotalCombatsEntered   = save.TotalCombatsEntered,
                    TotalCombatsWon       = save.TotalCombatsWon,
                    TotalCombatsLost      = save.TotalCombatsLost,
                    TotalCombatsFled      = save.TotalCombatsFled,
                    TotalRoomsExplored    = save.TotalRoomsExplored,
                    TotalLocationsVisited = save.TotalLocationsVisited,
                    TotalNpcsSpokenTo     = save.TotalNpcsSpokenTo,
                    TotalItemsLooted      = save.TotalItemsLooted,
                    TotalGoldEarned       = save.TotalGoldEarned,
                    TotalXpEarned         = save.TotalXpEarned,
                    TotalQuestsCompleted  = save.TotalQuestsCompleted,
                    TotalDeaths           = save.TotalDeaths,
                };

                // Restore world state
                var world = _worldService.GetWorldMap();
                foreach (var locId in save.DiscoveredLocationIds)
                    world.DiscoverLocation(locId);

                if (save.CurrentLocationId != Guid.Empty)
                {
                    world.SetPlayerLocation(
                        player.Id, save.CurrentLocationId);
                    var loc = world.GetLocation(
                        save.CurrentLocationId);
                    if (loc != null)
                        _session.SetLocation(loc);
                }
                else
                {
                    // Fallback — spawn at start
                    _worldService.SpawnPlayer(player.Id);
                    var loc = _worldService
                        .GetPlayerLocation(player.Id);
                    if (loc != null)
                        _session.SetLocation(loc);
                }

                // Restore quest progress
                foreach (var questId in save.ActiveQuestIds)
                {
                    var quest = _questService.GetQuest(questId);
                    if (quest == null) continue;

                    quest.Status = QuestStatus.Active;

                    foreach (var entry in save.QuestProgress
                        .Where(p => p.QuestId == questId))
                    {
                        var obj = quest.GetObjective(entry.ObjectiveId);
                        if (obj != null)
                            obj.CurrentCount = entry.CurrentCount;
                    }
                }

                Session.MarkSaved();
                Log($"📖 {player.Name}'s adventure continues.",
                    GameLogType.System);

                TransitionTo(GameState.Exploring);
                NotifyStateChanged();

                return (true, $"Loaded — welcome back, {player.Name}!");
            }
            catch (Exception ex)
            {
                return (false, $"Load failed: {ex.Message}");
            }
        }

        private async Task AutoSaveAsync()
        {
            if (!_settings.AutoSaveEnabled) return;
            await SaveGameAsync(slot: 0);
        }

        // =====================
        // Level Up
        // =====================

        private async Task HandleLevelUp()
        {
            if (!Session.HasActivePlayer) return;

            TransitionTo(GameState.CharacterLevel);

            var result = _characterService
                .LevelUpCharacter(
                    Session.ActivePlayer!.Id);

            if (result.Success)
            {
                Log(result.Message,
                    GameLogType.System);
                Notify(result.Message,
                    NotificationType.LevelUp);
                await PlaySound(SoundEffect.LevelUp);
            }

            TransitionTo(GameState.Exploring);
        }

        // =====================
        // World Events
        // =====================

        public void TriggerWorldEvent(
            WorldEvent worldEvent,
            int durationMinutes)
        {
            var message = _worldService
                .TriggerWorldEvent(
                    worldEvent, durationMinutes);

            Log(message, GameLogType.World);
            Notify(message, NotificationType.Warning);
            NotifyStateChanged();
        }

        // =====================
        // Daily Reset
        // =====================

        public void ProcessDailyReset()
        {
            var log = _questService
                .ProcessDailyReset();

            foreach (var entry in log)
                Log(entry, GameLogType.System);
        }

        // =====================
        // State Transitions
        // =====================

        public void TransitionTo(GameState state)
        {
            _session?.TransitionTo(state);
            OnStateChanged?.Invoke();
        }

        public void GoBack()
        {
            _session?.GoBack();
            OnStateChanged?.Invoke();
        }

        public void Pause()
        {
            if (CurrentState != GameState.Paused)
                TransitionTo(GameState.Paused);
        }

        public void Resume()
        {
            if (CurrentState == GameState.Paused)
                GoBack();
        }

        // =====================
        // Private Helpers
        // =====================

        private async Task<EncounterResult?>
            HandleEncounter(Encounter encounter)
        {
            if (!Session.HasActivePlayer) return null;

            var result = _encounterService
                .TriggerEncounter(
                    encounter,
                    Session.ActivePlayer!,
                    Session.ActiveParty);

            if (result.RequiresCombat &&
                encounter.Enemies.Any())
            {
                await StartCombatWithEnemies(
                    encounter.Enemies,
                    encounter.Name,
                    encounter.IsSurpriseRound);
            }

            if (result.SoundToPlay.HasValue)
                await PlaySound(result.SoundToPlay.Value);

            return result;
        }

        private void ProcessQuestResult(
            QuestUpdateResult result)
        {
            foreach (var entry in result.EventLog)
                Log(entry, GameLogType.Quest);

            foreach (var quest in
                result.CompletedQuests)
                Notify(
                    $"Quest ready: {quest.Title}!",
                    NotificationType.Quest);

            foreach (var quest in
                result.NewQuestsUnlocked)
                Notify(
                    $"New quest: {quest.Title}!",
                    NotificationType.Quest);
        }

        // =====================
        // Quest Wrappers
        // =====================

        public List<Quest> GetActiveQuests()
        {
            if (!HasSession || Session.ActivePlayer == null)
                return new();
            return _questService
                .GetActiveQuestsForPlayer(Session.ActivePlayer);
        }

        public List<Quest> GetAvailableQuests()
        {
            if (!HasSession || Session.ActivePlayer == null)
                return new();
            return _questService
                .GetAvailableQuestsForPlayer(Session.ActivePlayer);
        }

        public List<Quest> GetCompletedQuests()
        {
            if (!HasSession || Session.ActivePlayer == null)
                return new();
            return _questService
                .GetCompletedQuestsForPlayer(Session.ActivePlayer);
        }

        public (bool Success, string Message) AcceptQuest(
            Guid questId)
        {
            if (!HasSession || Session.ActivePlayer == null)
                return (false, "No active player.");

            var result = _questService.AcceptQuest(
                questId, Session.ActivePlayer);

            if (result.Success)
            {
                Log($"📜 {result.Message}",
                    GameLogType.Quest);
                Notify(result.Message,
                    NotificationType.Info);
                Session.MarkDirty();
                NotifyStateChanged();
            }
            return result;
        }

        public (bool Success, string Message) AbandonQuest(
            Guid questId)
        {
            if (!HasSession || Session.ActivePlayer == null)
                return (false, "No active player.");

            var result = _questService.AbandonQuest(
                questId, Session.ActivePlayer);

            if (result.Success)
            {
                Log($"🚫 {result.Message}",
                    GameLogType.Quest);
                Notify(result.Message,
                    NotificationType.Warning);
                Session.MarkDirty();
                NotifyStateChanged();
            }
            return result;
        }

        public bool HasReadyToCompleteQuests()
        {
            if (!HasSession || Session.ActivePlayer == null)
                return false;
            return _questService
                .GetReadyToCompleteQuests(Session.ActivePlayer)
                .Any();
        }



        private void Log(
            string message,
            GameLogType type = GameLogType.Info)
        {
            _session?.AddLog(message, type);
            OnLogMessage?.Invoke(message);
        }

        private void Notify(
            string message,
            NotificationType type =
                NotificationType.Info)
        {
            var notification = new GameNotification
            {
                Message = message,
                Type    = type
            };
            _session?.AddNotification(
                message, type);
            OnNotification?.Invoke(notification);
        }

        private async Task PlaySound(
            SoundEffect sound)
        {
            _session?.QueueSound(sound);
            OnPlaySound?.Invoke(sound);
            await _soundService.PlayAsync(sound);
        }

        private void NotifyStateChanged()
        {
            Session.MarkDirty();
            OnSessionUpdated?.Invoke();
            OnStateChanged?.Invoke();
        }
    }


}
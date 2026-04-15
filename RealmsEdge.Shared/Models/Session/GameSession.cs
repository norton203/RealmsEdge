using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Combat;
using PartyModels = RealmsEdge.Shared.Models.Party;
using RealmsEdge.Shared.Models.World;

namespace RealmsEdge.Shared.Models.Session
{
    public class GameSession
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string SessionName { get; set; }
            = string.Empty;
        public DateTime StartedAt { get; set; }
            = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; }
            = DateTime.UtcNow;
        public TimeSpan TotalPlayTime { get; set; }
            = TimeSpan.Zero;
        public bool IsActive { get; set; } = true;

        // =====================
        // Game State
        // =====================

        public GameState CurrentState { get; set; }
            = GameState.Initialising;
        public GameState PreviousState { get; set; }
            = GameState.Initialising;
        public Stack<GameState> StateHistory { get; set; }
            = new();

        public void TransitionTo(GameState newState)
        {
            PreviousState = CurrentState;
            StateHistory.Push(CurrentState);
            CurrentState  = newState;
            LastActivityAt = DateTime.UtcNow;

            // Keep history manageable
            while (StateHistory.Count > 20)
            {
                var temp = StateHistory.ToList();
                temp.RemoveAt(temp.Count - 1);
                StateHistory = new Stack<GameState>(
                    temp.AsEnumerable().Reverse());
            }
        }

        public bool CanGoBack =>
            StateHistory.Count > 0;

        public GameState? GoBack()
        {
            if (!CanGoBack) return null;
            CurrentState  = StateHistory.Pop();
            LastActivityAt = DateTime.UtcNow;
            return CurrentState;
        }

        // =====================
        // Player
        // =====================

        public PlayerCharacter? ActivePlayer
        { get; set; }

        public bool HasActivePlayer =>
            ActivePlayer != null;

        public string PlayerDisplayName =>
            ActivePlayer?.FullTitle ?? "No Player";

        // =====================
        // Party
        // =====================

        public PartyModels.Party? ActiveParty { get; set; }
        public bool IsInParty => ActiveParty != null;

        public List<PlayerCharacter> AllPlayers
        {
            get
            {
                if (ActiveParty != null)
                    return ActiveParty.Members
                        .Select(m => m.Character)
                        .ToList();

                if (ActivePlayer != null)
                    return new List<PlayerCharacter>
                        { ActivePlayer };

                return new List<PlayerCharacter>();
            }
        }

        // =====================
        // World
        // =====================

        public WorldLocation? CurrentLocation
        { get; set; }
        public Room? CurrentRoom { get; set; }
        public WorldLocation? PreviousLocation
        { get; set; }

        public bool IsInSafeZone =>
            CurrentLocation?.IsSafeZone ?? false;

        public bool IsInDungeon =>
            CurrentLocation?.Type ==
                LocationType.Dungeon ||
            CurrentLocation?.Type ==
                LocationType.Crypt   ||
            CurrentLocation?.Type ==
                LocationType.Cave    ||
            CurrentLocation?.Type ==
                LocationType.Tower;

        public void SetLocation(
            WorldLocation location,
            Room? room = null)
        {
            PreviousLocation = CurrentLocation;
            CurrentLocation  = location;
            CurrentRoom      = room;
            LastActivityAt   = DateTime.UtcNow;
        }

        // =====================
        // Combat
        // =====================

        public CombatState? ActiveCombat { get; set; }
        public bool IsInCombat =>
            ActiveCombat != null &&
            !ActiveCombat.AllEnemiesDefeated &&
            !ActiveCombat.AllPlayersDefeated;

        public void StartCombat(CombatState combat)
        {
            ActiveCombat = combat;
            TransitionTo(GameState.CombatStart);
        }

        public void EndCombat()
        {
            ActiveCombat = null;
            TransitionTo(IsInSafeZone
                ? GameState.Exploring
                : GameState.Exploring);
        }

        // =====================
        // Active NPC Interaction
        // =====================

        public NpcCharacter? ActiveNpc { get; set; }
        public bool IsInDialogue =>
            ActiveNpc != null &&
            CurrentState == GameState.Dialogue;

        public void StartDialogue(NpcCharacter npc)
        {
            ActiveNpc = npc;
            TransitionTo(GameState.Dialogue);
        }

        public void EndDialogue()
        {
            ActiveNpc = null;
            GoBack();
        }

        // =====================
        // Game Log
        // =====================

        public List<GameLogEntry> GameLog { get; set; }
            = new();
        public int MaxLogEntries { get; set; } = 200;

        public void AddLog(
            string message,
            GameLogType logType = GameLogType.Info)
        {
            GameLog.Add(new GameLogEntry
            {
                Message   = message,
                LogType   = logType,
                Timestamp = DateTime.UtcNow,
                State     = CurrentState
            });

            if (GameLog.Count > MaxLogEntries)
                GameLog.RemoveAt(0);
        }

        public List<GameLogEntry> GetRecentLog(
            int count = 20) =>
            GameLog.TakeLast(count).ToList();

        public List<GameLogEntry> GetLogByType(
            GameLogType type) =>
            GameLog.Where(l => l.LogType == type)
                .ToList();

        // =====================
        // Notifications
        // =====================

        public Queue<GameNotification> Notifications
        { get; set; } = new();

        public void AddNotification(
            string message,
            NotificationType type =
                NotificationType.Info)
        {
            Notifications.Enqueue(
                new GameNotification
                {
                    Message   = message,
                    Type      = type,
                    CreatedAt = DateTime.UtcNow
                });
        }

        public GameNotification? GetNextNotification()
            => Notifications.TryDequeue(out var n)
                ? n : null;

        // =====================
        // Sound Queue
        // =====================

        public Queue<SoundEffect> SoundQueue
        { get; set; } = new();

        public void QueueSound(SoundEffect sound)
            => SoundQueue.Enqueue(sound);

        public SoundEffect? GetNextSound()
            => SoundQueue.TryDequeue(out var s)
                ? s : null;

        // =====================
        // Session Statistics
        // =====================

        public int TotalCombatsEntered { get; set; }
        public int TotalCombatsWon { get; set; }
        public int TotalCombatsLost { get; set; }
        public int TotalCombatsFled { get; set; }
        public int TotalRoomsExplored { get; set; }
        public int TotalLocationsVisited { get; set; }
        public int TotalNpcsSpokenTo { get; set; }
        public int TotalItemsLooted { get; set; }
        public long TotalGoldEarned { get; set; }
        public long TotalXpEarned { get; set; }
        public int TotalQuestsCompleted { get; set; }
        public int TotalDeaths { get; set; }

        public double WinRate => TotalCombatsEntered > 0
            ? Math.Round(
                (double)TotalCombatsWon /
                TotalCombatsEntered * 100, 1)
            : 0;

        public string SessionSummary =>
            $"Session: {SessionName} | " +
            $"Playtime: {FormatPlayTime()} | " +
            $"Combats: {TotalCombatsWon}W/" +
            $"{TotalCombatsLost}L/" +
            $"{TotalCombatsFled}F | " +
            $"Quests: {TotalQuestsCompleted} | " +
            $"XP: {TotalXpEarned:N0}";

        private string FormatPlayTime()
        {
            var time = TotalPlayTime;
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}h " +
                  $"{time.Minutes}m"
                : $"{time.Minutes}m {time.Seconds}s";
        }

        // =====================
        // Save Data
        // =====================

        public DateTime? LastSavedAt { get; set; }
        public int SaveSlot { get; set; } = 1;
        public bool HasUnsavedChanges { get; set; }
            = false;

        public void MarkSaved()
        {
            LastSavedAt      = DateTime.UtcNow;
            HasUnsavedChanges = false;
        }

        public void MarkDirty()
            => HasUnsavedChanges = true;

        public string SaveStatusDisplay =>
            LastSavedAt.HasValue
                ? $"Last saved: " +
                  $"{LastSavedAt.Value:HH:mm:ss}" +
                  $"{(HasUnsavedChanges ? " *" : "")}"
                : "Not yet saved";

        // =====================
        // Display
        // =====================

        public string StateDisplay => CurrentState switch
        {
            GameState.Initialising => "⏳ Loading...",
            GameState.MainMenu => "🏰 Main Menu",
            GameState.CharacterCreate => "✏️ Create Character",
            GameState.CharacterSelect => "👤 Select Character",
            GameState.WorldMap => "🗺️ World Map",
            GameState.Exploring => "🚶 Exploring",
            GameState.Travelling => "🛤️ Travelling",
            GameState.Dialogue => "💬 In Dialogue",
            GameState.Shopping => "🛒 Shopping",
            GameState.CombatStart => "⚔️ Combat!",
            GameState.CombatPlayerTurn => "👤 Your Turn",
            GameState.CombatEnemyTurn => "👹 Enemy Turn",
            GameState.CombatVictory => "🏆 Victory!",
            GameState.CombatDefeat => "💀 Defeated",
            GameState.QuestJournal => "📖 Quest Journal",
            GameState.QuestComplete => "🏆 Quest Complete",
            GameState.Resting => "🏕️ Resting",
            GameState.Paused => "⏸️ Paused",
            GameState.GameOver => "💀 Game Over",
            _ => "🎮 Playing"
        };

        public override string ToString()
            => SessionSummary;
    }

    // =====================
    // Supporting Classes
    // =====================

    public enum GameLogType
    {
        Info,               // General information
        Combat,             // Combat events
        Quest,              // Quest updates
        Loot,               // Item found
        Dialogue,           // NPC conversation
        World,              // World events
        System,             // Game system messages
        Error               // Something went wrong
    }

    public enum NotificationType
    {
        Info,               // Blue — general info
        Success,            // Green — positive event
        Warning,            // Yellow — caution
        Danger,             // Red — threat or damage
        Quest,              // Purple — quest update
        Loot,               // Gold — item found
        LevelUp             // Bright — level gained
    }

    public class GameLogEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Message { get; set; }
            = string.Empty;
        public GameLogType LogType { get; set; }
        public DateTime Timestamp { get; set; }
            = DateTime.UtcNow;
        public GameState State { get; set; }

        public string TypeIcon => LogType switch
        {
            GameLogType.Combat => "⚔️",
            GameLogType.Quest => "📜",
            GameLogType.Loot => "💎",
            GameLogType.Dialogue => "💬",
            GameLogType.World => "🌍",
            GameLogType.System => "⚙️",
            GameLogType.Error => "❌",
            _ => "📋"
        };

        public string Display =>
            $"[{Timestamp:HH:mm:ss}] " +
            $"{TypeIcon} {Message}";
    }

    public class GameNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Message { get; set; }
            = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public int DisplayDurationMs { get; set; }
            = 3000;                             // 3 seconds

        public string TypeColour => Type switch
        {
            NotificationType.Success => "#00aa00",
            NotificationType.Warning => "#ffaa00",
            NotificationType.Danger => "#ff3232",
            NotificationType.Quest => "#a335ee",
            NotificationType.Loot => "#f0c040",
            NotificationType.LevelUp => "#00ffff",
            _ => "#aaaaaa"
        };

        public string TypeIcon => Type switch
        {
            NotificationType.Success => "✅",
            NotificationType.Warning => "⚠️",
            NotificationType.Danger => "💀",
            NotificationType.Quest => "📜",
            NotificationType.Loot => "💎",
            NotificationType.LevelUp => "⭐",
            _ => "ℹ️"
        };
    }
}
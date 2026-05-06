namespace RealmsEdge.Shared.Models.Session
{
    /// <summary>
    /// Slim snapshot of a game session for persistence.
    /// Characters live in their own DB table — we only
    /// store IDs here. Quest definitions are seeded at
    /// startup — we only store progress deltas.
    /// </summary>
    public class SessionSaveData
    {
        // =====================
        // Slot Identity
        // =====================

        public int SaveSlot { get; set; } = 1;
        public string SessionName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // "Thorin — Lv5 Dwarf Fighter"
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        // =====================
        // Session Timing
        // =====================

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public long PlayTimeTicks { get; set; } = 0;

        public TimeSpan TotalPlayTime
        {
            get => TimeSpan.FromTicks(PlayTimeTicks);
            set => PlayTimeTicks = value.Ticks;
        }

        // =====================
        // Active Player
        // =====================

        public Guid ActivePlayerId { get; set; }
        public string ActivePlayerName { get; set; } = string.Empty;
        public int ActivePlayerLevel { get; set; } = 1;

        // =====================
        // Active Party
        // =====================

        public Guid? ActivePartyId { get; set; }
        public List<Guid> PartyMemberIds { get; set; } = new();

        // =====================
        // World State
        // =====================

        public Guid CurrentLocationId { get; set; }
        public List<Guid> DiscoveredLocationIds { get; set; } = new();

        // =====================
        // Quest Progress
        // =====================

        // Active quest IDs the player currently holds
        public List<Guid> ActiveQuestIds { get; set; } = new();

        // Objective progress: questId → list of (objectiveId, currentCount)
        public List<QuestProgressEntry> QuestProgress { get; set; } = new();

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

        // =====================
        // Display Helpers
        // =====================

        public string PlayTimeDisplay
        {
            get
            {
                var t = TotalPlayTime;
                return t.TotalHours >= 1
                    ? $"{(int)t.TotalHours}h {t.Minutes}m"
                    : $"{t.Minutes}m {t.Seconds}s";
            }
        }

        public string SavedAtDisplay =>
            SavedAt.ToLocalTime().ToString("dd MMM yyyy  HH:mm");

        public bool IsAutoSave => SaveSlot == 0;
    }

    /// <summary>
    /// Stores how far along one quest objective is.
    /// Quest definitions are re-seeded at startup;
    /// we only need to restore CurrentCount.
    /// </summary>
    public class QuestProgressEntry
    {
        public Guid QuestId { get; set; }
        public Guid ObjectiveId { get; set; }
        public int CurrentCount { get; set; }
    }
}
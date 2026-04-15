using RealmsEdge.Shared.Enums;

namespace RealmsEdge.Shared.Models.Combat
{
    public enum RoundPhase
    {
        NotStarted,         // Round has not begun
        StatusEffects,      // Processing start of turn effects
        Initiative,         // Determining turn order
        PlayerTurn,         // Waiting for player input
        EnemyTurn,          // AI processing enemy actions
        EndOfRound,         // Cleanup, end of round effects
        Complete            // Round fully resolved
    }

    public class RoundSummary
    {
        public int RoundNumber { get; set; }
        public List<string> EventLog { get; set; } = new();
        public List<CombatAction> Actions { get; set; } = new();
        public int TotalDamageDealt { get; set; }
        public int TotalHealingDone { get; set; }
        public List<string> CasualtiesThisRound { get; set; } = new();
        public List<string> StatusesAppliedThisRound { get; set; } = new();
        public bool CombatEndedThisRound { get; set; }

        public string RoundDisplay =>
            $"⚔️ Round {RoundNumber} | " +
            $"Damage: {TotalDamageDealt} | " +
            $"Healing: {TotalHealingDone}" +
            (CasualtiesThisRound.Any()
                ? $" | Fallen: {string.Join(", ", CasualtiesThisRound)}"
                : string.Empty);
    }

    public class CombatRound
    {
        // =====================
        // Identity
        // =====================

        public int RoundNumber { get; set; }
        public RoundPhase Phase { get; set; }
            = RoundPhase.NotStarted;
        public DateTime StartedAt { get; set; }
            = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // =====================
        // Turn Order
        // =====================

        public List<int> InitiativeOrder { get; set; } = new();
        public int CurrentTurnIndex { get; set; } = 0;
        public int ActiveCombatantId =>
            InitiativeOrder.ElementAtOrDefault(CurrentTurnIndex);

        public bool IsLastTurn =>
            CurrentTurnIndex >= InitiativeOrder.Count - 1;

        public void AdvanceTurn()
        {
            if (!IsLastTurn)
                CurrentTurnIndex++;
        }

        // =====================
        // Actions This Round
        // =====================

        public List<CombatAction> Actions { get; set; } = new();

        public void RecordAction(CombatAction action)
            => Actions.Add(action);

        public List<CombatAction> GetActionsBy(int combatantId)
            => Actions
                .Where(a => a.ActorCombatId == combatantId)
                .ToList();

        public List<CombatAction> GetActionsAgainst(int combatantId)
            => Actions
                .Where(a => a.TargetCombatId == combatantId)
                .ToList();

        // =====================
        // Status Effect Log
        // =====================

        public List<string> StatusEffectLog { get; set; } = new();

        public void AddStatusLog(string entry)
            => StatusEffectLog.Add(entry);

        // =====================
        // Round Statistics
        // =====================

        public int TotalDamageDealt => Actions
            .Sum(a => a.DamageTotal);

        public int TotalHealingDone => Actions
            .Sum(a => a.HealingDone);

        public int TotalManaSpent => Actions
            .Sum(a => a.ManaSpent);

        public int TotalStaminaSpent => Actions
            .Sum(a => a.StaminaSpent);

        public int CriticalHitsThisRound => Actions
            .Count(a => a.WasCritical);

        public int CriticalMissesThisRound => Actions
            .Count(a => a.WasCriticalFail);

        public int HitsThisRound => Actions
            .Count(a => a.Result == ActionResult.Hit ||
                        a.Result == ActionResult.CriticalHit);

        public int MissesThisRound => Actions
            .Count(a => a.Result == ActionResult.Miss ||
                        a.Result == ActionResult.CriticalMiss);

        public List<string> KillingBlowsThisRound => Actions
            .Where(a => a.IsKillingBlow)
            .Select(a =>
                $"{a.ActorName} slew {a.TargetName}!")
            .ToList();

        // =====================
        // Phase Management
        // =====================

        public void StartPhase(RoundPhase phase)
        {
            Phase = phase;
        }

        public void Complete()
        {
            Phase       = RoundPhase.Complete;
            CompletedAt = DateTime.UtcNow;
        }

        public TimeSpan Duration => CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;

        // =====================
        // Build Summary
        // =====================

        public RoundSummary BuildSummary()
        {
            var summary = new RoundSummary
            {
                RoundNumber          = RoundNumber,
                Actions              = Actions,
                TotalDamageDealt     = TotalDamageDealt,
                TotalHealingDone     = TotalHealingDone,
                CasualtiesThisRound  = KillingBlowsThisRound,
                CombatEndedThisRound = Phase == RoundPhase.Complete
            };

            // Build event log
            var log = new List<string>();

            log.Add($"━━━ Round {RoundNumber} ━━━");

            // Status effect events
            if (StatusEffectLog.Any())
            {
                log.Add("🔄 Status Effects:");
                log.AddRange(StatusEffectLog);
            }

            // All actions in order
            log.Add("⚔️ Actions:");
            foreach (var action in Actions)
                log.Add($"  {action.ToCombatLogEntry()}");

            // Casualties
            if (KillingBlowsThisRound.Any())
            {
                log.Add("☠️ Fallen this round:");
                foreach (var kill in KillingBlowsThisRound)
                    log.Add($"  {kill}");
            }

            // Round stats
            log.Add(
                $"📊 Round Stats: " +
                $"{HitsThisRound} hits | " +
                $"{MissesThisRound} misses | " +
                $"{CriticalHitsThisRound} crits | " +
                $"{TotalDamageDealt} total damage | " +
                $"{TotalHealingDone} healing");

            summary.EventLog = log;
            return summary;
        }

        // =====================
        // Display
        // =====================

        public string PhaseDisplay => Phase switch
        {
            RoundPhase.NotStarted => "⏳ Not Started",
            RoundPhase.StatusEffects => "🔄 Status Effects",
            RoundPhase.Initiative => "🎲 Initiative",
            RoundPhase.PlayerTurn => "👤 Player Turn",
            RoundPhase.EnemyTurn => "👹 Enemy Turn",
            RoundPhase.EndOfRound => "🔔 End of Round",
            RoundPhase.Complete => "✅ Complete",
            _ => "Unknown"
        };

        public override string ToString() =>
            $"Round {RoundNumber} | " +
            $"{PhaseDisplay} | " +
            $"{Actions.Count} actions | " +
            $"{TotalDamageDealt} damage dealt";
    }
}
using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Services;

namespace RealmsEdge.Shared.Models.Combat
{
    public enum CombatPhase
    {
        NotStarted,         // Combat not yet begun
        Initiative,         // Rolling initiative
        Active,             // Combat in progress
        PlayerDecision,     // Waiting for player input
        ResolvingAction,    // Processing an action
        EndOfRound,         // Between rounds cleanup
        Victory,            // Players won
        Defeat,             // Players lost
        Fled,               // Party fled successfully
        Negotiated          // Resolved without full combat
    }

    public class CombatResult
    {
        public bool IsOver { get; set; }
        public CombatPhase Outcome { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> EventLog { get; set; } = new();
        public long TotalXpAwarded { get; set; }
        public int TotalGoldAwarded { get; set; }
        public int TotalRounds { get; set; }
        public List<string> Casualties { get; set; } = new();
        public SoundEffect? SoundToPlay { get; set; }

        public string OutcomeDisplay => Outcome switch
        {
            CombatPhase.Victory => "⚔️ Victory!",
            CombatPhase.Defeat => "💀 Defeated!",
            CombatPhase.Fled => "🏃 Escaped!",
            CombatPhase.Negotiated => "🤝 Negotiated!",
            _ => "Combat Ended"
        };
    }

    public class CombatState
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string BattleName { get; set; }
            = "Encounter";
        public CombatPhase Phase { get; set; }
            = CombatPhase.NotStarted;
        public DateTime StartedAt { get; set; }
            = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public int CurrentRound { get; set; } = 0;
        public int MaxRounds { get; set; } = 50;        // Safety limit

        // =====================
        // Combatants
        // =====================

        public List<Combatant> Combatants { get; set; } = new();
        private int _nextCombatId = 1;

        public Combatant AddCombatant(
            CharacterBase character,
            CombatTeam team,
            bool isPlayerControlled = false)
        {
            var combatant = new Combatant
            {
                CombatId          = _nextCombatId++,
                Character         = character,
                Team              = team,
                IsPlayerControlled = isPlayerControlled
            };
            Combatants.Add(combatant);
            return combatant;
        }

        public Combatant? GetCombatant(int combatId)
            => Combatants.FirstOrDefault(
                c => c.CombatId == combatId);

        public Combatant? GetCombatantByCharacterId(
            Guid characterId) => Combatants
                .FirstOrDefault(c =>
                    c.Character.Id == characterId);

        // =====================
        // Team Queries
        // =====================

        public List<Combatant> PlayerCombatants => Combatants
            .Where(c => c.Team == CombatTeam.Players)
            .ToList();

        public List<Combatant> EnemyCombatants => Combatants
            .Where(c => c.Team == CombatTeam.Enemies)
            .ToList();

        public List<Combatant> ActivePlayers => Combatants
            .Where(c => c.Team == CombatTeam.Players &&
                        c.CanAct && c.IsAlive)
            .ToList();

        public List<Combatant> ActiveEnemies => Combatants
            .Where(c => c.Team == CombatTeam.Enemies &&
                        c.CanAct && c.IsAlive)
            .ToList();

        public List<Combatant> AliveCombatants => Combatants
            .Where(c => c.IsAlive &&
                        c.State != CombatantState.Fled)
            .ToList();

        public bool AllPlayersDefeated => PlayerCombatants
            .All(c => !c.IsAlive ||
                      c.State == CombatantState.Fled);

        public bool AllEnemiesDefeated => EnemyCombatants
            .All(c => !c.IsAlive ||
                      c.State == CombatantState.Fled);

        // =====================
        // Initiative Order
        // =====================

        public List<Combatant> InitiativeOrder { get; set; }
            = new();

        public Combatant? CurrentCombatant =>
            InitiativeOrder.ElementAtOrDefault(
                _currentInitiativeIndex);

        private int _currentInitiativeIndex = 0;

        public void SetInitiativeOrder(
            List<Combatant> order)
        {
            InitiativeOrder        = order;
            _currentInitiativeIndex = 0;
        }

        public Combatant? AdvanceInitiative()
        {
            // Find next combatant that can act
            var startIndex = _currentInitiativeIndex;

            do
            {
                _currentInitiativeIndex++;

                // End of round
                if (_currentInitiativeIndex >=
                    InitiativeOrder.Count)
                    return null;

                var next = InitiativeOrder
                    [_currentInitiativeIndex];

                if (next.CanAct && next.IsAlive)
                    return next;

            } while (_currentInitiativeIndex != startIndex);

            return null;
        }

        public void ResetInitiative()
            => _currentInitiativeIndex = 0;

        // =====================
        // Round Management
        // =====================

        public List<CombatRound> Rounds { get; set; } = new();

        public CombatRound? CurrentRoundData => Rounds
            .FirstOrDefault(r =>
                r.RoundNumber == CurrentRound);

        public CombatRound StartNewRound()
        {
            CurrentRound++;
            var round = new CombatRound
            {
                RoundNumber = CurrentRound,
                Phase       = RoundPhase.NotStarted
            };
            Rounds.Add(round);
            ResetInitiative();

            // Start all combatant turns
            foreach (var combatant in AliveCombatants)
                combatant.StartTurn();

            return round;
        }

        // =====================
        // Combat Log
        // =====================

        public List<string> CombatLog { get; set; } = new();
        public int MaxLogEntries { get; set; } = 500;

        public void Log(string entry)
        {
            CombatLog.Add(
                $"[R{CurrentRound}] {entry}");

            if (CombatLog.Count > MaxLogEntries)
                CombatLog.RemoveAt(0);
        }

        public void LogRange(IEnumerable<string> entries)
        {
            foreach (var entry in entries)
                Log(entry);
        }

        public List<string> GetRecentLog(int lines = 20)
            => CombatLog.TakeLast(lines).ToList();

        // =====================
        // Combat Statistics
        // =====================

        public int TotalDamageDealt => Rounds
            .Sum(r => r.TotalDamageDealt);

        public int TotalHealingDone => Rounds
            .Sum(r => r.TotalHealingDone);

        public int TotalCriticalHits => Rounds
            .Sum(r => r.CriticalHitsThisRound);

        public int TotalCriticalMisses => Rounds
            .Sum(r => r.CriticalMissesThisRound);

        public Combatant? MostDamageDealt => Combatants
            .OrderByDescending(c => Rounds
                .SelectMany(r => r.Actions)
                .Where(a => a.ActorCombatId == c.CombatId)
                .Sum(a => a.DamageTotal))
            .FirstOrDefault();

        public Combatant? MostHealingDone => Combatants
            .OrderByDescending(c => Rounds
                .SelectMany(r => r.Actions)
                .Where(a => a.ActorCombatId == c.CombatId)
                .Sum(a => a.HealingDone))
            .FirstOrDefault();

        public TimeSpan Duration => EndedAt.HasValue
            ? EndedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;

        // =====================
        // Target Selection
        // =====================

        public List<Combatant> GetValidTargets(
            Combatant actor)
        {
            // Players target enemies and vice versa
            return actor.Team == CombatTeam.Players
                ? ActiveEnemies
                : ActivePlayers;
        }

        public Combatant? GetLowestHpTarget(
            CombatTeam targetTeam)
        {
            return Combatants
                .Where(c => c.Team == targetTeam &&
                            c.IsAlive && c.CanAct)
                .OrderBy(c =>
                    c.Character.Stats.HealthPercent)
                .FirstOrDefault();
        }

        public Combatant? GetHighestThreatTarget(
            CombatTeam targetTeam)
        {
            // Threat = highest damage dealer so far
            return Combatants
                .Where(c => c.Team == targetTeam &&
                            c.IsAlive && c.CanAct)
                .OrderByDescending(c => Rounds
                    .SelectMany(r => r.Actions)
                    .Where(a => a.ActorCombatId ==
                                c.CombatId)
                    .Sum(a => a.DamageTotal))
                .FirstOrDefault();
        }

        public Combatant? GetRandomTarget(
            CombatTeam targetTeam,
            DiceService diceService)
        {
            var targets = Combatants
                .Where(c => c.Team == targetTeam &&
                            c.IsAlive && c.CanAct)
                .ToList();

            if (!targets.Any()) return null;

            var roll = diceService.Roll(
                DiceType.D20).Total % targets.Count;
            return targets[roll];
        }

        // =====================
        // Morale System
        // (L.O.R.D inspired)
        // =====================

        public bool EnemyMoralebroken { get; set; } = false;

        public bool CheckEnemyMorale(DiceService diceService)
        {
            if (EnemyMoralebroken) return true;

            var livingEnemies = ActiveEnemies.Count;
            var totalEnemies = EnemyCombatants.Count;

            if (totalEnemies == 0) return false;

            var percentAlive =
                (double)livingEnemies / totalEnemies * 100;

            // Morale check when half enemies are down
            if (percentAlive <= 50)
            {
                var moraleRoll = diceService
                    .Roll(DiceType.D20);
                if (moraleRoll.Total <= 8)
                {
                    EnemyMoralebroken = true;
                    Log(
                        "😨 Enemy morale breaks! " +
                        "Remaining enemies attempt to flee!");
                    return true;
                }
            }

            return false;
        }

        // =====================
        // Combat End
        // =====================

        public CombatResult BuildVictoryResult()
        {
            EndedAt = DateTime.UtcNow;
            Phase   = CombatPhase.Victory;

            var xp = EnemyCombatants
                .Sum(e => (e.Character as NpcCharacter)
                    ?.ExperienceReward ?? 0);
            var gold = EnemyCombatants
                .Sum(e => (e.Character as NpcCharacter)
                    ?.GoldReward ?? 0);

            var casualties = Combatants
                .Where(c => !c.IsAlive)
                .Select(c => c.Character.Name)
                .ToList();

            return new CombatResult
            {
                IsOver          = true,
                Outcome         = CombatPhase.Victory,
                Message         =
                    $"Victory after {CurrentRound} rounds!",
                TotalXpAwarded  = xp,
                TotalGoldAwarded = gold,
                TotalRounds     = CurrentRound,
                Casualties      = casualties,
                EventLog        = GetRecentLog(50),
                SoundToPlay     = SoundEffect.LevelUp
            };
        }

        public CombatResult BuildDefeatResult()
        {
            EndedAt = DateTime.UtcNow;
            Phase   = CombatPhase.Defeat;

            return new CombatResult
            {
                IsOver      = true,
                Outcome     = CombatPhase.Defeat,
                Message     =
                    "Your party has been defeated...",
                TotalRounds = CurrentRound,
                Casualties  = PlayerCombatants
                    .Where(c => !c.IsAlive)
                    .Select(c => c.Character.Name)
                    .ToList(),
                EventLog    = GetRecentLog(50),
                SoundToPlay = SoundEffect.CombatDeath
            };
        }

        public CombatResult BuildFledResult()
        {
            EndedAt = DateTime.UtcNow;
            Phase   = CombatPhase.Fled;

            return new CombatResult
            {
                IsOver      = true,
                Outcome     = CombatPhase.Fled,
                Message     = "Your party escapes!",
                TotalRounds = CurrentRound,
                EventLog    = GetRecentLog(20),
                SoundToPlay = SoundEffect.CombatMiss
            };
        }

        // =====================
        // Display
        // =====================

        public string BattlefieldDisplay
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(
                    $"⚔️ {BattleName} — " +
                    $"Round {CurrentRound} | " +
                    $"{Phase}");
                sb.AppendLine("── Players ──");

                foreach (var p in PlayerCombatants)
                    sb.AppendLine(
                        $"  {(p.CombatId == CurrentCombatant?.CombatId ? "▶ " : "  ")}" +
                        $"{p.CombatSummary}");

                sb.AppendLine("── Enemies ──");

                foreach (var e in EnemyCombatants)
                    sb.AppendLine(
                        $"  {(e.CombatId == CurrentCombatant?.CombatId ? "▶ " : "  ")}" +
                        $"{e.CombatSummary}");

                return sb.ToString().Trim();
            }
        }

        public string PhaseDisplay => Phase switch
        {
            CombatPhase.NotStarted => "⏳ Not Started",
            CombatPhase.Initiative => "🎲 Rolling Initiative",
            CombatPhase.Active => "⚔️ Active Combat",
            CombatPhase.PlayerDecision => "👤 Your Turn",
            CombatPhase.ResolvingAction => "⚡ Resolving...",
            CombatPhase.EndOfRound => "🔔 End of Round",
            CombatPhase.Victory => "🏆 Victory!",
            CombatPhase.Defeat => "💀 Defeated!",
            CombatPhase.Fled => "🏃 Escaped!",
            CombatPhase.Negotiated => "🤝 Negotiated!",
            _ => "Unknown"
        };

        public override string ToString() =>
            $"{BattleName} | " +
            $"Round {CurrentRound} | " +
            $"{PhaseDisplay} | " +
            $"{ActivePlayers.Count} players vs " +
            $"{ActiveEnemies.Count} enemies";
    }
}
using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Shared.Models.Combat
{
    public enum CombatTeam
    {
        Players,            // Player characters and companions
        Enemies,            // Hostile NPCs
        Neutral             // Can turn either way
    }

    public enum CombatantState
    {
        Active,             // Can act normally
        Defending,          // Chose defend action, bonus AC
        Waiting,            // Chose wait, acts later
        Stunned,            // Cannot act this round
        Fled,               // Has left the battle
        Unconscious,        // Down but not dead
        Dead                // Removed from combat
    }

    public class StatusEffect
    {
        public CharacterStatus Status { get; set; }
        public int DurationRounds { get; set; }         // Rounds remaining
        public int DamagePerRound { get; set; } = 0;    // Damage each turn
        public DamageType DamageType { get; set; }
            = DamageType.True;
        public int SourceCombatantId { get; set; }      // Who applied it
        public bool IsExpired => DurationRounds <= 0;

        public void Tick()
        {
            if (DurationRounds > 0)
                DurationRounds--;
        }

        public string Display =>
            $"{Status} ({DurationRounds} rounds remaining" +
            $"{(DamagePerRound > 0 ? $", {DamagePerRound} dmg/round" : "")})";
    }

    public class Combatant
    {
        // =====================
        // Identity
        // =====================

        public int CombatId { get; set; }               // Combat slot number
        public CharacterBase Character { get; set; } = null!;
        public CombatTeam Team { get; set; }
        public CombatantState State { get; set; }
            = CombatantState.Active;
        public bool IsPlayerControlled { get; set; }

        // =====================
        // Initiative
        // =====================

        public int InitiativeRoll { get; set; }         // D20 roll
        public int InitiativeTotal { get; set; }        // Roll + modifier
        public bool HasActedThisRound { get; set; }
        public bool HasBonusActionThisRound { get; set; }
        public bool HasReactionThisRound { get; set; }

        // =====================
        // Combat Stats
        // (temporary, combat only)
        // =====================

        public int TemporaryAcBonus { get; set; } = 0;  // From Defend action
        public int TemporaryAttackBonus { get; set; } = 0;
        public int TemporaryDamageBonus { get; set; } = 0;
        public bool HasAdvantage { get; set; } = false;
        public bool HasDisadvantage { get; set; } = false;
        public bool IsRaging { get; set; } = false;     // Barbarian rage
        public bool IsHiding { get; set; } = false;     // Stealth mid combat
        public bool IsGuarding { get; set; } = false;   // Protecting ally
        public Guid? GuardingCharacterId { get; set; }  // Who they protect

        // =====================
        // Active Status Effects
        // =====================

        public List<StatusEffect> ActiveEffects { get; set; } = new();

        public bool HasEffect(CharacterStatus status)
            => ActiveEffects.Any(e => e.Status == status
                && !e.IsExpired);

        public void ApplyEffect(StatusEffect effect)
        {
            // Remove existing instance of same status
            ActiveEffects.RemoveAll(e =>
                e.Status == effect.Status);
            ActiveEffects.Add(effect);
            Character.ApplyStatus(effect.Status);
        }

        public void RemoveEffect(CharacterStatus status)
        {
            ActiveEffects.RemoveAll(e =>
                e.Status == status);
            Character.RemoveStatus(status);
        }

        // Process all status effects at turn start
        public List<string> ProcessStatusEffects()
        {
            var log = new List<string>();

            foreach (var effect in ActiveEffects.ToList())
            {
                // Apply per round damage
                if (effect.DamagePerRound > 0)
                {
                    Character.Stats.ApplyDamage(
                        effect.DamagePerRound);
                    log.Add(
                        $"🔥 {Character.Name} takes " +
                        $"{effect.DamagePerRound} " +
                        $"{effect.DamageType} damage " +
                        $"from {effect.Status}!");
                }

                // Tick duration down
                effect.Tick();

                // Remove expired effects
                if (effect.IsExpired)
                {
                    RemoveEffect(effect.Status);
                    log.Add(
                        $"✨ {effect.Status} fades from " +
                        $"{Character.Name}.");
                }
            }

            return log;
        }

        // =====================
        // Combat Calculations
        // =====================

        public int EffectiveAC =>
            Character.Stats.ArmourClass +
            TemporaryAcBonus +
            (State == CombatantState.Defending ? 4 : 0) +
            (IsHiding ? 5 : 0);

        public int EffectiveAttackBonus =>
            Character.Stats.MeleeAttackBonus +
            TemporaryAttackBonus +
            (IsRaging ? 2 : 0);

        public int EffectiveDamageBonus =>
            Character.Stats.StrengthModifier +
            TemporaryDamageBonus +
            (IsRaging ? 3 : 0);

        public int EffectiveRangedBonus =>
            Character.Stats.RangedAttackBonus +
            TemporaryAttackBonus;

        public int EffectiveSpellBonus =>
            Character.Stats.SpellAttackBonus +
            TemporaryAttackBonus;

        // =====================
        // State Checks
        // =====================

        public bool CanAct =>
            State == CombatantState.Active    ||
            State == CombatantState.Defending ||
            State == CombatantState.Waiting;

        public bool CanBeTargeted =>
            State != CombatantState.Fled &&
            State != CombatantState.Dead;

        public bool IsAlive =>
            Character.Stats.IsAlive;

        public bool ShouldFlee =>
            Character is NpcCharacter npc &&
            npc.Behaviour == NpcBehaviour.Cowardly &&
            Character.Stats.HealthPercent <= npc.FleeHealthPercent;

        // =====================
        // Turn Management
        // =====================

        public void StartTurn()
        {
            HasActedThisRound       = false;
            HasBonusActionThisRound = false;
            HasReactionThisRound    = false;
            TemporaryAcBonus        = 0;
            TemporaryAttackBonus    = 0;
            TemporaryDamageBonus    = 0;

            // Reset defend state
            if (State == CombatantState.Defending)
                State = CombatantState.Active;

            // Update state based on HP
            if (!Character.Stats.IsAlive)
                State = Character.Stats.IsDead
                    ? CombatantState.Dead
                    : CombatantState.Unconscious;
        }

        public void EndTurn()
        {
            HasActedThisRound = true;

            // Snap out of hiding if acted
            if (IsHiding && HasActedThisRound)
                IsHiding = false;
        }

        // =====================
        // Rage Management
        // =====================

        public bool ActivateRage()
        {
            if (Character.Class != CharacterClass.Barbarian)
                return false;
            if (!Character.Stats.HasStamina) return false;

            IsRaging = true;
            Character.Stats.SpendStamina(2);
            ApplyEffect(new StatusEffect
            {
                Status         = CharacterStatus.Hasted,
                DurationRounds = 5,
                DamagePerRound = 0
            });
            return true;
        }

        public void EndRage()
        {
            IsRaging = false;
            RemoveEffect(CharacterStatus.Hasted);
        }

        // =====================
        // Damage Application
        // =====================

        public int ApplyDamage(
            int rawDamage,
            DamageType damageType,
            bool isCritical = false)
        {
            var damage = rawDamage;

            // Critical hit doubles damage
            if (isCritical) damage *= 2;

            // Apply damage type resistances
            damage = ApplyResistances(damage, damageType);

            // Apply to HP
            Character.Stats.ApplyDamage(damage);

            // Update state
            if (!Character.Stats.IsAlive)
                State = Character.Stats.IsDead
                    ? CombatantState.Dead
                    : CombatantState.Unconscious;

            return damage;
        }

        private int ApplyResistances(
            int damage,
            DamageType damageType)
        {
            // Undead heal from shadow damage
            if (Character.Race == CharacterRace.Undead &&
                damageType == DamageType.Shadow)
            {
                Character.Stats.ApplyHealing(damage);
                return 0;
            }

            // Holy damage extra effective vs undead and demons
            if (damageType == DamageType.Holy &&
                (Character.Race == CharacterRace.Undead ||
                 Character.Race == CharacterRace.Demon))
                return (int)(damage * 1.5);

            // Bludgeoning extra vs undead
            if (damageType == DamageType.Bludgeoning &&
                Character.Race == CharacterRace.Undead)
                return (int)(damage * 1.25);

            // Poison resistance
            if (damageType == DamageType.Poison)
            {
                var reduction = Character.Stats.ResistPoison / 100.0;
                return (int)(damage * (1 - reduction));
            }

            // Magic resistance
            if (damageType == DamageType.Arcane ||
                damageType == DamageType.Fire    ||
                damageType == DamageType.Ice     ||
                damageType == DamageType.Holy    ||
                damageType == DamageType.Shadow)
            {
                var reduction = Character.Stats.ResistMagic / 100.0;
                return (int)(damage * (1 - reduction));
            }

            return damage;
        }

        // =====================
        // Display
        // =====================

        public string StateDisplay => State switch
        {
            CombatantState.Active => "⚔️ Active",
            CombatantState.Defending => "🛡️ Defending",
            CombatantState.Waiting => "⏳ Waiting",
            CombatantState.Stunned => "😵 Stunned",
            CombatantState.Fled => "🏃 Fled",
            CombatantState.Unconscious => "💫 Unconscious",
            CombatantState.Dead => "💀 Dead",
            _ => "Unknown"
        };

        public string HealthBar
        {
            get
            {
                var percent = Character.Stats.HealthPercent;
                var filled = (int)(percent / 10);
                var empty = 10 - filled;
                var bar = new string('█', filled) +
                              new string('░', empty);
                return $"[{bar}] " +
                       $"{Character.Stats.CurrentHitPoints}/" +
                       $"{Character.Stats.MaxHitPoints}";
            }
        }

        public string CombatSummary =>
            $"{Character.Name} | " +
            $"{StateDisplay} | " +
            $"HP: {HealthBar} | " +
            $"AC: {EffectiveAC} | " +
            $"Init: {InitiativeTotal}" +
            (ActiveEffects.Any()
                ? $" | Effects: {string.Join(", ", ActiveEffects.Select(e => e.Status))}"
                : string.Empty);

        public override string ToString() => CombatSummary;
    }
}
using RealmsEdge.Shared.Enums;

namespace RealmsEdge.Shared.Models.Combat
{
    public enum ActionResult
    {
        Hit,                // Attack connected
        Miss,               // Attack missed
        CriticalHit,        // Natural 20 — double damage
        CriticalMiss,       // Natural 1 — lose next action
        Blocked,            // Defender blocked attack
        Dodged,             // Defender dodged attack
        Resisted,           // Magic fully resisted
        Reflected,          // Magic reflected back
        Countered           // Parry triggered counter attack
    }

    public class CombatAction
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public int Round { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // =====================
        // Participants
        // =====================

        public int ActorCombatId { get; set; }          // Who acted
        public string ActorName { get; set; } = string.Empty;
        public int? TargetCombatId { get; set; }        // Who was targeted
        public string? TargetName { get; set; }

        // =====================
        // Action Details
        // =====================

        public CombatActionType ActionType { get; set; }
        public DamageType DamageType { get; set; }
        public ActionResult Result { get; set; }

        // =====================
        // Dice Rolls
        // =====================

        public int AttackRoll { get; set; }             // D20 roll
        public int AttackBonus { get; set; }            // Modifier applied
        public int AttackTotal { get; set; }            // Roll + bonus
        public int TargetAC { get; set; }               // What needed to hit
        public int DamageRoll { get; set; }             // Damage die result
        public int DamageBonus { get; set; }            // Damage modifier
        public int DamageTotal { get; set; }            // Final damage dealt
        public bool WasCritical { get; set; }
        public bool WasCriticalFail { get; set; }
        public bool HadAdvantage { get; set; }
        public bool HadDisadvantage { get; set; }

        // =====================
        // Effects Applied
        // =====================

        public List<CharacterStatus> StatusesApplied { get; set; }
            = new();
        public List<CharacterStatus> StatusesRemoved { get; set; }
            = new();
        public int HealingDone { get; set; } = 0;
        public int ManaSpent { get; set; } = 0;
        public int StaminaSpent { get; set; } = 0;

        // =====================
        // Special Flags
        // =====================

        public bool IsKillingBlow { get; set; } = false;
        public bool TriggeredCounterAttack { get; set; } = false;
        public bool WasSurpriseAttack { get; set; } = false;
        public string? SpecialAbilityUsed { get; set; }
        public string? ItemUsed { get; set; }

        // =====================
        // Display Helpers
        // =====================

        public string ResultDisplay => Result switch
        {
            ActionResult.Hit => "✅ Hit",
            ActionResult.Miss => "❌ Miss",
            ActionResult.CriticalHit => "⚔️ CRITICAL HIT!",
            ActionResult.CriticalMiss => "💀 CRITICAL MISS!",
            ActionResult.Blocked => "🛡️ Blocked",
            ActionResult.Dodged => "💨 Dodged",
            ActionResult.Resisted => "✨ Resisted",
            ActionResult.Reflected => "🔄 Reflected",
            ActionResult.Countered => "⚡ Countered",
            _ => "Unknown"
        };

        public string ActionDisplay => ActionType switch
        {
            CombatActionType.MeleeAttack => "⚔️ attacks",
            CombatActionType.RangedAttack => "🏹 shoots at",
            CombatActionType.CastSpell => "✨ casts a spell at",
            CombatActionType.UsePotion => "🧪 drinks a potion",
            CombatActionType.HealAlly => "💚 heals",
            CombatActionType.Defend => "🛡️ takes a defensive stance",
            CombatActionType.Flee => "🏃 attempts to flee",
            CombatActionType.Rage => "😡 enters a rage",
            CombatActionType.BardSong => "🎵 plays an inspiring song",
            CombatActionType.BattleCry => "📣 lets out a battle cry",
            CombatActionType.Intimidate => "😱 attempts to intimidate",
            CombatActionType.Negotiate => "🤝 attempts to negotiate",
            CombatActionType.Wait => "⏳ waits",
            CombatActionType.Guard => "🛡️ takes up a guard position",
            CombatActionType.PowerAttack => "💥 makes a powerful attack at",
            CombatActionType.PrecisionAttack => "🎯 makes a precise attack at",
            CombatActionType.SummonAlly => "🌑 summons an ally",
            CombatActionType.Sacrifice => "🩸 sacrifices health for power",
            _ => "acts"
        };

        // =====================
        // Combat Log Entry
        // =====================

        public string ToCombatLogEntry()
        {
            var sb = new System.Text.StringBuilder();

            // Base action
            sb.Append($"[R{Round}] {ActorName} {ActionDisplay}");

            if (TargetName != null)
                sb.Append($" {TargetName}");

            sb.Append($" — {ResultDisplay}");

            // Attack roll details
            if (ActionType == CombatActionType.MeleeAttack  ||
                ActionType == CombatActionType.RangedAttack ||
                ActionType == CombatActionType.PowerAttack  ||
                ActionType == CombatActionType.PrecisionAttack)
            {
                sb.Append(
                    $" (rolled {AttackRoll}" +
                    $"{(AttackBonus >= 0 ? "+" : "")}{AttackBonus}" +
                    $"={AttackTotal} vs AC {TargetAC})");
            }

            // Damage
            if (DamageTotal > 0)
            {
                sb.Append($" for {DamageTotal} {DamageType} damage");
                if (WasCritical)
                    sb.Append(" 💥 CRIT!");
            }

            // Healing
            if (HealingDone > 0)
                sb.Append($" restoring {HealingDone} HP");

            // Status effects
            if (StatusesApplied.Any())
                sb.Append($" applying " +
                    $"{string.Join(", ", StatusesApplied)}");

            // Killing blow
            if (IsKillingBlow)
                sb.Append($" ☠️ {TargetName} is slain!");

            // Critical miss
            if (WasCriticalFail)
                sb.Append(
                    " The attack goes wildly wrong!");

            // Resources spent
            if (ManaSpent > 0)
                sb.Append($" [{ManaSpent} mana spent]");
            if (StaminaSpent > 0)
                sb.Append(
                    $" [{StaminaSpent} stamina spent]");

            return sb.ToString();
        }

        // =====================
        // Factory Methods
        // =====================

        public static CombatAction CreateAttack(
            int round,
            Combatant actor,
            Combatant target,
            int attackRoll,
            int attackBonus,
            int targetAC,
            int damageRoll,
            int damageBonus,
            DamageType damageType,
            bool isCritical,
            bool isCriticalFail)
        {
            var total = attackRoll + attackBonus;
            var hit = !isCriticalFail &&
                         (isCritical || total >= targetAC);
            var damage = hit
                ? (isCritical
                    ? (damageRoll + damageBonus) * 2
                    : damageRoll + damageBonus)
                : 0;

            return new CombatAction
            {
                Round          = round,
                ActorCombatId  = actor.CombatId,
                ActorName      = actor.Character.Name,
                TargetCombatId = target.CombatId,
                TargetName     = target.Character.Name,
                ActionType     = CombatActionType.MeleeAttack,
                DamageType     = damageType,
                Result         = isCriticalFail
                    ? ActionResult.CriticalMiss
                    : isCritical
                        ? ActionResult.CriticalHit
                        : hit
                            ? ActionResult.Hit
                            : ActionResult.Miss,
                AttackRoll     = attackRoll,
                AttackBonus    = attackBonus,
                AttackTotal    = total,
                TargetAC       = targetAC,
                DamageRoll     = damageRoll,
                DamageBonus    = damageBonus,
                DamageTotal    = damage,
                WasCritical    = isCritical,
                WasCriticalFail = isCriticalFail,
                HadAdvantage   = actor.HasAdvantage,
                HadDisadvantage = actor.HasDisadvantage
            };
        }

        public static CombatAction CreateHeal(
            int round,
            Combatant actor,
            Combatant target,
            int healAmount,
            int manaSpent)
        {
            return new CombatAction
            {
                Round          = round,
                ActorCombatId  = actor.CombatId,
                ActorName      = actor.Character.Name,
                TargetCombatId = target.CombatId,
                TargetName     = target.Character.Name,
                ActionType     = CombatActionType.HealAlly,
                Result         = ActionResult.Hit,
                HealingDone    = healAmount,
                ManaSpent      = manaSpent
            };
        }

        public static CombatAction CreateStatusAction(
            int round,
            Combatant actor,
            Combatant target,
            CombatActionType actionType,
            CharacterStatus statusApplied,
            int manaSpent = 0)
        {
            return new CombatAction
            {
                Round            = round,
                ActorCombatId    = actor.CombatId,
                ActorName        = actor.Character.Name,
                TargetCombatId   = target.CombatId,
                TargetName       = target.Character.Name,
                ActionType       = actionType,
                Result           = ActionResult.Hit,
                StatusesApplied  = new() { statusApplied },
                ManaSpent        = manaSpent
            };
        }
    }
}
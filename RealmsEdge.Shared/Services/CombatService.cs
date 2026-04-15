using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Combat;
using RealmsEdge.Shared.Models.Items;
using RealmsEdge.Shared.Models.Party;

namespace RealmsEdge.Shared.Services
{
    public class CombatService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly DiceService _diceService;
        private readonly CharacterService _characterService;
        private readonly PartyService _partyService;
        private readonly EncounterService _encounterService;

        public CombatService(
            DiceService diceService,
            CharacterService characterService,
            PartyService partyService,
            EncounterService encounterService)
        {
            _diceService      = diceService;
            _characterService = characterService;
            _partyService     = partyService;
            _encounterService = encounterService;
        }

        // =====================
        // Start Combat
        // =====================

        public CombatState StartCombat(
            string battleName,
            List<PlayerCharacter> players,
            List<NpcCharacter> enemies,
            bool isSurpriseRound = false,
            Party? party = null)
        {
            var state = new CombatState
            {
                BattleName = battleName,
                Phase      = CombatPhase.Initiative
            };

            state.Log($"⚔️ {battleName} begins!");

            // Add players as combatants
            foreach (var player in players)
            {
                var combatant = state.AddCombatant(
                    player,
                    CombatTeam.Players,
                    isPlayerControlled: true);

                // Roll initiative
                RollInitiative(combatant, isSurpriseRound);
                state.Log(
                    $"🎲 {player.Name} rolls initiative: " +
                    $"{combatant.InitiativeTotal}");
            }

            // Add enemies as combatants
            foreach (var enemy in enemies)
            {
                var combatant = state.AddCombatant(
                    enemy,
                    CombatTeam.Enemies,
                    isPlayerControlled: false);

                // Enemies get advantage on surprise round
                RollInitiative(combatant, false);
                if (isSurpriseRound)
                    combatant.InitiativeTotal += 5;

                state.Log(
                    $"🎲 {enemy.Name} rolls initiative: " +
                    $"{combatant.InitiativeTotal}");
            }

            // Sort by initiative — highest goes first
            var initiativeOrder = state.Combatants
                .OrderByDescending(c => c.InitiativeTotal)
                .ThenByDescending(c =>
                    c.Character.Stats.DexterityModifier)
                .ToList();

            state.SetInitiativeOrder(initiativeOrder);

            state.Log("📋 Initiative order: " +
                string.Join(", ", initiativeOrder
                    .Select(c =>
                        $"{c.Character.Name}({c.InitiativeTotal})")));

            // Start first round
            state.Phase = CombatPhase.Active;
            state.StartNewRound();
            state.Phase = CombatPhase.PlayerDecision;

            return state;
        }

        // =====================
        // Process Player Action
        // =====================

        public async Task<CombatResult> ProcessPlayerAction(
            CombatState state,
            int actorCombatId,
            CombatActionType actionType,
            int? targetCombatId = null,
            string? spellName = null,
            Guid? itemId = null,
            Party? party = null)
        {
            var actor = state.GetCombatant(actorCombatId);
            if (actor == null)
                return NotOver(state,
                    "Combatant not found.");

            if (!actor.CanAct)
                return NotOver(state,
                    $"{actor.Character.Name} cannot act.");

            if (actor.HasActedThisRound)
                return NotOver(state,
                    $"{actor.Character.Name} has already acted.");

            state.Phase = CombatPhase.ResolvingAction;
            var round = state.CurrentRoundData!;

            // Get target if needed
            Combatant? target = null;
            if (targetCombatId.HasValue)
            {
                target = state.GetCombatant(
                    targetCombatId.Value);
                if (target == null)
                    return NotOver(state, "Target not found.");
            }

            // Route to correct handler
            CombatAction? action = actionType switch
            {
                CombatActionType.MeleeAttack     or
                CombatActionType.PowerAttack     or
                CombatActionType.PrecisionAttack =>
                    ResolveMeleeAttack(
                        state, actor, target!, actionType),

                CombatActionType.RangedAttack =>
                    ResolveRangedAttack(
                        state, actor, target!),

                CombatActionType.CastSpell =>
                    ResolveCastSpell(
                        state, actor, target, spellName),

                CombatActionType.HealAlly =>
                    ResolveHeal(state, actor, target ?? actor),

                CombatActionType.UsePotion =>
                    ResolveUsePotion(
                        state, actor, itemId),

                CombatActionType.Defend =>
                    ResolveDefend(state, actor),

                CombatActionType.Rage =>
                    ResolveRage(state, actor),

                CombatActionType.BardSong =>
                    ResolveBardSong(state, actor, party),

                CombatActionType.BattleCry =>
                    ResolveBattleCry(state, actor, party),

                CombatActionType.Intimidate =>
                    ResolveIntimidate(
                        state, actor, target!),

                CombatActionType.Negotiate =>
                    ResolveNegotiate(state, actor),

                CombatActionType.Guard =>
                    ResolveGuard(
                        state, actor, target),

                CombatActionType.Wait =>
                    ResolveWait(state, actor),

                CombatActionType.Flee => null,

                _ => null
            };

            // Handle flee separately
            if (actionType == CombatActionType.Flee)
            {
                return await ResolveFlee(
                    state, actor, party);
            }

            if (action != null)
            {
                round.RecordAction(action);
                state.Log(action.ToCombatLogEntry());
                actor.EndTurn();
            }

            // Check combat end
            var endResult = CheckCombatEnd(
                state, party);
            if (endResult != null) return endResult;

            // Advance to next combatant
            return await AdvanceToNextCombatant(
                state, party);
        }

        // =====================
        // Melee Attack
        // =====================

        private CombatAction ResolveMeleeAttack(
            CombatState state,
            Combatant actor,
            Combatant target,
            CombatActionType actionType)
        {
            // Determine attack bonus
            var attackBonus = actionType switch
            {
                CombatActionType.PowerAttack =>
                    actor.EffectiveAttackBonus - 3,
                CombatActionType.PrecisionAttack =>
                    actor.EffectiveAttackBonus + 3,
                _ =>
                    actor.EffectiveAttackBonus
            };

            // Roll to hit
            var attackRoll = RollAttack(actor);
            var isCrit = attackRoll == 20;
            var isCritFail = attackRoll == 1;

            // Determine damage die from equipped weapon
            var weapon = (actor.Character as PlayerCharacter)
                ?.EquippedWeapon;
            var damageDie = weapon?.DamageDie ?? DiceType.D4;

            // Power attack does extra damage die
            var damageRoll = actionType ==
                CombatActionType.PowerAttack
                    ? _diceService.Roll(damageDie, 2,
                        actor.EffectiveDamageBonus)
                    : _diceService.Roll(damageDie, 1,
                        actor.EffectiveDamageBonus);

            var damageType = GetWeaponDamageType(weapon);
            var action = CombatAction.CreateAttack(
                state.CurrentRound,
                actor, target,
                attackRoll, attackBonus,
                target.EffectiveAC,
                damageRoll.Total,
                actor.EffectiveDamageBonus,
                damageType,
                isCrit, isCritFail);

            // Apply damage
            if (action.Result == ActionResult.Hit ||
                action.Result == ActionResult.CriticalHit)
            {
                var dealt = target.ApplyDamage(
                    action.DamageTotal, damageType, isCrit);

                action.IsKillingBlow = !target.IsAlive;

                // Update player stats
                if (actor.Character is PlayerCharacter pc)
                {
                    pc.TotalDamageDealt += dealt;
                    if (isCrit) pc.TotalCriticalHits++;
                    if (action.IsKillingBlow) pc.TotalKills++;
                    _characterService.UpdateCharacter(pc);
                }
            }
            else if (action.Result ==
                     ActionResult.CriticalMiss)
            {
                state.Log(
                    $"💀 {actor.Character.Name}'s attack " +
                    $"goes wildly wrong!");

                if (actor.Character is PlayerCharacter pcf)
                {
                    pcf.TotalCriticalFails++;
                    _characterService.UpdateCharacter(pcf);
                }
            }

            return action;
        }

        // =====================
        // Ranged Attack
        // =====================

        private CombatAction ResolveRangedAttack(
            CombatState state,
            Combatant actor,
            Combatant target)
        {
            var attackRoll = RollAttack(actor);
            var isCrit = attackRoll == 20;
            var isCritFail = attackRoll == 1;

            var weapon = (actor.Character as
                PlayerCharacter)?.EquippedWeapon;
            var damageDie = weapon?.DamageDie ?? DiceType.D6;

            var damageRoll = _diceService.Roll(
                damageDie, 1,
                actor.EffectiveRangedBonus);

            var action = CombatAction.CreateAttack(
                state.CurrentRound,
                actor, target,
                attackRoll,
                actor.EffectiveRangedBonus,
                target.EffectiveAC,
                damageRoll.Total,
                actor.EffectiveRangedBonus,
                DamageType.Piercing,
                isCrit, isCritFail);

            if (action.Result == ActionResult.Hit ||
                action.Result == ActionResult.CriticalHit)
            {
                target.ApplyDamage(
                    action.DamageTotal,
                    DamageType.Piercing,
                    isCrit);

                action.IsKillingBlow = !target.IsAlive;
            }

            return action;
        }

        // =====================
        // Spell Casting
        // =====================

        private CombatAction? ResolveCastSpell(
            CombatState state,
            Combatant actor,
            Combatant? target,
            string? spellName)
        {
            var manaCost = 5;

            if (!actor.Character.Stats.SpendMana(manaCost))
            {
                state.Log(
                    $"✨ {actor.Character.Name} has " +
                    $"no mana to cast spells!");
                return null;
            }

            var spellRoll = _diceService.Roll(
                DiceType.D8, 2,
                actor.EffectiveSpellBonus);

            var damageType = actor.Character.Class switch
            {
                CharacterClass.Necromancer => DamageType.Shadow,
                CharacterClass.Druid => DamageType.Poison,
                CharacterClass.Cleric => DamageType.Holy,
                CharacterClass.Warlock => DamageType.Shadow,
                _ => DamageType.Arcane
            };

            var effectiveTarget = target
                ?? state.GetLowestHpTarget(
                    CombatTeam.Enemies);

            if (effectiveTarget == null)
            {
                state.Log("No valid spell target.");
                return null;
            }

            // Magic resistance save
            var saveRoll = _diceService.Roll(
                DiceType.D20, 1,
                effectiveTarget.Character
                    .Stats.WisdomModifier);

            if (saveRoll.Total >= 15 &&
                effectiveTarget.Character.Stats
                    .ResistMagic > 0)
            {
                state.Log(
                    $"✨ {effectiveTarget.Character.Name} " +
                    $"resists the spell!");

                return new CombatAction
                {
                    Round          = state.CurrentRound,
                    ActorCombatId  = actor.CombatId,
                    ActorName      = actor.Character.Name,
                    TargetCombatId = effectiveTarget.CombatId,
                    TargetName     = effectiveTarget.Character.Name,
                    ActionType     = CombatActionType.CastSpell,
                    Result         = ActionResult.Resisted,
                    ManaSpent      = manaCost
                };
            }

            effectiveTarget.ApplyDamage(
                spellRoll.Total, damageType, false);

            return new CombatAction
            {
                Round          = state.CurrentRound,
                ActorCombatId  = actor.CombatId,
                ActorName      = actor.Character.Name,
                TargetCombatId = effectiveTarget.CombatId,
                TargetName     = effectiveTarget.Character.Name,
                ActionType     = CombatActionType.CastSpell,
                DamageType     = damageType,
                Result         = ActionResult.Hit,
                DamageTotal    = spellRoll.Total,
                ManaSpent      = manaCost,
                IsKillingBlow  = !effectiveTarget.IsAlive
            };
        }

        // =====================
        // Healing
        // =====================

        private CombatAction? ResolveHeal(
            CombatState state,
            Combatant actor,
            Combatant target)
        {
            var manaCost = 4;

            if (!actor.Character.Stats.SpendMana(manaCost))
            {
                state.Log(
                    $"💚 {actor.Character.Name} has " +
                    $"no mana to heal!");
                return null;
            }

            var healRoll = _diceService.Roll(
                DiceType.D8, 1,
                actor.Character.Stats.WisdomModifier);

            var healAmount = Math.Max(1, healRoll.Total);
            target.Character.Stats.ApplyHealing(healAmount);

            if (actor.Character is PlayerCharacter pc)
            {
                pc.TotalHealingDone += healAmount;
                _characterService.UpdateCharacter(pc);
            }

            return CombatAction.CreateHeal(
                state.CurrentRound,
                actor, target,
                healAmount, manaCost);
        }

        // =====================
        // Use Potion
        // =====================

        private CombatAction? ResolveUsePotion(
            CombatState state,
            Combatant actor,
            Guid? itemId)
        {
            var player = actor.Character as PlayerCharacter;
            if (player == null) return null;

            InventoryItem? potion;

            if (itemId.HasValue)
                potion = player.Inventory
                    .FirstOrDefault(i => i.Id == itemId);
            else
                potion = player.Inventory
                    .FirstOrDefault(i =>
                        i.Type == ItemType.Potion &&
                        i.HealAmount > 0);

            if (potion == null)
            {
                state.Log(
                    $"🧪 {player.Name} has no potions!");
                return null;
            }

            // Apply potion effects
            if (potion.HealAmount > 0)
                actor.Character.Stats
                    .ApplyHealing(potion.HealAmount);

            if (potion.ManaRestoreAmount > 0)
                actor.Character.Stats.CurrentMana =
                    Math.Min(
                        actor.Character.Stats.MaxMana,
                        actor.Character.Stats.CurrentMana +
                        potion.ManaRestoreAmount);

            if (potion.CuresStatus.HasValue)
                actor.RemoveEffect(potion.CuresStatus.Value);

            if (potion.AppliesStatus.HasValue)
                actor.ApplyEffect(new StatusEffect
                {
                    Status         = potion.AppliesStatus.Value,
                    DurationRounds = potion.StatusDuration
                });

            // Remove potion from inventory
            player.RemoveItem(potion.Id);
            _characterService.UpdateCharacter(player);

            state.Log(
                $"🧪 {player.Name} uses {potion.Name}!");

            return new CombatAction
            {
                Round         = state.CurrentRound,
                ActorCombatId = actor.CombatId,
                ActorName     = actor.Character.Name,
                ActionType    = CombatActionType.UsePotion,
                Result        = ActionResult.Hit,
                HealingDone   = potion.HealAmount,
                ItemUsed      = potion.Name
            };
        }

        // =====================
        // Defend
        // =====================

        private CombatAction ResolveDefend(
            CombatState state,
            Combatant actor)
        {
            actor.State            = CombatantState.Defending;
            actor.TemporaryAcBonus = 4;

            state.Log(
                $"🛡️ {actor.Character.Name} takes a " +
                $"defensive stance (+4 AC).");

            return new CombatAction
            {
                Round         = state.CurrentRound,
                ActorCombatId = actor.CombatId,
                ActorName     = actor.Character.Name,
                ActionType    = CombatActionType.Defend,
                Result        = ActionResult.Hit
            };
        }

        // =====================
        // Rage
        // =====================

        private CombatAction ResolveRage(
            CombatState state,
            Combatant actor)
        {
            var activated = actor.ActivateRage();

            state.Log(activated
                ? $"😡 {actor.Character.Name} enters " +
                  $"a berserker rage! (+2 attack, +3 damage)"
                : $"{actor.Character.Name} cannot rage!");

            return new CombatAction
            {
                Round            = state.CurrentRound,
                ActorCombatId    = actor.CombatId,
                ActorName        = actor.Character.Name,
                ActionType       = CombatActionType.Rage,
                Result           = activated
                    ? ActionResult.Hit
                    : ActionResult.Miss,
                StaminaSpent     = activated ? 2 : 0,
                SpecialAbilityUsed = "Berserker Rage"
            };
        }

        // =====================
        // Bard Song
        // =====================

        private CombatAction ResolveBardSong(
            CombatState state,
            Combatant actor,
            Party? party)
        {
            if (!actor.Character.Stats.SpendMana(3))
            {
                state.Log(
                    $"🎵 {actor.Character.Name} " +
                    $"has no mana to sing!");
                return new CombatAction
                {
                    Round         = state.CurrentRound,
                    ActorCombatId = actor.CombatId,
                    ActorName     = actor.Character.Name,
                    ActionType    = CombatActionType.BardSong,
                    Result        = ActionResult.Miss
                };
            }

            // Buff all player combatants
            foreach (var ally in state.ActivePlayers)
            {
                ally.TemporaryAttackBonus += 2;
                ally.TemporaryDamageBonus += 1;
                ally.ApplyEffect(new StatusEffect
                {
                    Status         = CharacterStatus.Blessed,
                    DurationRounds = 3
                });
            }

            state.Log(
                $"🎵 {actor.Character.Name}'s song " +
                $"inspires the party! " +
                $"(+2 attack, +1 damage for 3 rounds)");

            return new CombatAction
            {
                Round              = state.CurrentRound,
                ActorCombatId      = actor.CombatId,
                ActorName          = actor.Character.Name,
                ActionType         = CombatActionType.BardSong,
                Result             = ActionResult.Hit,
                ManaSpent          = 3,
                StatusesApplied    = new()
                    { CharacterStatus.Blessed },
                SpecialAbilityUsed = "Bardic Inspiration"
            };
        }

        // =====================
        // Battle Cry
        // =====================

        private CombatAction ResolveBattleCry(
            CombatState state,
            Combatant actor,
            Party? party)
        {
            if (!actor.Character.Stats.SpendStamina(2))
            {
                state.Log(
                    $"📣 {actor.Character.Name} has " +
                    $"no stamina for a battle cry!");
                return new CombatAction
                {
                    Round         = state.CurrentRound,
                    ActorCombatId = actor.CombatId,
                    ActorName     = actor.Character.Name,
                    ActionType    = CombatActionType.BattleCry,
                    Result        = ActionResult.Miss
                };
            }

            // Boost attack for all allies this round
            foreach (var ally in state.ActivePlayers
                .Where(a => a.CombatId != actor.CombatId))
            {
                ally.TemporaryAttackBonus += 3;
            }

            state.Log(
                $"📣 {actor.Character.Name} lets out " +
                $"a battle cry! Allies gain +3 attack!");

            return new CombatAction
            {
                Round              = state.CurrentRound,
                ActorCombatId      = actor.CombatId,
                ActorName          = actor.Character.Name,
                ActionType         = CombatActionType.BattleCry,
                Result             = ActionResult.Hit,
                StaminaSpent       = 2,
                SpecialAbilityUsed = "Battle Cry"
            };
        }

        // =====================
        // Intimidate
        // =====================

        private CombatAction ResolveIntimidate(
            CombatState state,
            Combatant actor,
            Combatant target)
        {
            var intimidateRoll = _diceService.Roll(
                DiceType.D20, 1,
                actor.Character.Stats.CharismaModifier);

            var targetWill = _diceService.Roll(
                DiceType.D20, 1,
                target.Character.Stats.WisdomModifier);

            state.Log(
                $"😱 {actor.Character.Name} attempts " +
                $"to intimidate {target.Character.Name}! " +
                $"({intimidateRoll.Total} vs {targetWill.Total})");

            if (intimidateRoll.Total > targetWill.Total)
            {
                target.ApplyEffect(new StatusEffect
                {
                    Status         = CharacterStatus.Feared,
                    DurationRounds = 2
                });

                state.Log(
                    $"😨 {target.Character.Name} is " +
                    $"frightened!");

                return CombatAction.CreateStatusAction(
                    state.CurrentRound,
                    actor, target,
                    CombatActionType.Intimidate,
                    CharacterStatus.Feared);
            }

            state.Log(
                $"{target.Character.Name} holds firm!");

            return new CombatAction
            {
                Round         = state.CurrentRound,
                ActorCombatId = actor.CombatId,
                ActorName     = actor.Character.Name,
                ActionType    = CombatActionType.Intimidate,
                Result        = ActionResult.Miss
            };
        }

        // =====================
        // Negotiate
        // =====================

        private CombatAction ResolveNegotiate(
            CombatState state,
            Combatant actor)
        {
            var negotiateRoll = _diceService.Roll(
                DiceType.D20, 1,
                actor.Character.Stats.CharismaModifier);

            state.Log(
                $"🤝 {actor.Character.Name} attempts " +
                $"to negotiate! (rolled {negotiateRoll.Total})");

            // High charisma roll ends combat peacefully
            if (negotiateRoll.Total >= 18)
            {
                state.Phase = CombatPhase.Negotiated;
                state.Log(
                    "🤝 Negotiation successful! " +
                    "Enemies stand down!");
            }
            else
            {
                state.Log(
                    "❌ Negotiation failed. " +
                    "Enemies press the attack!");
            }

            return new CombatAction
            {
                Round         = state.CurrentRound,
                ActorCombatId = actor.CombatId,
                ActorName     = actor.Character.Name,
                ActionType    = CombatActionType.Negotiate,
                Result        = negotiateRoll.Total >= 18
                    ? ActionResult.Hit
                    : ActionResult.Miss
            };
        }

        // =====================
        // Guard
        // =====================

        private CombatAction ResolveGuard(
            CombatState state,
            Combatant actor,
            Combatant? target)
        {
            actor.IsGuarding = true;
            actor.GuardingCharacterId = target?.Character.Id;

            state.Log(
                $"🛡️ {actor.Character.Name} guards " +
                $"{target?.Character.Name ?? "the party"}!");

            return new CombatAction
            {
                Round         = state.CurrentRound,
                ActorCombatId = actor.CombatId,
                ActorName     = actor.Character.Name,
                ActionType    = CombatActionType.Guard,
                Result        = ActionResult.Hit
            };
        }

        // =====================
        // Wait
        // =====================

        private CombatAction ResolveWait(
            CombatState state,
            Combatant actor)
        {
            actor.State = CombatantState.Waiting;
            state.Log(
                $"⏳ {actor.Character.Name} waits...");

            return new CombatAction
            {
                Round         = state.CurrentRound,
                ActorCombatId = actor.CombatId,
                ActorName     = actor.Character.Name,
                ActionType    = CombatActionType.Wait,
                Result        = ActionResult.Hit
            };
        }

        // =====================
        // Flee
        // =====================

        private async Task<CombatResult> ResolveFlee(
            CombatState state,
            Combatant actor,
            Party? party)
        {
            var fleeRoll = _diceService.Roll(
                DiceType.D20, 1,
                actor.Character.Stats.DexterityModifier);

            state.Log(
                $"🏃 {actor.Character.Name} attempts " +
                $"to flee! (rolled {fleeRoll.Total})");

            if (fleeRoll.Total >= 10)
            {
                actor.State = CombatantState.Fled;
                state.Log(
                    $"{actor.Character.Name} escapes!");

                // If all players fled end combat
                if (state.ActivePlayers.Count == 0)
                    return state.BuildFledResult();
            }
            else
            {
                // Failed flee — take opportunity attack
                var attacker = state.ActiveEnemies
                    .FirstOrDefault();
                if (attacker != null)
                {
                    var oppAttack = ResolveMeleeAttack(
                        state, attacker, actor,
                        CombatActionType.MeleeAttack);
                    state.CurrentRoundData?
                        .RecordAction(oppAttack);
                    state.Log(
                        $"⚔️ Opportunity attack! " +
                        $"{oppAttack.ToCombatLogEntry()}");
                }

                state.Log(
                    $"❌ {actor.Character.Name} " +
                    $"fails to flee!");
            }

            await Task.CompletedTask;
            var endResult = CheckCombatEnd(state, party);
            if (endResult != null) return endResult;

            return await AdvanceToNextCombatant(
                state, party);
        }

        // =====================
        // Enemy AI Turn
        // =====================

        public async Task<CombatResult> ProcessEnemyTurn(
            CombatState state,
            Party? party = null)
        {
            var enemy = state.CurrentCombatant;
            if (enemy == null || enemy.IsPlayerControlled)
                return await AdvanceToNextCombatant(
                    state, party);

            if (!enemy.CanAct || !enemy.IsAlive)
                return await AdvanceToNextCombatant(
                    state, party);

            // Process status effects first
            var statusLog = enemy.ProcessStatusEffects();
            state.LogRange(statusLog);

            // Check morale
            if (state.CheckEnemyMorale(_diceService))
            {
                return await ResolveFlee(
                    state, enemy, party);
            }

            // Check if should flee due to low HP
            if (enemy.ShouldFlee)
            {
                state.Log(
                    $"😨 {enemy.Character.Name} " +
                    $"attempts to flee!");
                return await ResolveFlee(
                    state, enemy, party);
            }

            var npc = enemy.Character as NpcCharacter;

            // Choose action based on behaviour
            CombatAction? action = npc?.Behaviour switch
            {
                NpcBehaviour.Aggressive =>
                    ProcessAggressiveEnemy(state, enemy),
                NpcBehaviour.Defensive =>
                    ProcessDefensiveEnemy(state, enemy),
                NpcBehaviour.Cowardly =>
                    ProcessCowardlyEnemy(state, enemy),
                _ =>
                    ProcessAggressiveEnemy(state, enemy)
            };

            if (action != null)
            {
                state.CurrentRoundData?.RecordAction(action);
                state.Log(action.ToCombatLogEntry());
            }

            enemy.EndTurn();

            await Task.CompletedTask;
            var endResult = CheckCombatEnd(state, party);
            if (endResult != null) return endResult;

            return await AdvanceToNextCombatant(
                state, party);
        }

        // =====================
        // Enemy AI Behaviours
        // =====================

        private CombatAction ProcessAggressiveEnemy(
            CombatState state,
            Combatant enemy)
        {
            // Target lowest HP player
            var target = state.GetLowestHpTarget(
                CombatTeam.Players);

            if (target == null)
                return ResolveWait(state, enemy);

            return ResolveMeleeAttack(
                state, enemy, target,
                CombatActionType.MeleeAttack);
        }

        private CombatAction ProcessDefensiveEnemy(
            CombatState state,
            Combatant enemy)
        {
            // Defend if below 50% HP
            if (enemy.Character.Stats.HealthPercent < 50)
                return ResolveDefend(state, enemy);

            // Otherwise attack highest threat
            var target = state.GetHighestThreatTarget(
                CombatTeam.Players);

            if (target == null)
                return ResolveWait(state, enemy);

            return ResolveMeleeAttack(
                state, enemy, target,
                CombatActionType.MeleeAttack);
        }

        private CombatAction ProcessCowardlyEnemy(
            CombatState state,
            Combatant enemy)
        {
            // Always defend when bloodied
            if (enemy.Character.Stats.IsBloodied)
                return ResolveDefend(state, enemy);

            var target = state.GetRandomTarget(
                CombatTeam.Players, _diceService);

            if (target == null)
                return ResolveWait(state, enemy);

            return ResolveMeleeAttack(
                state, enemy, target,
                CombatActionType.MeleeAttack);
        }

        // =====================
        // Round Management
        // =====================

        private async Task<CombatResult> AdvanceToNextCombatant(
            CombatState state,
            Party? party)
        {
            var next = state.AdvanceInitiative();

            if (next == null)
            {
                // End of round
                return await EndRound(state, party);
            }

            // Process status effects at turn start
            var statusLog = next.ProcessStatusEffects();
            state.LogRange(statusLog);

            // Check if stunned
            if (next.HasEffect(CharacterStatus.Stunned))
            {
                state.Log(
                    $"😵 {next.Character.Name} is " +
                    $"stunned and loses their turn!");
                next.EndTurn();
                return await AdvanceToNextCombatant(
                    state, party);
            }

            // Set phase based on who's next
            state.Phase = next.IsPlayerControlled
                ? CombatPhase.PlayerDecision
                : CombatPhase.ResolvingAction;

            // Auto process enemy turns
            if (!next.IsPlayerControlled)
                return await ProcessEnemyTurn(state, party);

            return NotOver(state,
                $"⚔️ {next.Character.Name}'s turn.");
        }

        private async Task<CombatResult> EndRound(
            CombatState state,
            Party? party)
        {
            state.Phase = CombatPhase.EndOfRound;

            // Build round summary
            var summary = state.CurrentRoundData?
                .BuildSummary();

            if (summary != null)
            {
                state.LogRange(summary.EventLog);
                state.Log(summary.RoundDisplay);
            }

            // Check max rounds safety limit
            if (state.CurrentRound >= state.MaxRounds)
            {
                state.Log(
                    "⏰ Maximum rounds reached! " +
                    "Combat ends in a draw.");
                return state.BuildFledResult();
            }

            // Check combat end conditions
            var endResult = CheckCombatEnd(state, party);
            if (endResult != null) return endResult;

            // Start new round
            state.StartNewRound();
            state.Phase = CombatPhase.Active;

            state.Log(
                $"━━━ Round {state.CurrentRound} begins ━━━");

            await Task.CompletedTask;
            return await AdvanceToNextCombatant(
                state, party);
        }

        // =====================
        // Combat End Check
        // =====================

        private CombatResult? CheckCombatEnd(
            CombatState state,
            Party? party)
        {
            if (state.Phase == CombatPhase.Negotiated)
            {
                return new CombatResult
                {
                    IsOver      = true,
                    Outcome     = CombatPhase.Negotiated,
                    Message     = "Combat ended peacefully.",
                    TotalRounds = state.CurrentRound,
                    EventLog    = state.GetRecentLog(20),
                    SoundToPlay = SoundEffect.MenuSelect
                };
            }

            if (state.AllEnemiesDefeated)
            {
                state.Log("🏆 All enemies defeated!");
                return state.BuildVictoryResult();
            }

            if (state.AllPlayersDefeated)
            {
                state.Log("💀 All players defeated!");
                return state.BuildDefeatResult();
            }

            return null;
        }

        // =====================
        // Initiative Roll
        // =====================

        private void RollInitiative(
            Combatant combatant,
            bool isSurprised)
        {
            if (isSurprised &&
                combatant.Team == CombatTeam.Players)
            {
                combatant.InitiativeRoll  = 1;
                combatant.InitiativeTotal = 1;
                return;
            }

            var roll = _diceService.Roll(
                DiceType.D20, 1,
                combatant.Character.Stats
                    .DexterityModifier);

            combatant.InitiativeRoll  = roll.IndividualResults[0];
            combatant.InitiativeTotal = roll.Total;
        }

        // =====================
        // Helpers
        // =====================

        private int RollAttack(Combatant actor)
        {
            if (actor.HasAdvantage)
                return _diceService
                    .RollWithAdvantage(DiceType.D20).Total;

            if (actor.HasDisadvantage)
                return _diceService
                    .RollWithDisadvantage(DiceType.D20).Total;

            return _diceService.Roll(DiceType.D20)
                .IndividualResults[0];
        }

        private static DamageType GetWeaponDamageType(
            InventoryItem? weapon) => weapon?.WeaponType switch
            {
                WeaponType.Sword => DamageType.Slashing,
                WeaponType.Axe => DamageType.Slashing,
                WeaponType.Dagger => DamageType.Piercing,
                WeaponType.Bow => DamageType.Piercing,
                WeaponType.Crossbow => DamageType.Piercing,
                WeaponType.Spear => DamageType.Piercing,
                WeaponType.Mace => DamageType.Bludgeoning,
                WeaponType.Staff => DamageType.Bludgeoning,
                WeaponType.Fists => DamageType.Bludgeoning,
                _ => DamageType.Slashing
            };

        private static CombatResult NotOver(
            CombatState state,
            string message) => new()
            {
                IsOver   = false,
                Outcome  = state.Phase,
                Message  = message,
                EventLog = state.GetRecentLog(10)
            };
    }
}
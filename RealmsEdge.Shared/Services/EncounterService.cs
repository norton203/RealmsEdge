using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;
using RealmsEdge.Shared.Models.World;
using RealmsEdge.Shared.Models.Party;

namespace RealmsEdge.Shared.Services
{
    public class EncounterResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public EncounterOutcome Outcome { get; set; }
        public EncounterReward? Reward { get; set; }
        public List<string> EventLog { get; set; } = new();
        public SoundEffect? SoundToPlay { get; set; }
        public bool RequiresCombat { get; set; }
        public bool IsComplete { get; set; }
    }

    public class EncounterService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly DiceService _diceService;
        private readonly CharacterService _characterService;
        private readonly PartyService _partyService;

        public EncounterService(
            DiceService diceService,
            CharacterService characterService,
            PartyService partyService)
        {
            _diceService      = diceService;
            _characterService = characterService;
            _partyService     = partyService;
        }

        // =====================
        // Trigger Encounter
        // =====================

        public EncounterResult TriggerEncounter(
            Encounter encounter,
            PlayerCharacter player,
            Party? party = null)
        {
            // Check trigger chance
            if (encounter.TriggerChance < 100)
            {
                var triggered = _diceService
                    .RollPercentageChance(encounter.TriggerChance);
                if (!triggered)
                    return new EncounterResult
                    {
                        Success  = true,
                        Outcome  = EncounterOutcome.Ignored,
                        Message  = "Nothing happens.",
                        IsComplete = true
                    };
            }

            return encounter.Type switch
            {
                EncounterType.Combat     or
                EncounterType.EliteCombat or
                EncounterType.BossCombat or
                EncounterType.Ambush => TriggerCombatEncounter(
                    encounter, player, party),

                EncounterType.Trap => TriggerTrapEncounter(
                    encounter, player),

                EncounterType.Treasure => TriggerTreasureEncounter(
                    encounter, player),

                EncounterType.Choice => TriggerChoiceEncounter(
                    encounter, player),

                EncounterType.RandomEvent => TriggerRandomEvent(
                    encounter, player, party),

                EncounterType.Shrine => TriggerShrine(
                    encounter, player),

                EncounterType.LoreEntry => TriggerLoreEntry(
                    encounter),

                _ => new EncounterResult
                {
                    Success    = true,
                    Outcome    = EncounterOutcome.Skipped,
                    Message    = "Nothing of interest here.",
                    IsComplete = true
                }
            };
        }

        // =====================
        // Combat Encounter
        // =====================

        private EncounterResult TriggerCombatEncounter(
            Encounter encounter,
            PlayerCharacter player,
            Party? party)
        {
            var log = new List<string>();

            if (encounter.IsSurpriseRound)
            {
                log.Add("⚠️ Ambush! Enemies get a surprise round!");
                // Apply surprise damage from first enemy
                var firstEnemy = encounter.Enemies.FirstOrDefault();
                if (firstEnemy != null)
                {
                    var surpriseDamage = _diceService
                        .Roll(DiceType.D6, 1,
                            firstEnemy.Stats.StrengthModifier);
                    player.Stats.ApplyDamage(surpriseDamage.Total);
                    log.Add(
                        $"{firstEnemy.Name} strikes from the shadows! " +
                        $"{surpriseDamage.Total} damage dealt!");
                }
            }

            log.Add($"⚔️ {encounter.Name}");
            log.Add($"Enemies: {string.Join(", ", encounter.Enemies
                .Select(e => $"{e.Name} (HP:{e.Stats.CurrentHitPoints})"))}");

            return new EncounterResult
            {
                Success        = true,
                Outcome        = EncounterOutcome.Pending,
                Message        = encounter.Description,
                EventLog       = log,
                RequiresCombat = true,
                IsComplete     = false,
                SoundToPlay    = encounter.Type == EncounterType.Ambush
                    ? SoundEffect.CombatHit
                    : SoundEffect.BattleMusic
            };
        }

        // =====================
        // Resolve Combat
        // =====================

        public EncounterResult ResolveCombatVictory(
            Encounter encounter,
            List<PlayerCharacter> winners,
            Party? party = null)
        {
            var log = new List<string>();
            var partyId = party?.Id;

            log.Add("✅ Victory!");

            // Distribute rewards
            if (encounter.VictoryReward != null)
            {
                var reward = encounter.VictoryReward;

                // XP distribution
                if (reward.ExperiencePoints > 0)
                {
                    if (party != null)
                    {
                        var xpResults = _partyService
                            .DistributeXpToParty(
                                party.Id,
                                reward.ExperiencePoints);
                        log.AddRange(xpResults);
                    }
                    else
                    {
                        winners.First().AddExperience(
                            reward.ExperiencePoints);
                        log.Add(
                            $"{winners.First().Name} gains " +
                            $"{reward.ExperiencePoints:N0} XP.");
                    }
                }

                // Gold distribution
                if (reward.GoldReward > 0 ||
                    reward.SilverReward > 0 ||
                    reward.CopperReward > 0)
                {
                    if (party != null)
                    {
                        var goldPerMember = reward.GoldReward
                            / party.MemberCount;
                        foreach (var member in party.Members
                            .Where(m => m.Character.Stats.IsAlive))
                        {
                            member.Character.AddCurrency(
                                goldPerMember,
                                reward.SilverReward,
                                reward.CopperReward);
                            _characterService.UpdateCharacter(
                                member.Character);
                        }
                        log.Add(
                            $"Gold split: {goldPerMember}g " +
                            $"per member.");
                    }
                    else
                    {
                        winners.First().AddCurrency(
                            reward.GoldReward,
                            reward.SilverReward,
                            reward.CopperReward);
                        log.Add(
                            $"{winners.First().Name} finds " +
                            $"{reward.GoldReward}g " +
                            $"{reward.SilverReward}s " +
                            $"{reward.CopperReward}c.");
                    }
                }

                // Item rewards
                foreach (var item in reward.ItemRewards)
                {
                    if (party != null)
                    {
                        _partyService.DistributeLootItem(
                            party.Id, item);
                        log.Add(
                            $"🎁 {item.Name} added to party loot.");
                    }
                    else
                    {
                        winners.First().AddItem(item);
                        log.Add(
                            $"🎁 {winners.First().Name} " +
                            $"picks up {item.Name}.");
                    }
                }

                // Status effects from reward
                if (reward.StatusGranted.HasValue)
                {
                    foreach (var winner in winners)
                        winner.ApplyStatus(reward.StatusGranted.Value);
                    log.Add(
                        $"✨ {reward.StatusGranted} granted " +
                        $"to all party members.");
                }

                // Quest triggers
                if (reward.QuestIdCompleted.HasValue)
                {
                    foreach (var winner in winners)
                        winner.CompleteQuest(
                            reward.QuestIdCompleted.Value);
                    log.Add("📜 Quest step completed!");
                }

                if (reward.QuestIdGranted.HasValue)
                {
                    foreach (var winner in winners)
                        winner.AcceptQuest(
                            reward.QuestIdGranted.Value);
                    log.Add("📜 New quest discovered!");
                }

                // Lore reveal
                if (reward.LoreText != null)
                    log.Add($"📖 {reward.LoreText}");
            }

            encounter.Complete(EncounterOutcome.Victory);

            return new EncounterResult
            {
                Success     = true,
                Outcome     = EncounterOutcome.Victory,
                Message     = "The battle is won!",
                Reward      = encounter.VictoryReward,
                EventLog    = log,
                IsComplete  = true,
                SoundToPlay = SoundEffect.LevelUp
            };
        }

        public EncounterResult ResolveCombatDefeat(
            Encounter encounter,
            List<PlayerCharacter> losers)
        {
            var log = new List<string>();
            log.Add("💀 Defeat!");

            foreach (var loser in losers)
            {
                loser.TotalDeaths++;
                if (!loser.Stats.IsAlive)
                {
                    loser.ApplyStatus(CharacterStatus.Dead);
                    log.Add(
                        $"{loser.Name} has fallen in battle!");
                }
                _characterService.UpdateCharacter(loser);
            }

            encounter.Complete(EncounterOutcome.Defeat);

            return new EncounterResult
            {
                Success     = false,
                Outcome     = EncounterOutcome.Defeat,
                Message     =
                    "Your party has been defeated. " +
                    "The world grows dark...",
                Reward      = encounter.DefeatReward,
                EventLog    = log,
                IsComplete  = true,
                SoundToPlay = SoundEffect.CombatDeath
            };
        }

        // =====================
        // Flee Combat
        // =====================

        public EncounterResult AttemptFlee(
            Encounter encounter,
            PlayerCharacter player)
        {
            var log = new List<string>();

            if (!encounter.CanFlee)
                return new EncounterResult
                {
                    Success  = false,
                    Message  = "There is no escape!",
                    EventLog = new() { "🚫 You cannot flee this fight!" },
                    IsComplete = false
                };

            // Dexterity check vs flee chance
            var fleeRoll = _diceService.Roll(
                DiceType.D20, 1,
                player.Stats.DexterityModifier);

            var fleeTarget = 20 - (encounter.FleeSuccessChance / 5);

            log.Add(
                $"🏃 Flee attempt: rolled {fleeRoll.Total} " +
                $"(need {fleeTarget}+)");

            if (fleeRoll.Total >= fleeTarget)
            {
                // Flee successful but take opportunity attack
                var opportunityDamage = _diceService
                    .Roll(DiceType.D4, 1, 0);
                player.Stats.ApplyDamage(opportunityDamage.Total);
                log.Add(
                    $"You escape but take {opportunityDamage.Total} " +
                    $"damage fleeing!");

                encounter.Complete(EncounterOutcome.Fled);

                return new EncounterResult
                {
                    Success    = true,
                    Outcome    = EncounterOutcome.Fled,
                    Message    = "You flee the battle!",
                    EventLog   = log,
                    IsComplete = true,
                    SoundToPlay = SoundEffect.CombatMiss
                };
            }

            log.Add("Failed to flee! Enemies block your escape.");

            return new EncounterResult
            {
                Success    = false,
                Outcome    = EncounterOutcome.Pending,
                Message    = "You failed to escape!",
                EventLog   = log,
                IsComplete = false
            };
        }

        // =====================
        // Trap Encounter
        // =====================

        private EncounterResult TriggerTrapEncounter(
            Encounter encounter,
            PlayerCharacter player)
        {
            var log = new List<string>();

            if (encounter.TrapIsDisarmed)
                return new EncounterResult
                {
                    Success    = true,
                    Message    = "The trap has already been disarmed.",
                    IsComplete = true
                };

            // Perception check to spot trap
            var perceptionRoll = _diceService.Roll(
                DiceType.D20, 1,
                player.Stats.WisdomModifier);

            log.Add(
                $"👁️ Perception check: {perceptionRoll.Total} " +
                $"vs DC {encounter.TrapDifficultyClass}");

            if (perceptionRoll.Total >= encounter.TrapDifficultyClass)
            {
                encounter.TrapIsSpotted = true;
                log.Add("⚠️ You spot a trap!");

                // Attempt to disarm — Dexterity check
                var disarmRoll = _diceService.Roll(
                    DiceType.D20, 1,
                    player.Stats.DexterityModifier);

                log.Add(
                    $"🔧 Disarm attempt: {disarmRoll.Total} " +
                    $"vs DC {encounter.TrapDifficultyClass}");

                if (disarmRoll.Total >= encounter.TrapDifficultyClass)
                {
                    encounter.TrapIsDisarmed = true;
                    encounter.Complete(EncounterOutcome.Victory);
                    log.Add("✅ Trap disarmed successfully!");

                    return new EncounterResult
                    {
                        Success    = true,
                        Outcome    = EncounterOutcome.Victory,
                        Message    = "You disarm the trap!",
                        EventLog   = log,
                        IsComplete = true,
                        SoundToPlay = SoundEffect.ChestOpen
                    };
                }

                log.Add("❌ Disarm failed! Trap triggered!");
            }
            else
            {
                log.Add("💥 You walk straight into the trap!");
            }

            // Trap triggers — saving throw
            return ResolveTrapDamage(encounter, player, log);
        }

        private EncounterResult ResolveTrapDamage(
            Encounter encounter,
            PlayerCharacter player,
            List<string> log)
        {
            if (!encounter.TrapDamageDie.HasValue)
            {
                encounter.Complete(EncounterOutcome.Defeat);
                return new EncounterResult
                {
                    Success    = false,
                    Outcome    = EncounterOutcome.Defeat,
                    Message    = "The trap triggers!",
                    EventLog   = log,
                    IsComplete = true
                };
            }

            // Saving throw
            var saveStat = encounter.TrapSaveUsing
                ?? CharacterStat.Dexterity;
            var saveModifier = player.Stats
                .GetModifier(saveStat);
            var saveRoll = _diceService.Roll(
                DiceType.D20, 1, saveModifier);

            log.Add(
                $"🎲 {saveStat} save: {saveRoll.Total} " +
                $"vs DC {encounter.TrapDifficultyClass}");

            var damageRoll = _diceService.Roll(
                encounter.TrapDamageDie.Value,
                1,
                encounter.TrapDamageBonus);

            // Successful save = half damage
            var damage = saveRoll.Total >= encounter.TrapDifficultyClass
                ? damageRoll.Total / 2
                : damageRoll.Total;

            player.Stats.ApplyDamage(damage);
            log.Add(
                $"💥 {damage} damage taken " +
                $"{(saveRoll.Total >= encounter.TrapDifficultyClass ? "(saved for half)" : "")}");

            // Apply trap status
            if (encounter.TrapAppliesStatus.HasValue &&
                saveRoll.Total < encounter.TrapDifficultyClass)
            {
                player.ApplyStatus(encounter.TrapAppliesStatus.Value);
                log.Add(
                    $"😵 {player.Name} is afflicted with " +
                    $"{encounter.TrapAppliesStatus}!");
            }

            _characterService.UpdateCharacter(player);
            encounter.Complete(EncounterOutcome.Defeat);

            return new EncounterResult
            {
                Success    = false,
                Outcome    = EncounterOutcome.Defeat,
                Message    = $"The trap deals {damage} damage!",
                EventLog   = log,
                IsComplete = true,
                SoundToPlay = SoundEffect.CombatHit
            };
        }

        // =====================
        // Treasure Encounter
        // =====================

        private EncounterResult TriggerTreasureEncounter(
            Encounter encounter,
            PlayerCharacter player)
        {
            var log = new List<string>();

            if (encounter.TreasureIsLooted)
                return new EncounterResult
                {
                    Success    = true,
                    Message    = "This chest is already empty.",
                    IsComplete = true
                };

            // Locked chest
            if (encounter.TreasureIsLocked)
            {
                var lockpickRoll = _diceService.Roll(
                    DiceType.D20, 1,
                    player.Stats.DexterityModifier);

                log.Add(
                    $"🔒 Lockpicking: {lockpickRoll.Total} " +
                    $"vs DC {encounter.TreasureLockDifficulty}");

                if (lockpickRoll.Total < encounter.TreasureLockDifficulty)
                {
                    log.Add("❌ You cannot open the lock.");
                    return new EncounterResult
                    {
                        Success    = false,
                        Message    = "The chest remains locked.",
                        EventLog   = log,
                        IsComplete = false
                    };
                }

                log.Add("✅ Lock picked!");
            }

            // Loot the chest
            log.Add("💎 You open the chest!");

            if (encounter.TreasureGold > 0 ||
                encounter.TreasureSilver > 0 ||
                encounter.TreasureCopper > 0)
            {
                player.AddCurrency(
                    encounter.TreasureGold,
                    encounter.TreasureSilver,
                    encounter.TreasureCopper);
                log.Add(
                    $"💰 Found: {encounter.TreasureGold}g " +
                    $"{encounter.TreasureSilver}s " +
                    $"{encounter.TreasureCopper}c");
            }

            foreach (var item in encounter.TreasureItems)
            {
                var added = player.AddItem(item);
                log.Add(added
                    ? $"🎁 Found: {item.Name} ({item.Rarity})"
                    : $"⚠️ {item.Name} left behind — too heavy.");
            }

            encounter.TreasureIsLooted = true;
            encounter.Complete(EncounterOutcome.Victory);
            _characterService.UpdateCharacter(player);

            return new EncounterResult
            {
                Success    = true,
                Outcome    = EncounterOutcome.Victory,
                Message    = "Treasure looted!",
                EventLog   = log,
                IsComplete = true,
                SoundToPlay = SoundEffect.ChestOpen
            };
        }

        // =====================
        // Choice Encounter
        // =====================

        private EncounterResult TriggerChoiceEncounter(
            Encounter encounter,
            PlayerCharacter player)
        {
            var availableChoices = encounter
                .GetAvailableChoices(player);

            return new EncounterResult
            {
                Success    = true,
                Outcome    = EncounterOutcome.Pending,
                Message    = encounter.SetupText
                    ?? encounter.Description,
                EventLog   = availableChoices
                    .Select(c => $"▶ {c.Text} " +
                        $"{c.AvailabilityDisplay(player)}")
                    .ToList(),
                IsComplete = false
            };
        }

        public EncounterResult ResolveChoice(
            Encounter encounter,
            PlayerCharacter player,
            Guid choiceId)
        {
            var choice = encounter.Choices
                .FirstOrDefault(c => c.Id == choiceId);

            if (choice == null)
                return new EncounterResult
                {
                    Success  = false,
                    Message  = "Invalid choice.",
                    IsComplete = false
                };

            if (!choice.IsAvailableTo(player))
                return new EncounterResult
                {
                    Success  = false,
                    Message  = "You do not meet the requirements " +
                               "for that choice.",
                    IsComplete = false
                };

            encounter.ChoiceMade = choice;
            encounter.Complete(choice.Outcome);

            // Apply rewards
            if (choice.Reward != null)
            {
                player.AddExperience(
                    choice.Reward.ExperiencePoints);
                player.AddCurrency(
                    choice.Reward.GoldReward,
                    choice.Reward.SilverReward,
                    choice.Reward.CopperReward);

                if (choice.Reward.StatusGranted.HasValue)
                    player.ApplyStatus(
                        choice.Reward.StatusGranted.Value);

                if (choice.Reward.StatusRemoved.HasValue)
                    player.RemoveStatus(
                        choice.Reward.StatusRemoved.Value);

                if (choice.Reward.QuestIdGranted.HasValue)
                    player.AcceptQuest(
                        choice.Reward.QuestIdGranted.Value);

                _characterService.UpdateCharacter(player);
            }

            return new EncounterResult
            {
                Success    = true,
                Outcome    = choice.Outcome,
                Message    = choice.OutcomeText
                    ?? "Choice made.",
                Reward     = choice.Reward,
                EventLog   = new()
                {
                    $"▶ {choice.Text}",
                    choice.OutcomeText ?? string.Empty
                },
                IsComplete = true
            };
        }

        // =====================
        // Random Event
        // (L.O.R.D inspired)
        // =====================

        private EncounterResult TriggerRandomEvent(
            Encounter encounter,
            PlayerCharacter player,
            Party? party)
        {
            var log = new List<string>();
            var roll = _diceService.Roll(DiceType.D100);

            log.Add($"🎲 Random event: {encounter.Name}");
            log.Add(encounter.RandomEventText
                ?? "Something unexpected happens...");

            // Positive events on high rolls
            var isPositive = encounter.HasPositiveOutcome
                ? roll.Total > 50
                : roll.Total > 80;

            if (isPositive && encounter.VictoryReward != null)
            {
                player.AddExperience(
                    encounter.VictoryReward.ExperiencePoints);
                player.AddCurrency(
                    encounter.VictoryReward.GoldReward,
                    encounter.VictoryReward.SilverReward,
                    encounter.VictoryReward.CopperReward);

                log.Add($"✨ {encounter.VictoryReward.RewardSummary}");
                _characterService.UpdateCharacter(player);

                encounter.Complete(EncounterOutcome.Victory);
                return new EncounterResult
                {
                    Success    = true,
                    Outcome    = EncounterOutcome.Victory,
                    Message    = "Fortune smiles upon you!",
                    Reward     = encounter.VictoryReward,
                    EventLog   = log,
                    IsComplete = true,
                    SoundToPlay = SoundEffect.GoldPickup
                };
            }

            log.Add("The moment passes without reward.");
            encounter.Complete(EncounterOutcome.Ignored);

            return new EncounterResult
            {
                Success    = true,
                Outcome    = EncounterOutcome.Ignored,
                Message    = "Nothing comes of it.",
                EventLog   = log,
                IsComplete = true
            };
        }

        // =====================
        // Shrine Encounter
        // =====================

        private EncounterResult TriggerShrine(
            Encounter encounter,
            PlayerCharacter player)
        {
            var log = new List<string>();
            log.Add($"✨ You approach the shrine of {encounter.Name}");

            // Roll for shrine blessing
            var shrineRoll = _diceService.Roll(
                DiceType.D20, 1,
                player.Stats.WisdomModifier);

            log.Add($"🙏 Wisdom check: {shrineRoll.Total}");

            if (shrineRoll.Total >= 15)
            {
                // Great blessing
                player.Stats.Luck += 5;
                player.Stats.ApplyHealing(
                    player.Stats.MaxHitPoints / 4);
                player.ApplyStatus(CharacterStatus.Blessed);
                log.Add(
                    "✨ The shrine blesses you greatly! " +
                    "+5 Luck, healed and Blessed!");
            }
            else if (shrineRoll.Total >= 10)
            {
                // Minor blessing
                player.Stats.ApplyHealing(
                    player.Stats.MaxHitPoints / 8);
                log.Add(
                    "✨ The shrine offers a minor blessing. " +
                    "Small amount of HP restored.");
            }
            else
            {
                // Nothing or curse
                log.Add(
                    "The shrine remains silent. " +
                    "Perhaps you are unworthy today.");
            }

            encounter.Complete(EncounterOutcome.Victory);
            _characterService.UpdateCharacter(player);

            return new EncounterResult
            {
                Success    = true,
                Outcome    = EncounterOutcome.Victory,
                Message    = "You commune with the shrine.",
                EventLog   = log,
                IsComplete = true,
                SoundToPlay = SoundEffect.BuffApplied
            };
        }

        // =====================
        // Lore Entry
        // =====================

        private static EncounterResult TriggerLoreEntry(
            Encounter encounter)
        {
            encounter.Complete(EncounterOutcome.Victory);

            return new EncounterResult
            {
                Success    = true,
                Outcome    = EncounterOutcome.Victory,
                Message    = encounter.LoreTitle ?? "Ancient Text",
                EventLog   = new()
                {
                    $"📖 {encounter.LoreTitle}",
                    encounter.LoreContent ?? string.Empty
                },
                IsComplete = true
            };
        }
    }
}
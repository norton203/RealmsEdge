using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Items;
using RealmsEdge.Shared.Services;

namespace RealmsEdge.Shared.Models.Characters
{
    public enum NpcType
    {
        // Friendly
        Merchant,           // Buys and sells items
        QuestGiver,         // Gives and receives quests
        Trainer,            // Teaches skills for gold
        Innkeeper,          // Rest, rumours, tavern games
        Guard,              // Town protection, law enforcement
        Companion,          // Joins the party, fights alongside

        // Neutral
        Wanderer,           // Random encounter, may trade or fight
        Villager,           // Background NPC, flavour dialogue

        // Hostile
        Enemy,              // Standard enemy combatant
        Boss,               // Named enemy, special abilities
        Elite,              // Stronger than standard, mini boss
        Summoned,           // Called by another NPC during combat
    }

    public enum NpcBehaviour
    {
        Passive,            // Never attacks unless attacked first
        Defensive,          // Attacks if player gets too close
        Aggressive,         // Attacks on sight
        Cowardly,           // Flees when HP drops below 25%
        Patrolling,         // Follows a set path
        Guarding,           // Stays in fixed position
        Territorial,        // Aggressive within home area only
        Friendly,           // Always helpful, never attacks
    }

    public enum LootTable
    {
        None,               // Drops nothing
        Poor,               // Copper coins, basic items
        Common,             // Silver coins, common items
        Uncommon,           // Mix of common and uncommon items
        Rare,               // Chance of rare items
        Elite,              // Good chance of rare, small epic chance
        Boss,               // Guaranteed rare, chance of epic/legendary
        Legendary           // Legendary loot table
    }

    public class NpcCharacter : CharacterBase
    {
        // =====================
        // NPC Identity
        // =====================

        public NpcType NpcType { get; set; }
        public NpcBehaviour Behaviour { get; set; }
        public string? Greeting { get; set; }           // First time dialogue
        public string? IdleDialogue { get; set; }       // Repeated visits
        public string? CombatTaunt { get; set; }        // Said when entering combat
        public string? DeathDialogue { get; set; }      // Said on death
        public string? FleeDialogue { get; set; }       // Said when fleeing

        // =====================
        // Combat AI
        // =====================

        public int AggroRange { get; set; } = 3;        // Tiles before NPC notices player
        public int ChaseRange { get; set; } = 8;        // Tiles before NPC gives up chase
        public int FleeHealthPercent { get; set; } = 0; // HP% to trigger flee (0 = never)
        public bool CanCallForHelp { get; set; } = false;
        public bool IsElite { get; set; } = false;
        public bool IsBoss { get; set; } = false;

        // Special ability names this NPC can use
        public List<string> Abilities { get; set; } = new();

        // =====================
        // Faction & Reputation
        // =====================

        public string? FactionName { get; set; }        // e.g. "Town Guard", "Thieves Guild"
        public int FactionId { get; set; } = 0;         // 0 = no faction
        public int ReputationRequired { get; set; } = 0; // Min reputation to interact

        // =====================
        // Merchant Data
        // =====================

        public bool IsMerchant => NpcType == NpcType.Merchant;
        public List<InventoryItem> ShopInventory { get; set; } = new();
        public int BuyMarkupPercent { get; set; } = 140;    // Sells at 140% base value
        public int SellMarkdownPercent { get; set; } = 60;  // Buys at 60% base value
        public bool RestocksInventory { get; set; } = true;
        public DateTime LastRestockTime { get; set; } = DateTime.UtcNow;
        public int RestockIntervalHours { get; set; } = 24;

        public int GetBuyPrice(InventoryItem item, int playerCharismaModifier)
        {
            var basePrice = item.BaseValueInCopper * BuyMarkupPercent / 100;
            var discount = basePrice * (playerCharismaModifier * 2) / 100;
            return Math.Max(1, basePrice - discount);
        }

        public int GetSellPrice(InventoryItem item, int playerCharismaModifier)
        {
            var basePrice = item.BaseValueInCopper * SellMarkdownPercent / 100;
            var bonus = basePrice * (playerCharismaModifier * 2) / 100;
            return Math.Max(1, basePrice + bonus);
        }

        // =====================
        // Trainer Data
        // =====================

        public bool IsTrainer => NpcType == NpcType.Trainer;
        public List<string> TeachableSkills { get; set; } = new();
        public int TrainingCostPerLevel { get; set; } = 100;    // In copper

        public bool CanTeach(string skillName)
            => TeachableSkills.Contains(skillName);

        public int GetTrainingCost(int currentSkillLevel)
            => TrainingCostPerLevel * (currentSkillLevel + 1);

        // =====================
        // Quest Giver Data
        // =====================

        public bool IsQuestGiver => NpcType == NpcType.QuestGiver;
        public List<Guid> AvailableQuestIds { get; set; } = new();
        public List<Guid> CompletedQuestIds { get; set; } = new();

        // =====================
        // Companion Data
        // =====================

        public bool IsCompanion => NpcType == NpcType.Companion;
        public bool IsHired { get; set; } = false;
        public int HireCostPerDay { get; set; } = 0;            // In copper, 0 = free
        public int LoyaltyLevel { get; set; } = 50;             // 0-100
        public Guid? HiredByPlayerId { get; set; }

        public bool CanAffordToHire(PlayerCharacter player)
            => player.CanAfford(HireCostPerDay);

        public void IncreaseLoyalty(int amount)
            => LoyaltyLevel = Math.Min(100, LoyaltyLevel + amount);

        public void DecreaseLoyalty(int amount)
            => LoyaltyLevel = Math.Max(0, LoyaltyLevel - amount);

        public bool WillFlee => LoyaltyLevel < 20;

        // =====================
        // Loot
        // =====================

        public LootTable LootTable { get; set; } = LootTable.Common;
        public int ExperienceReward { get; set; } = 0;
        public int GoldReward { get; set; } = 0;
        public List<InventoryItem> GuaranteedDrops { get; set; } = new();

        // =====================
        // Respawn
        // =====================

        public bool CanRespawn { get; set; } = true;
        public int RespawnTimeMinutes { get; set; } = 60;
        public DateTime? LastKilledAt { get; set; }

        public bool IsReadyToRespawn => CanRespawn
            && LastKilledAt.HasValue
            && DateTime.UtcNow >= LastKilledAt.Value.AddMinutes(RespawnTimeMinutes);

        // =====================
        // Alignment Reaction
        // =====================

        // How this NPC reacts to different player alignments
        public string GetReactionTo(Alignment playerAlignment) => NpcType switch
        {
            NpcType.Guard => playerAlignment switch
            {
                Alignment.LawfulGood    or
                Alignment.LawfulNeutral => "Respectful",
                Alignment.ChaoticEvil => "Hostile",
                _ => "Neutral"
            },
            NpcType.Merchant => playerAlignment switch
            {
                Alignment.ChaoticEvil => "Suspicious",
                Alignment.LawfulGood => "Welcoming",
                _ => "Neutral"
            },
            _ => "Neutral"
        };

        // =====================
        // Factory Methods
        // =====================

        public static NpcCharacter CreateEnemy(
            string name,
            CharacterRace race,
            CharacterClass charClass,
            int level,
            NpcBehaviour behaviour,
            LootTable lootTable,
            DiceService diceService)
        {
            var npc = new NpcCharacter
            {
                Name        = name,
                Race        = race,
                Class       = charClass,
                Level       = level,
                NpcType     = NpcType.Enemy,
                Behaviour   = behaviour,
                LootTable   = lootTable,
                Alignment   = Alignment.ChaoticEvil,
                Gender      = Gender.None,
            };

            // Scale stats with level
            npc.Stats.Strength      = 8 + (level * 2);
            npc.Stats.Dexterity     = 8 + level;
            npc.Stats.Constitution  = 8 + (level * 2);
            npc.Stats.Intelligence  = 6 + level;
            npc.Stats.Wisdom        = 6 + level;
            npc.Stats.Charisma      = 4;

            npc.ApplyRaceBonuses();

            // Roll HP using level and constitution
            var hpRoll = diceService.Roll(DiceType.D8, level, npc.Stats.ConstitutionModifier);
            npc.Stats.MaxHitPoints      = Math.Max(1, hpRoll.Total);
            npc.Stats.CurrentHitPoints  = npc.Stats.MaxHitPoints;

            // XP reward scales with level
            npc.ExperienceReward = level * 100;
            npc.GoldReward       = diceService.Roll(DiceType.D6, level).Total;

            return npc;
        }

        public static NpcCharacter CreateMerchant(
            string name,
            string greeting,
            List<InventoryItem> stock)
        {
            return new NpcCharacter
            {
                Name            = name,
                NpcType         = NpcType.Merchant,
                Behaviour       = NpcBehaviour.Friendly,
                Greeting        = greeting,
                ShopInventory   = stock,
                Alignment       = Alignment.TrueNeutral,
                Gender          = Gender.Unspecified,
                Stats           = { Strength = 8, Dexterity = 10,
                                    Constitution = 10, Intelligence = 12,
                                    Wisdom = 12, Charisma = 14 }
            };
        }

        public static NpcCharacter CreateCompanion(
            string name,
            CharacterRace race,
            CharacterClass charClass,
            int level,
            int hireCostPerDay,
            DiceService diceService)
        {
            var companion = CreateEnemy(
                name, race, charClass, level,
                NpcBehaviour.Friendly,
                LootTable.None,
                diceService);

            companion.NpcType           = NpcType.Companion;
            companion.Behaviour         = NpcBehaviour.Friendly;
            companion.IsHired           = false;
            companion.HireCostPerDay    = hireCostPerDay;
            companion.LoyaltyLevel      = 50;
            companion.Alignment         = Alignment.TrueNeutral;
            companion.CanRespawn        = false;

            return companion;
        }
    }
}
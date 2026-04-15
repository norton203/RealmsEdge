using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Shared.Models.Party
{
    public enum PartyStatus
    {
        Forming,            // Party being assembled, not yet adventuring
        Adventuring,        // Active, in the world
        InCombat,           // Currently in a fight
        Resting,            // Taking a rest, recovering resources
        InTown,             // Safe zone, can trade and quest
        Disbanded           // Party no longer exists
    }

    public enum LootDistribution
    {
        FreeForAll,         // First to grab it keeps it
        RoundRobin,         // Each drop goes to next member in rotation
        LeaderDecides,      // Party leader assigns all loot
        NeedBeforeGreed,    // Roll Need or Greed on each item
        EqualSplit          // All gold and sellable items split equally
    }

    public enum XpDistribution
    {
        Equal,              // All members get full XP
        Scaled,             // XP scaled by level difference
        LeaderTakesAll,     // Only leader gets XP (unusual but valid)
        ProRata             // XP split proportionally by damage dealt
    }

    public class PartyMember
    {
        public PlayerCharacter Character { get; set; } = null!;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsReady { get; set; } = false;
        public bool IsOnline { get; set; } = false;
        public string? StatusMessage { get; set; }

        // Combat role chosen by player
        public string Role { get; set; } = "DPS";      // Tank, Healer, DPS, Support

        // Loot rotation tracker
        public int LootRollPosition { get; set; } = 0;
        public DateTime LastLootReceived { get; set; } = DateTime.MinValue;
    }

    public class Party
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Motto { get; set; }              // Party motto, shown in UI
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public PartyStatus Status { get; set; } = PartyStatus.Forming;

        // =====================
        // Members
        // =====================

        public List<PartyMember> Members { get; set; } = new();
        public Guid LeaderId { get; set; }
        public int MaxMembers { get; set; } = 6;        // Classic D&D party size

        public bool IsFull => Members.Count >= MaxMembers;
        public bool IsEmpty => Members.Count == 0;
        public int MemberCount => Members.Count;

        public PartyMember? Leader => Members
            .FirstOrDefault(m => m.Character.Id == LeaderId);

        public PartyMember? GetMember(Guid characterId) => Members
            .FirstOrDefault(m => m.Character.Id == characterId);

        public bool HasMember(Guid characterId) => Members
            .Any(m => m.Character.Id == characterId);

        public bool AllMembersReady => Members.Any() &&
            Members.All(m => m.IsReady);

        public bool AllMembersOnline => Members.Any() &&
            Members.All(m => m.IsOnline);

        public List<PartyMember> OnlineMembers => Members
            .Where(m => m.IsOnline).ToList();

        // =====================
        // Member Management
        // =====================

        public bool AddMember(PlayerCharacter character)
        {
            if (IsFull) return false;
            if (HasMember(character.Id)) return false;

            Members.Add(new PartyMember
            {
                Character       = character,
                LootRollPosition = Members.Count
            });

            character.PartyId = Id;
            return true;
        }

        public bool RemoveMember(Guid characterId)
        {
            var member = GetMember(characterId);
            if (member == null) return false;

            Members.Remove(member);
            member.Character.PartyId        = null;
            member.Character.IsPartyLeader  = false;

            // If leader left, promote next online member
            if (characterId == LeaderId && Members.Any())
                PromoteToLeader(Members.First().Character.Id);

            return true;
        }

        public bool PromoteToLeader(Guid characterId)
        {
            var member = GetMember(characterId);
            if (member == null) return false;

            // Demote current leader
            if (Leader != null)
                Leader.Character.IsPartyLeader = false;

            LeaderId = characterId;
            member.Character.IsPartyLeader = true;
            return true;
        }

        // =====================
        // Shared Inventory
        // =====================

        public List<InventoryItem> SharedInventory { get; set; } = new();
        public int SharedGold { get; set; } = 0;

        public void AddToSharedInventory(InventoryItem item)
            => SharedInventory.Add(item);

        public bool RemoveFromSharedInventory(Guid itemId)
        {
            var item = SharedInventory.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            SharedInventory.Remove(item);
            return true;
        }

        public void AddSharedGold(int amount)
            => SharedGold = Math.Max(0, SharedGold + amount);

        public bool SpendSharedGold(int amount)
        {
            if (SharedGold < amount) return false;
            SharedGold -= amount;
            return true;
        }

        // =====================
        // Loot & XP Settings
        // =====================

        public LootDistribution LootDistribution { get; set; }
            = LootDistribution.NeedBeforeGreed;

        public XpDistribution XpDistribution { get; set; }
            = XpDistribution.Equal;

        // Distribute XP to all living members
        public void DistributeXp(long totalXp)
        {
            var livingMembers = Members
                .Where(m => m.Character.Stats.IsAlive)
                .ToList();

            if (!livingMembers.Any()) return;

            switch (XpDistribution)
            {
                case XpDistribution.Equal:
                    foreach (var member in livingMembers)
                        member.Character.AddExperience(totalXp);
                    break;

                case XpDistribution.Scaled:
                    var avgLevel = livingMembers.Average(m => m.Character.Level);
                    foreach (var member in livingMembers)
                    {
                        var scaleFactor = avgLevel / member.Character.Level;
                        var scaledXp = (long)(totalXp * Math.Min(1.5, scaleFactor));
                        member.Character.AddExperience(scaledXp);
                    }
                    break;

                case XpDistribution.ProRata:
                    // Split evenly for now — damage tracking comes in combat service
                    var share = totalXp / livingMembers.Count;
                    foreach (var member in livingMembers)
                        member.Character.AddExperience(share);
                    break;

                case XpDistribution.LeaderTakesAll:
                    Leader?.Character.AddExperience(totalXp);
                    break;
            }
        }

        // Get next member in round robin loot rotation
        public PartyMember? GetNextLootRecipient()
        {
            if (!Members.Any()) return null;

            return Members
                .Where(m => m.Character.Stats.IsAlive && m.IsOnline)
                .OrderBy(m => m.LastLootReceived)
                .FirstOrDefault();
        }

        // =====================
        // Party Stats
        // =====================

        public int AverageLevel => Members.Any()
            ? (int)Members.Average(m => m.Character.Level)
            : 0;

        public int HighestLevel => Members.Any()
            ? Members.Max(m => m.Character.Level)
            : 0;

        public int LowestLevel => Members.Any()
            ? Members.Min(m => m.Character.Level)
            : 0;

        public int TotalCurrentHp => Members
            .Sum(m => m.Character.Stats.CurrentHitPoints);

        public int TotalMaxHp => Members
            .Sum(m => m.Character.Stats.MaxHitPoints);

        public bool HasHealer => Members.Any(m =>
            m.Character.Class == CharacterClass.Cleric  ||
            m.Character.Class == CharacterClass.Druid   ||
            m.Character.Class == CharacterClass.Shaman  ||
            m.Character.Class == CharacterClass.Paladin ||
            m.Character.Class == CharacterClass.Bard);

        public bool HasTank => Members.Any(m =>
            m.Character.Class == CharacterClass.Fighter    ||
            m.Character.Class == CharacterClass.Paladin    ||
            m.Character.Class == CharacterClass.Barbarian);

        public bool HasMagicUser => Members.Any(m =>
            m.Character.Class == CharacterClass.Mage       ||
            m.Character.Class == CharacterClass.Sorcerer   ||
            m.Character.Class == CharacterClass.Warlock    ||
            m.Character.Class == CharacterClass.Necromancer);

        // =====================
        // Party Chat Log
        // =====================

        public List<PartyChatMessage> ChatLog { get; set; } = new();
        public int MaxChatHistory { get; set; } = 100;

        public void AddChatMessage(Guid senderId, string message)
        {
            var sender = GetMember(senderId);
            if (sender == null) return;

            ChatLog.Add(new PartyChatMessage
            {
                SenderName  = sender.Character.Name,
                Message     = message,
                SentAt      = DateTime.UtcNow
            });

            // Trim chat history
            if (ChatLog.Count > MaxChatHistory)
                ChatLog.RemoveAt(0);
        }

        // =====================
        // Display Helpers
        // =====================

        public string StatusDisplay => Status switch
        {
            PartyStatus.Forming => "⚔️ Forming",
            PartyStatus.Adventuring => "🗺️ Adventuring",
            PartyStatus.InCombat => "💀 In Combat",
            PartyStatus.Resting => "🏕️ Resting",
            PartyStatus.InTown => "🏰 In Town",
            PartyStatus.Disbanded => "❌ Disbanded",
            _ => "Unknown"
        };

        public string CompositionWarning
        {
            get
            {
                if (!HasHealer) return "⚠️ No healer in party!";
                if (!HasTank) return "⚠️ No tank in party!";
                return string.Empty;
            }
        }
    }

    public class PartyChatMessage
    {
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public string Display => $"[{SentAt:HH:mm}] {SenderName}: {Message}";
    }
}
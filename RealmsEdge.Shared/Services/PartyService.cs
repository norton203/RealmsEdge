using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;
using RealmsEdge.Shared.Models.Party;

namespace RealmsEdge.Shared.Services
{
    public class PartyService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly DiceService _diceService;
        private readonly CharacterService _characterService;

        // In memory store for now
        // Will be replaced with database service later
        private readonly Dictionary<Guid, Party> _parties = new();

        public PartyService(
            DiceService diceService,
            CharacterService characterService)
        {
            _diceService      = diceService;
            _characterService = characterService;
        }

        // =====================
        // Party Creation
        // =====================

        public (bool Success, string Message, Party? Party) CreateParty(
            string partyName,
            Guid leaderCharacterId,
            LootDistribution lootDistribution = LootDistribution.NeedBeforeGreed,
            XpDistribution xpDistribution = XpDistribution.Equal)
        {
            var leader = _characterService.GetCharacter(leaderCharacterId);
            if (leader == null)
                return (false, "Character not found.", null);

            if (leader.PartyId.HasValue)
                return (false,
                    $"{leader.Name} is already in a party. " +
                    $"Leave your current party first.", null);

            if (string.IsNullOrWhiteSpace(partyName))
                return (false, "Party name cannot be empty.", null);

            if (partyName.Length > 32)
                return (false, "Party name cannot exceed 32 characters.", null);

            var party = new Party
            {
                Name             = partyName,
                LeaderId         = leaderCharacterId,
                LootDistribution = lootDistribution,
                XpDistribution   = xpDistribution,
                Status           = PartyStatus.Forming
            };

            // Add leader as first member
            party.AddMember(leader);
            leader.IsPartyLeader = true;

            _parties[party.Id] = party;

            return (true,
                $"Party '{partyName}' created. " +
                $"{leader.Name} is the party leader.", party);
        }

        // =====================
        // Party Retrieval
        // =====================

        public Party? GetParty(Guid partyId)
            => _parties.TryGetValue(partyId, out var party) ? party : null;

        public Party? GetCharacterParty(Guid characterId)
            => _parties.Values
                .FirstOrDefault(p => p.HasMember(characterId));

        public List<Party> GetAllParties()
            => _parties.Values.ToList();

        public List<Party> GetOpenParties()
            => _parties.Values
                .Where(p => !p.IsFull &&
                    p.Status == PartyStatus.Forming ||
                    p.Status == PartyStatus.InTown)
                .ToList();

        // =====================
        // Member Management
        // =====================

        public (bool Success, string Message) JoinParty(
            Guid partyId,
            Guid characterId)
        {
            var party = GetParty(partyId);
            var character = _characterService.GetCharacter(characterId);

            if (party == null)
                return (false, "Party not found.");

            if (character == null)
                return (false, "Character not found.");

            if (character.PartyId.HasValue)
                return (false,
                    $"{character.Name} is already in a party.");

            if (party.IsFull)
                return (false,
                    $"Party '{party.Name}' is full. " +
                    $"Maximum {party.MaxMembers} members.");

            if (party.Status == PartyStatus.InCombat)
                return (false,
                    "Cannot join a party that is currently in combat.");

            if (party.Status == PartyStatus.Disbanded)
                return (false, "This party has been disbanded.");

            party.AddMember(character);
            _characterService.UpdateCharacter(character);

            // Notify party chat
            party.AddChatMessage(party.LeaderId,
                $"{character.Name} has joined the party!");

            return (true,
                $"{character.Name} has joined '{party.Name}'.");
        }

        public (bool Success, string Message) LeaveParty(
            Guid partyId,
            Guid characterId)
        {
            var party = GetParty(partyId);
            var character = _characterService.GetCharacter(characterId);

            if (party == null)
                return (false, "Party not found.");

            if (character == null)
                return (false, "Character not found.");

            if (!party.HasMember(characterId))
                return (false,
                    $"{character.Name} is not in this party.");

            if (party.Status == PartyStatus.InCombat)
                return (false,
                    "Cannot leave a party during combat.");

            var wasLeader = party.LeaderId == characterId;
            party.RemoveMember(characterId);
            _characterService.UpdateCharacter(character);

            // Disband if empty
            if (party.IsEmpty)
            {
                DisbandParty(partyId);
                return (true,
                    $"{character.Name} left the party. " +
                    $"Party disbanded as all members have left.");
            }

            // Notify party chat
            party.AddChatMessage(party.LeaderId,
                $"{character.Name} has left the party.");

            var message = wasLeader
                ? $"{character.Name} left. " +
                  $"{party.Leader?.Character.Name} is the new party leader."
                : $"{character.Name} has left '{party.Name}'.";

            return (true, message);
        }

        public (bool Success, string Message) KickMember(
            Guid partyId,
            Guid leaderCharacterId,
            Guid targetCharacterId)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            if (party.LeaderId != leaderCharacterId)
                return (false, "Only the party leader can kick members.");

            if (leaderCharacterId == targetCharacterId)
                return (false,
                    "You cannot kick yourself. Use leave party instead.");

            var target = _characterService.GetCharacter(targetCharacterId);
            if (target == null)
                return (false, "Target character not found.");

            if (!party.HasMember(targetCharacterId))
                return (false,
                    $"{target.Name} is not in this party.");

            party.RemoveMember(targetCharacterId);
            _characterService.UpdateCharacter(target);

            party.AddChatMessage(leaderCharacterId,
                $"{target.Name} has been removed from the party.");

            return (true,
                $"{target.Name} has been kicked from '{party.Name}'.");
        }

        public (bool Success, string Message) PromoteToLeader(
            Guid partyId,
            Guid currentLeaderId,
            Guid newLeaderId)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            if (party.LeaderId != currentLeaderId)
                return (false, "Only the current leader can promote members.");

            var newLeader = _characterService.GetCharacter(newLeaderId);
            if (newLeader == null)
                return (false, "Target character not found.");

            if (!party.HasMember(newLeaderId))
                return (false,
                    $"{newLeader.Name} is not in this party.");

            party.PromoteToLeader(newLeaderId);

            party.AddChatMessage(newLeaderId,
                $"{newLeader.Name} is now the party leader.");

            return (true,
                $"{newLeader.Name} has been promoted to party leader.");
        }

        // =====================
        // Party Status
        // =====================

        public (bool Success, string Message) SetPartyStatus(
            Guid partyId,
            Guid leaderCharacterId,
            PartyStatus newStatus)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            if (party.LeaderId != leaderCharacterId)
                return (false, "Only the party leader can change party status.");

            // Cannot manually set InCombat — combat system sets this
            if (newStatus == PartyStatus.InCombat)
                return (false,
                    "Combat status is set automatically by the game engine.");

            party.Status = newStatus;

            return (true,
                $"Party status changed to {newStatus}.");
        }

        public void SetPartyCombatStatus(Guid partyId, bool inCombat)
        {
            var party = GetParty(partyId);
            if (party == null) return;

            party.Status = inCombat
                ? PartyStatus.InCombat
                : PartyStatus.Adventuring;
        }

        // =====================
        // Ready Check
        // =====================

        public (bool Success, string Message) SetMemberReady(
            Guid partyId,
            Guid characterId,
            bool isReady)
        {
            var party = GetParty(partyId);
            var member = party?.GetMember(characterId);

            if (party == null) return (false, "Party not found.");
            if (member == null) return (false, "Member not found in party.");

            member.IsReady = isReady;

            var readyCount = party.Members.Count(m => m.IsReady);
            var totalCount = party.MemberCount;

            if (party.AllMembersReady)
            {
                party.AddChatMessage(characterId,
                    "All members are ready! The adventure begins!");
                party.Status = PartyStatus.Adventuring;
            }

            return (true,
                $"{readyCount}/{totalCount} members ready.");
        }

        // =====================
        // Party Rest
        // =====================

        public List<string> PartyShortRest(Guid partyId)
        {
            var party = GetParty(partyId);
            var results = new List<string>();

            if (party == null)
            {
                results.Add("Party not found.");
                return results;
            }

            if (party.Status == PartyStatus.InCombat)
            {
                results.Add("Cannot rest during combat!");
                return results;
            }

            party.Status = PartyStatus.Resting;

            foreach (var member in party.Members.Where(m => m.IsOnline))
            {
                var result = _characterService.ShortRest(member.Character.Id);
                results.Add(result);
            }

            party.Status = PartyStatus.Adventuring;
            return results;
        }

        public List<string> PartyLongRest(Guid partyId)
        {
            var party = GetParty(partyId);
            var results = new List<string>();

            if (party == null)
            {
                results.Add("Party not found.");
                return results;
            }

            if (party.Status == PartyStatus.InCombat)
            {
                results.Add("Cannot rest during combat!");
                return results;
            }

            party.Status = PartyStatus.Resting;

            foreach (var member in party.Members.Where(m => m.IsOnline))
            {
                var result = _characterService.LongRest(member.Character.Id);
                results.Add(result);
            }

            party.Status = PartyStatus.InTown;
            return results;
        }

        // =====================
        // Loot Distribution
        // =====================

        public (bool Success, string Message) DistributeLootItem(
            Guid partyId,
            InventoryItem item,
            Guid? assignToCharacterId = null)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            switch (party.LootDistribution)
            {
                case LootDistribution.LeaderDecides:
                    if (!assignToCharacterId.HasValue)
                        return (false,
                            "Leader must assign loot to a specific member.");

                    return AssignItemToMember(party,
                        assignToCharacterId.Value, item);

                case LootDistribution.RoundRobin:
                    var nextMember = party.GetNextLootRecipient();
                    if (nextMember == null)
                        return (false, "No eligible members to receive loot.");

                    nextMember.LastLootReceived = DateTime.UtcNow;
                    return AssignItemToMember(party,
                        nextMember.Character.Id, item);

                case LootDistribution.FreeForAll:
                    party.AddToSharedInventory(item);
                    return (true,
                        $"{item.Name} added to shared inventory. " +
                        $"First come first served!");

                case LootDistribution.EqualSplit:
                    // Sell value split equally — gold added to each member
                    var splitValue = item.SellValueInCopper / party.MemberCount;
                    foreach (var member in party.Members
                        .Where(m => m.Character.Stats.IsAlive))
                    {
                        member.Character.AddCurrency(0, 0, splitValue);
                        _characterService.UpdateCharacter(member.Character);
                    }
                    return (true,
                        $"{item.Name} sold and split equally. " +
                        $"Each member receives {splitValue}c.");

                case LootDistribution.NeedBeforeGreed:
                    // Add to shared — players roll need or greed separately
                    party.AddToSharedInventory(item);
                    return (true,
                        $"{item.Name} added to shared inventory. " +
                        $"Roll Need or Greed!");

                default:
                    party.AddToSharedInventory(item);
                    return (true, $"{item.Name} added to shared inventory.");
            }
        }

        public List<string> DistributeXpToParty(
            Guid partyId,
            long totalXp,
            string reason = "")
        {
            var party = GetParty(partyId);
            var results = new List<string>();

            if (party == null)
            {
                results.Add("Party not found.");
                return results;
            }

            party.DistributeXp(totalXp);

            // Check for level ups
            foreach (var member in party.Members
                .Where(m => m.Character.Stats.IsAlive))
            {
                _characterService.UpdateCharacter(member.Character);

                if (member.Character.CanLevelUp)
                {
                    var levelResult = _characterService
                        .LevelUpCharacter(member.Character.Id);
                    if (levelResult.Success)
                        results.Add(levelResult.Message);
                }
                else
                {
                    results.Add(
                        $"{member.Character.Name} gains {totalXp:N0} XP. " +
                        $"({member.Character.ExperiencePoints:N0}/" +
                        $"{member.Character.ExperienceToNextLevel:N0})");
                }
            }

            return results;
        }

        // =====================
        // Party Chat
        // =====================

        public (bool Success, string Message) SendChatMessage(
            Guid partyId,
            Guid characterId,
            string message)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            if (!party.HasMember(characterId))
                return (false, "You are not in this party.");

            if (string.IsNullOrWhiteSpace(message))
                return (false, "Message cannot be empty.");

            if (message.Length > 256)
                return (false,
                    "Message cannot exceed 256 characters.");

            party.AddChatMessage(characterId, message);
            return (true, string.Empty);
        }

        public List<PartyChatMessage> GetChatHistory(Guid partyId)
        {
            var party = GetParty(partyId);
            return party?.ChatLog ?? new List<PartyChatMessage>();
        }

        // =====================
        // Party Settings
        // =====================

        public (bool Success, string Message) UpdatePartySettings(
            Guid partyId,
            Guid leaderCharacterId,
            string? newName = null,
            string? newMotto = null,
            LootDistribution? lootDistribution = null,
            XpDistribution? xpDistribution = null,
            int? maxMembers = null)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            if (party.LeaderId != leaderCharacterId)
                return (false,
                    "Only the party leader can change party settings.");

            if (newName != null)
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return (false, "Party name cannot be empty.");
                if (newName.Length > 32)
                    return (false,
                        "Party name cannot exceed 32 characters.");
                party.Name = newName;
            }

            if (newMotto != null)
                party.Motto = newMotto;

            if (lootDistribution.HasValue)
                party.LootDistribution = lootDistribution.Value;

            if (xpDistribution.HasValue)
                party.XpDistribution = xpDistribution.Value;

            if (maxMembers.HasValue)
            {
                if (maxMembers < party.MemberCount)
                    return (false,
                        "Cannot set max members below current member count.");
                if (maxMembers > 8)
                    return (false,
                        "Maximum party size is 8 members.");
                party.MaxMembers = maxMembers.Value;
            }

            return (true, "Party settings updated.");
        }

        // =====================
        // Disband
        // =====================

        public (bool Success, string Message) DisbandParty(Guid partyId)
        {
            var party = GetParty(partyId);
            if (party == null)
                return (false, "Party not found.");

            // Remove all members from party
            foreach (var member in party.Members.ToList())
            {
                member.Character.PartyId       = null;
                member.Character.IsPartyLeader = false;
                _characterService.UpdateCharacter(member.Character);
            }

            party.Status = PartyStatus.Disbanded;
            _parties.Remove(partyId);

            return (true, $"Party '{party.Name}' has been disbanded.");
        }

        // =====================
        // Party Summary
        // =====================

        public string GetPartySummary(Guid partyId)
        {
            var party = GetParty(partyId);
            if (party == null) return "Party not found.";

            var memberList = string.Join(", ",
                party.Members.Select(m =>
                    $"{m.Character.Name} (L{m.Character.Level} " +
                    $"{m.Character.Class})"));

            return $"Party: {party.Name} | " +
                   $"Status: {party.StatusDisplay} | " +
                   $"Members: {party.MemberCount}/{party.MaxMembers} | " +
                   $"Avg Level: {party.AverageLevel} | " +
                   $"{memberList}";
        }

        // =====================
        // Private Helpers
        // =====================

        private (bool Success, string Message) AssignItemToMember(
            Party party,
            Guid characterId,
            InventoryItem item)
        {
            var member = party.GetMember(characterId);
            if (member == null)
                return (false, "Member not found in party.");

            var result = _characterService
                .AddItemToCharacter(characterId, item);

            if (result.Success)
                party.AddChatMessage(characterId,
                    $"{member.Character.Name} receives {item.Name}!");

            return result;
        }
    }
}
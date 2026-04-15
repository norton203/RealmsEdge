using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Shared.Services
{
    public class CharacterService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly DiceService _diceService;
        private readonly CharacterValidationService _validationService;

        // In memory store for now
        // Will be replaced with database service later
        private readonly Dictionary<Guid, PlayerCharacter> _characters = new();

        public CharacterService(
            DiceService diceService,
            CharacterValidationService validationService)
        {
            _diceService       = diceService;
            _validationService = validationService;
        }

        // =====================
        // Character Creation
        // =====================

        public (ValidationResult Result, PlayerCharacter? Character) CreateCharacter(
            string playerName,
            string characterName,
            CharacterRace race,
            CharacterClass charClass,
            Alignment alignment,
            Gender gender,
            List<DiceRoll> statRolls)
        {
            // Validate before creating
            var statValues = statRolls.Select(r => r.Total).ToList();
            var validation = _validationService.ValidateNewCharacter(
                characterName, race, charClass, alignment, gender, statValues);

            if (!validation.IsValid)
                return (validation, null);

            // Create the character using our factory method
            var character = PlayerCharacter.Create(
                playerName,
                characterName,
                race,
                charClass,
                alignment,
                gender,
                statRolls,
                _diceService);

            // Store in memory
            _characters[character.Id] = character;

            return (validation, character);
        }

        // =====================
        // Character Retrieval
        // =====================

        public PlayerCharacter? GetCharacter(Guid characterId)
            => _characters.TryGetValue(characterId, out var character)
                ? character : null;

        public List<PlayerCharacter> GetAllCharacters()
            => _characters.Values.ToList();

        public List<PlayerCharacter> GetCharactersByPlayer(string playerName)
            => _characters.Values
                .Where(c => c.PlayerName
                    .Equals(playerName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.LastPlayedAt)
                .ToList();

        public bool CharacterExists(Guid characterId)
            => _characters.ContainsKey(characterId);

        // =====================
        // Character Updates
        // =====================

        public bool UpdateCharacter(PlayerCharacter character)
        {
            if (!CharacterExists(character.Id)) return false;
            character.LastPlayedAt = DateTime.UtcNow;
            _characters[character.Id] = character;
            return true;
        }

        public bool DeleteCharacter(Guid characterId)
            => _characters.Remove(characterId);

        // =====================
        // Stat Rolling
        // =====================

        // Roll a full set of stats for character creation
        public List<DiceRoll> RollCharacterStats()
            => _diceService.RollFullStatArray();

        // Roll stats multiple times, player picks best set
        // Classic D&D character creation option
        public List<List<DiceRoll>> RollMultipleStatSets(int numberOfSets = 3)
            => Enumerable.Range(0, numberOfSets)
                .Select(_ => _diceService.RollFullStatArray())
                .ToList();

        // Standard array — no rolling, classic fixed stats
        // 15, 14, 13, 12, 10, 8 — assign as desired
        public List<int> GetStandardStatArray()
            => new() { 15, 14, 13, 12, 10, 8 };

        // =====================
        // Level Up
        // =====================

        public (bool Success, string Message) LevelUpCharacter(Guid characterId)
        {
            var character = GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            if (!character.CanLevelUp)
                return (false,
                    $"{character.Name} needs " +
                    $"{character.ExperienceToNextLevel - character.ExperiencePoints:N0} " +
                    $"more XP to reach level {character.Level + 1}.");

            var oldLevel = character.Level;
            character.LevelUp(_diceService);
            UpdateCharacter(character);

            return (true,
                $"{character.Name} has reached level {character.Level}! " +
                $"HP increased to {character.Stats.MaxHitPoints}.");
        }

        // =====================
        // Experience
        // =====================

        public void AwardExperience(Guid characterId, long amount, string reason = "")
        {
            var character = GetCharacter(characterId);
            if (character == null) return;

            character.AddExperience(amount);
            UpdateCharacter(character);
        }

        public void AwardExperienceToMultiple(
            List<Guid> characterIds,
            long amount,
            string reason = "")
        {
            foreach (var id in characterIds)
                AwardExperience(id, amount, reason);
        }

        // =====================
        // Inventory Management
        // =====================

        public (bool Success, string Message) AddItemToCharacter(
            Guid characterId,
            InventoryItem item)
        {
            var character = GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            if (character.IsEncumbered)
                return (false,
                    $"{character.Name} is carrying too much. " +
                    $"Drop something first.");

            var added = character.AddItem(item);
            if (!added)
                return (false,
                    $"{item.Name} is too heavy. " +
                    $"{character.Name} cannot carry any more.");

            UpdateCharacter(character);
            return (true, $"{item.Name} added to inventory.");
        }

        public (bool Success, string Message) RemoveItemFromCharacter(
            Guid characterId,
            Guid itemId)
        {
            var character = GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            var item = character.Inventory.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
                return (false, "Item not found in inventory.");

            if (item.IsQuestItem)
                return (false,
                    $"{item.Name} is a quest item and cannot be removed.");

            character.RemoveItem(itemId);
            UpdateCharacter(character);
            return (true, $"{item.Name} removed from inventory.");
        }

        public (bool Success, string Message) EquipItemOnCharacter(
            Guid characterId,
            Guid itemId)
        {
            var character = GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            var item = character.Inventory.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
                return (false, "Item not found in inventory.");

            if (!item.CanBeUsedBy(character))
                return (false,
                    $"{character.Name} does not meet the requirements " +
                    $"to equip {item.Name}.");

            var equipped = character.EquipItem(item);
            if (!equipped)
                return (false, $"Could not equip {item.Name}.");

            UpdateCharacter(character);
            return (true, $"{item.Name} equipped successfully.");
        }

        // =====================
        // Currency Management
        // =====================

        public (bool Success, string Message) TransferCurrency(
            Guid fromCharacterId,
            Guid toCharacterId,
            int goldAmount,
            int silverAmount,
            int copperAmount)
        {
            var from = GetCharacter(fromCharacterId);
            var to = GetCharacter(toCharacterId);

            if (from == null || to == null)
                return (false, "One or both characters not found.");

            var totalCopper = (goldAmount * 100) + (silverAmount * 10) + copperAmount;

            if (!from.CanAfford(totalCopper))
                return (false,
                    $"{from.Name} cannot afford this transfer.");

            from.SpendCurrency(totalCopper);
            to.AddCurrency(goldAmount, silverAmount, copperAmount);

            UpdateCharacter(from);
            UpdateCharacter(to);

            return (true,
                $"Transferred {goldAmount}g {silverAmount}s {copperAmount}c " +
                $"from {from.Name} to {to.Name}.");
        }

        // =====================
        // Rest & Recovery
        // =====================

        public string ShortRest(Guid characterId)
        {
            var character = GetCharacter(characterId);
            if (character == null) return "Character not found.";

            if (!character.Stats.IsAlive)
                return $"{character.Name} is dead and cannot rest.";

            if (character.HasStatus(CharacterStatus.Bleeding))
                return $"{character.Name} is bleeding and must be stabilised first.";

            // Short rest — roll hit die and recover partial resources
            var hitDieRoll = _diceService.Roll(DiceType.D8, 1,
                character.Stats.ConstitutionModifier);
            var hpRecovered = Math.Max(1, hitDieRoll.Total);
            var manaRecovered = character.Stats.WisdomModifier * 2;
            var staminaRecovered = character.Stats.ConstitutionModifier * 3;

            character.Stats.RestorePartial(hpRecovered, manaRecovered, staminaRecovered);
            character.RemoveStatus(CharacterStatus.Poisoned);
            character.RemoveStatus(CharacterStatus.Stunned);

            UpdateCharacter(character);

            return $"{character.Name} takes a short rest and recovers " +
                   $"{hpRecovered} HP, {manaRecovered} Mana, " +
                   $"{staminaRecovered} Stamina.";
        }

        public string LongRest(Guid characterId)
        {
            var character = GetCharacter(characterId);
            if (character == null) return "Character not found.";

            if (!character.Stats.IsAlive)
                return $"{character.Name} is dead and cannot rest.";

            character.Stats.RestoreFull();
            character.ClearAllStatuses();
            character.LastPlayedAt = DateTime.UtcNow;

            UpdateCharacter(character);

            return $"{character.Name} takes a long rest and is fully restored.";
        }

        // =====================
        // Character Summary
        // =====================

        public string GetCharacterSummary(Guid characterId)
        {
            var c = GetCharacter(characterId);
            if (c == null) return "Character not found.";

            return $"{c.FullTitle} | {c.Race} {c.Class} | " +
                   $"Level {c.Level} | {c.Alignment} | " +
                   $"HP: {c.Stats.CurrentHitPoints}/{c.Stats.MaxHitPoints} | " +
                   $"XP: {c.ExperiencePoints:N0}/{c.ExperienceToNextLevel:N0} | " +
                   $"Gold: {c.Gold}g {c.Silver}s {c.Copper}c";
        }
    }
}
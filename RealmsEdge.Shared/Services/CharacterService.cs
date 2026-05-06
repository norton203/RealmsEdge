using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Interfaces;
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

        private readonly DiceService              _diceService;
        private readonly CharacterValidationService _validationService;
        private readonly IDatabaseService         _database;

        // ── Write-through cache ──────────────────────
        // Keeps the rest of the app fast (sync reads)
        // while all writes go straight to SQLite.

        private Dictionary<Guid, PlayerCharacter> _cache = new();
        private bool _cacheLoaded = false;

        public CharacterService(
            DiceService diceService,
            CharacterValidationService validationService,
            IDatabaseService database)
        {
            _diceService       = diceService;
            _validationService = validationService;
            _database          = database;
        }

        // =====================
        // Cache
        // =====================

        /// <summary>
        /// Loads all characters from SQLite into the cache.
        /// Called automatically on first access — no manual
        /// init required from the call site.
        /// </summary>
        private async Task EnsureCacheAsync()
        {
            if (_cacheLoaded) return;

            var characters = await _database.GetAllCharactersAsync();
            _cache = characters.ToDictionary(c => c.Id);
            _cacheLoaded = true;
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
            var statValues = statRolls.Select(r => r.Total).ToList();
            var validation = _validationService.ValidateNewCharacter(
                characterName, race, charClass,
                alignment, gender, statValues);

            if (!validation.IsValid)
                return (validation, null);

            var character = PlayerCharacter.Create(
                playerName, characterName,
                race, charClass,
                alignment, gender,
                statRolls, _diceService);

            // Write-through: cache + database
            _cache[character.Id] = character;
            _cacheLoaded = true;

            // Fire-and-forget is safe here — the cache
            // is the source of truth until the app restarts
            _ = _database.SaveCharacterAsync(character);

            return (validation, character);
        }

        // =====================
        // Character Retrieval
        // =====================

        public PlayerCharacter? GetCharacter(Guid characterId)
        {
            // Ensure cache is warm — blocks only on very first call
            if (!_cacheLoaded)
                EnsureCacheAsync().GetAwaiter().GetResult();

            return _cache.TryGetValue(characterId, out var c) ? c : null;
        }

        public List<PlayerCharacter> GetAllCharacters()
        {
            if (!_cacheLoaded)
                EnsureCacheAsync().GetAwaiter().GetResult();

            return _cache.Values.ToList();
        }
        
        public async Task<List<PlayerCharacter>> GetAllCharactersAsync()
        {
            await EnsureCacheAsync();
            return _cache.Values.ToList();
        }

        public List<PlayerCharacter> GetCharactersByPlayer(string playerName)
        {
            if (!_cacheLoaded)
                EnsureCacheAsync().GetAwaiter().GetResult();

            return _cache.Values
                .Where(c => c.PlayerName.Equals(
                    playerName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.LastPlayedAt)
                .ToList();
        }

        public bool CharacterExists(Guid characterId)
        {
            if (!_cacheLoaded)
                EnsureCacheAsync().GetAwaiter().GetResult();

            return _cache.ContainsKey(characterId);
        }

        // =====================
        // Character Updates
        // =====================

        public bool UpdateCharacter(PlayerCharacter character)
        {
            if (!CharacterExists(character.Id)) return false;

            character.LastPlayedAt = DateTime.UtcNow;
            _cache[character.Id]   = character;

            _ = _database.SaveCharacterAsync(character);
            return true;
        }

        public bool DeleteCharacter(Guid characterId)
        {
            if (!_cache.Remove(characterId)) return false;

            _ = _database.DeleteCharacterAsync(characterId);
            return true;
        }

        // =====================
        // Stat Rolling
        // =====================

        public List<DiceRoll> RollCharacterStats()
            => _diceService.RollFullStatArray();

        public List<List<DiceRoll>> RollMultipleStatSets(int numberOfSets = 3)
            => Enumerable.Range(0, numberOfSets)
                .Select(_ => _diceService.RollFullStatArray())
                .ToList();

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

            character.LevelUp(_diceService);
            UpdateCharacter(character);

            return (true,
                $"{character.Name} has reached level {character.Level}! " +
                $"HP increased to {character.Stats.MaxHitPoints}.");
        }

        // =====================
        // Experience
        // =====================

        public void AwardExperience(
            Guid characterId,
            long amount,
            string reason = "")
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

            var item = character.Inventory
                .FirstOrDefault(i => i.Id == itemId);
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

            var item = character.Inventory
                .FirstOrDefault(i => i.Id == itemId);
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
            var to   = GetCharacter(toCharacterId);

            if (from == null || to == null)
                return (false, "One or both characters not found.");

            var totalCopper =
                (goldAmount * 100) +
                (silverAmount * 10) +
                copperAmount;

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

            var healAmount = _diceService
                .Roll(DiceType.D6, 1, character.Stats.ConstitutionModifier)
                .Total;

            healAmount = Math.Max(1, healAmount);
            character.Stats.CurrentHitPoints = Math.Min(
                character.Stats.MaxHitPoints,
                character.Stats.CurrentHitPoints + healAmount);

            UpdateCharacter(character);
            return $"{character.Name} rests and recovers {healAmount} HP.";
        }

        public string LongRest(Guid characterId)
        {
            var character = GetCharacter(characterId);
            if (character == null) return "Character not found.";

            if (!character.Stats.IsAlive)
                return $"{character.Name} is dead and cannot rest.";

            character.Stats.CurrentHitPoints = character.Stats.MaxHitPoints;
            character.Stats.CurrentMana      = character.Stats.MaxMana;
            character.Stats.CurrentStamina   = character.Stats.MaxStamina;
            character.ClearAllStatuses();

            UpdateCharacter(character);
            return $"{character.Name} takes a long rest and is fully restored.";
        }
    }
}

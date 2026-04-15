using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.World;

namespace RealmsEdge.Shared.Services
{
    public class TravelResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public WorldLocation? Destination { get; set; }
        public List<Encounter> TriggeredEncounters { get; set; } = new();
        public bool EncounterTriggered => TriggeredEncounters.Any();
        public int TravelTimeMinutes { get; set; }
        public SoundEffect? SoundToPlay { get; set; }
    }

    public class WorldService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly WorldMap _worldMap;
        private readonly CharacterService _characterService;
        private readonly DiceService _diceService;

        public WorldService(
            WorldMap worldMap,
            CharacterService characterService,
            DiceService diceService)
        {
            _worldMap         = worldMap;
            _characterService = characterService;
            _diceService      = diceService;
        }

        // =====================
        // World Access
        // =====================

        public WorldMap GetWorldMap() => _worldMap;

        public WorldLocation? GetLocation(Guid locationId)
            => _worldMap.GetLocation(locationId);

        public WorldLocation? GetPlayerLocation(Guid characterId)
            => _worldMap.GetPlayerLocation(characterId);

        public List<WorldLocation> GetAvailableLocations(
            Guid characterId)
        {
            var location = GetPlayerLocation(characterId);
            if (location == null) return new();
            return _worldMap.GetNeighbours(location.Id);
        }

        public List<WorldLocation> GetLocationsForLevel(int level)
            => _worldMap.GetLocationsForLevel(level);

        // =====================
        // Player Spawning
        // =====================

        public (bool Success, string Message) SpawnPlayer(
            Guid characterId)
        {
            var character = _characterService.GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            var startLocation = _worldMap.StartingLocation;
            if (startLocation == null)
                return (false, "No starting location defined.");

            _worldMap.SetPlayerLocation(characterId, startLocation.Id);
            _worldMap.DiscoverLocation(startLocation.Id);

            return (true,
                $"{character.Name} arrives in " +
                $"{startLocation.Name}. " +
                $"{startLocation.Description}");
        }

        // =====================
        // Travel Between Locations
        // =====================

        public async Task<TravelResult> TravelTo(
            Guid characterId,
            Guid destinationId)
        {
            var character = _characterService.GetCharacter(characterId);
            if (character == null)
                return new TravelResult
                {
                    Success = false,
                    Message = "Character not found."
                };

            var currentLocation = GetPlayerLocation(characterId);
            if (currentLocation == null)
                return new TravelResult
                {
                    Success = false,
                    Message = "Current location not found. " +
                              "Use SpawnPlayer first."
                };

            var destination = _worldMap.GetLocation(destinationId);
            if (destination == null)
                return new TravelResult
                {
                    Success = false,
                    Message = "Destination not found."
                };

            // Check if travel is possible
            if (!_worldMap.CanTravel(
                currentLocation.Id, destinationId, character))
                return new TravelResult
                {
                    Success = false,
                    Message = _worldMap.CannotTravelReason(
                        currentLocation.Id, destinationId, character)
                };

            var path = _worldMap.GetPath(
                currentLocation.Id, destinationId);
            if (path == null)
                return new TravelResult
                {
                    Success = false,
                    Message = "No path found."
                };

            // Roll for travel encounters
            var encounters = await RollTravelEncounters(
                character, path, destination);

            // Move player to destination
            _worldMap.SetPlayerLocation(characterId, destinationId);
            _worldMap.DiscoverLocation(destinationId);

            // Discover destination region
            var region = _worldMap.GetRegionForLocation(destinationId);
            if (region != null)
                region.IsDiscovered = true;

            // Update last played
            _characterService.UpdateCharacter(character);

            return new TravelResult
            {
                Success              = true,
                Message              = BuildArrivalMessage(
                    character, destination),
                Destination          = destination,
                TriggeredEncounters  = encounters,
                TravelTimeMinutes    = path.TravelTimeMinutes,
                SoundToPlay          = destination.Type switch
                {
                    LocationType.Town    or
                    LocationType.City    or
                    LocationType.Village => SoundEffect.TavernAmbience,
                    LocationType.Dungeon or
                    LocationType.Crypt   or
                    LocationType.Cave => SoundEffect.DungeonAmbience,
                    _ => null
                }
            };
        }

        // =====================
        // Party Travel
        // =====================

        public async Task<List<TravelResult>> PartyTravelTo(
            List<Guid> characterIds,
            Guid destinationId)
        {
            var results = new List<TravelResult>();

            foreach (var characterId in characterIds)
            {
                var result = await TravelTo(characterId, destinationId);
                results.Add(result);
            }

            return results;
        }

        // =====================
        // World Events
        // =====================

        public string TriggerWorldEvent(
            WorldEvent worldEvent,
            int durationMinutes)
        {
            _worldMap.SetWorldEvent(worldEvent, durationMinutes);
            return _worldMap.WorldEventDisplay;
        }

        public string ClearWorldEvent()
        {
            var eventName = _worldMap.CurrentEvent.ToString();
            _worldMap.ClearWorldEvent();
            return $"The {eventName} has ended. " +
                   $"The world returns to normal.";
        }

        public WorldEvent GetCurrentWorldEvent()
            => _worldMap.CurrentEvent;

        public int GetWorldEventDiceModifier(DiceType diceType)
            => _worldMap.GetEventDiceModifier(diceType);

        // =====================
        // Location Discovery
        // =====================

        public List<WorldLocation> GetDiscoveredLocations()
            => _worldMap.Locations
                .Where(l => _worldMap
                    .IsLocationDiscovered(l.Id))
                .ToList();

        public List<WorldLocation> GetUndiscoveredLocations()
            => _worldMap.Locations
                .Where(l => !_worldMap
                    .IsLocationDiscovered(l.Id))
                .ToList();

        public bool DiscoverLocation(Guid locationId)
        {
            var location = GetLocation(locationId);
            if (location == null) return false;
            _worldMap.DiscoverLocation(locationId);
            return true;
        }

        // =====================
        // Location Services
        // =====================

        public bool LocationHasService(
            Guid locationId,
            string serviceName)
        {
            var location = GetLocation(locationId);
            if (location == null) return false;

            return serviceName.ToLower() switch
            {
                "inn" => location.HasInn,
                "merchant" => location.HasMerchant,
                "blacksmith" => location.HasBlacksmith,
                "temple" => location.HasTemple,
                "trainer" => location.HasTrainer,
                "tavern" => location.HasTavern,
                "bank" => location.HasBank,
                "guild" => location.HasGuild,
                _ => false
            };
        }

        public (bool Success, string Message) UseInn(
            Guid characterId,
            Guid locationId)
        {
            if (!LocationHasService(locationId, "inn"))
                return (false, "There is no inn here.");

            var character = _characterService
                .GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            // Inn costs 5 silver per night
            var costCopper = 50;
            if (!character.CanAfford(costCopper))
                return (false,
                    $"You cannot afford the inn. " +
                    $"It costs 5 silver per night.");

            character.SpendCurrency(costCopper);
            var restResult = _characterService
                .LongRest(characterId);
            _characterService.UpdateCharacter(character);

            return (true,
                $"You spend a comfortable night at the inn. " +
                $"5 silver spent. {restResult}");
        }

        public (bool Success, string Message) UseTemple(
            Guid characterId,
            Guid locationId)
        {
            if (!LocationHasService(locationId, "temple"))
                return (false, "There is no temple here.");

            var character = _characterService
                .GetCharacter(characterId);
            if (character == null)
                return (false, "Character not found.");

            if (!character.Stats.IsAlive)
            {
                // Resurrection costs 100 gold
                var costCopper = 10000;
                if (!character.CanAfford(costCopper))
                    return (false,
                        "Resurrection costs 100 gold. " +
                        "You cannot afford it.");

                character.SpendCurrency(costCopper);
                character.Stats.CurrentHitPoints = 1;
                character.RemoveStatus(CharacterStatus.Dead);
                _characterService.UpdateCharacter(character);

                return (true,
                    $"{character.Name} is resurrected! " +
                    $"100 gold paid to the temple.");
            }

            // Healing costs 2 silver per HP restored
            var missingHp = character.Stats.MaxHitPoints
                          - character.Stats.CurrentHitPoints;
            if (missingHp == 0)
                return (false,
                    $"{character.Name} is already at full health.");

            var healCost = missingHp * 2;
            if (!character.CanAfford(healCost))
                return (false,
                    $"Full healing costs {healCost}c. " +
                    $"You cannot afford it.");

            character.SpendCurrency(healCost);
            character.Stats.RestoreFull();
            character.ClearAllStatuses();
            _characterService.UpdateCharacter(character);

            return (true,
                $"{character.Name} is fully healed by the temple. " +
                $"{healCost}c spent.");
        }

        // =====================
        // NPC Interaction
        // =====================

        public NpcCharacter? GetNpcAtLocation(
            Guid locationId,
            Guid npcId)
        {
            var location = GetLocation(locationId);
            return location?.GetNpc(npcId);
        }

        public List<NpcCharacter> GetNpcsAtLocation(
            Guid locationId,
            NpcType? filterByType = null)
        {
            var location = GetLocation(locationId);
            if (location == null) return new();

            return filterByType.HasValue
                ? location.GetNpcsByType(filterByType.Value)
                : location.PermanentNpcs;
        }

        public string GetNpcGreeting(
            Guid locationId,
            Guid npcId,
            PlayerCharacter player)
        {
            var npc = GetNpcAtLocation(locationId, npcId);
            if (npc == null) return "There is no one here.";

            var reaction = npc.GetReactionTo(player.Alignment);

            return reaction switch
            {
                "Hostile" =>
                    $"{npc.Name} eyes you with open hostility. " +
                    $"\"{npc.CombatTaunt ?? "Get out of my sight."}\"",
                "Suspicious" =>
                    $"{npc.Name} looks you over with suspicion. " +
                    $"\"{npc.Greeting ?? "What do you want?"}\"",
                "Welcoming" =>
                    $"{npc.Name} greets you warmly. " +
                    $"\"{npc.Greeting ?? "Welcome, friend!"}\"",
                _ =>
                    $"{npc.Name} acknowledges your presence. " +
                    $"\"{npc.Greeting ?? "Hello, traveller."}\"",
            };
        }

        // =====================
        // World Statistics
        // =====================

        public string GetWorldSummary()
            => _worldMap.WorldSummary;

        public double GetExplorationPercent()
            => _worldMap.ExplorationPercent;

        public List<WorldLocation> GetMostDangerousLocations()
            => _worldMap.MostDangerousLocations;

        public List<WorldLocation> GetBeginnersLocations()
            => _worldMap.BeginnersLocations;

        // =====================
        // Private Helpers
        // =====================

        private async Task<List<Encounter>> RollTravelEncounters(
            PlayerCharacter character,
            WorldPath path,
            WorldLocation destination)
        {
            var encounters = new List<Encounter>();

            // Apply world event modifier to encounter chance
            var eventModifier = _worldMap.CurrentEvent switch
            {
                WorldEvent.WarDeclared => 20,
                WorldEvent.DragonSighting => 30,
                WorldEvent.Plague => 15,
                WorldEvent.Eclipse => 10,
                _ => 0
            };

            var encounterChance = Math.Min(95,
                path.EncounterChance + eventModifier);

            var roll = _diceService
                .RollPercentageChance(encounterChance);

            if (roll)
            {
                // Generate a basic travel encounter
                var enemy = NpcCharacter.CreateEnemy(
                    GetRandomEnemyName(destination.Type),
                    GetRandomEnemyRace(destination.Type),
                    CharacterClass.Fighter,
                    Math.Max(1, destination.RecommendedLevel),
                    NpcBehaviour.Aggressive,
                    destination.DangerLevel switch
                    {
                        <= 3 => LootTable.Common,
                        <= 6 => LootTable.Uncommon,
                        <= 8 => LootTable.Rare,
                        _ => LootTable.Elite
                    },
                    _diceService);

                var encounter = Encounter.CreateCombat(
                    $"Ambushed by {enemy.Name}!",
                    new List<NpcCharacter> { enemy },
                    new EncounterReward
                    {
                        ExperiencePoints = enemy.ExperienceReward,
                        GoldReward       = enemy.GoldReward
                    });

                encounters.Add(encounter);
            }

            await Task.CompletedTask;
            return encounters;
        }

        private string BuildArrivalMessage(
            PlayerCharacter character,
            WorldLocation destination)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"{character.Name} arrives at {destination.Name}.");
            sb.AppendLine(destination.Description);

            if (destination.WeatherDescription != null)
                sb.AppendLine(destination.WeatherDescription);

            if (_worldMap.HasActiveEvent)
                sb.AppendLine(_worldMap.WorldEventDisplay);

            if (destination.AvailableServices.Any())
                sb.AppendLine("Services: " + string.Join(", ",
                    destination.AvailableServices));

            return sb.ToString().Trim();
        }

        private static string GetRandomEnemyName(LocationType type)
            => type switch
            {
                LocationType.Forest => "Forest Bandit",
                LocationType.Crypt => "Skeleton Warrior",
                LocationType.Cave => "Cave Troll",
                LocationType.Dungeon => "Dungeon Crawler",
                LocationType.Mountains => "Mountain Ogre",
                LocationType.Swamp => "Swamp Hag",
                LocationType.Plains => "Highwayman",
                _ => "Wandering Brigand"
            };

        private static CharacterRace GetRandomEnemyRace(
            LocationType type) => type switch
            {
                LocationType.Crypt => CharacterRace.Undead,
                LocationType.Cave => CharacterRace.Orc,
                LocationType.Dungeon => CharacterRace.Goblin,
                LocationType.Mountains => CharacterRace.HalfOrc,
                LocationType.Swamp => CharacterRace.Human,
                _ => CharacterRace.Human
            };
    }
}
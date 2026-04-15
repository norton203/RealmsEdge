using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.World;

namespace RealmsEdge.Shared.Services
{
    public class NavigationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Room? Room { get; set; }
        public List<string> EventLog { get; set; } = new();
        public List<Encounter> TriggeredEncounters { get; set; } = new();
        public bool EncounterTriggered => TriggeredEncounters.Any();
        public SoundEffect? SoundToPlay { get; set; }
        public List<Direction> AvailableDirections { get; set; } = new();
    }

    public class SearchResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> EventLog { get; set; } = new();
        public bool SecretFound { get; set; }
        public bool TrapFound { get; set; }
        public bool LootFound { get; set; }
        public List<Encounter> DiscoveredEncounters { get; set; } = new();
    }

    public class NavigationService
    {
        // =====================
        // Dependencies
        // =====================

        private readonly WorldService _worldService;
        private readonly DiceService _diceService;
        private readonly CharacterService _characterService;
        private readonly EncounterService _encounterService;

        // Tracks current room per character
        private readonly Dictionary<Guid, Guid> _characterRooms = new();

        public NavigationService(
            WorldService worldService,
            DiceService diceService,
            CharacterService characterService,
            EncounterService encounterService)
        {
            _worldService     = worldService;
            _diceService      = diceService;
            _characterService = characterService;
            _encounterService = encounterService;
        }

        // =====================
        // Current Room
        // =====================

        public Room? GetCurrentRoom(Guid characterId)
        {
            if (!_characterRooms.TryGetValue(
                characterId, out var roomId))
                return null;

            var location = _worldService
                .GetPlayerLocation(characterId);

            return location?.GetRoom(roomId);
        }

        public WorldLocation? GetCurrentLocation(Guid characterId)
            => _worldService.GetPlayerLocation(characterId);

        // =====================
        // Enter Location
        // =====================

        public NavigationResult EnterLocation(
            Guid characterId,
            Guid locationId)
        {
            var log = new List<string>();
            var location = _worldService.GetLocation(locationId);
            var character = _characterService
                .GetCharacter(characterId);

            if (location == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = "Location not found."
                };

            if (character == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = "Character not found."
                };

            if (!location.CanEnter(character))
                return new NavigationResult
                {
                    Success = false,
                    Message = location.CannotEnterReason(character)
                };

            // Place character in entry room
            var entryRoom = location.EntryRoom;
            if (entryRoom == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = $"{location.Name} has no entry point."
                };

            _characterRooms[characterId] = entryRoom.Id;
            entryRoom.PlayerEntered(characterId);

            log.Add($"📍 You enter {location.Name}.");
            log.Add(location.Description);

            if (location.IsDark)
                log.Add(
                    "🕯️ It is very dark here. " +
                    "You need a light source.");

            if (entryRoom.OnEnterMessage != null)
                log.Add(entryRoom.OnEnterMessage);

            // Check for on enter encounters
            var encounters = GetOnEnterEncounters(entryRoom);

            return new NavigationResult
            {
                Success              = true,
                Message              = $"You enter {location.Name}.",
                Room                 = entryRoom,
                EventLog             = log,
                TriggeredEncounters  = encounters,
                AvailableDirections  = entryRoom.AvailableDirections,
                SoundToPlay          = location.AmbientSoundEffect != null
                    ? Enum.TryParse<SoundEffect>(
                        location.AmbientSoundEffect,
                        out var sound) ? sound : null
                    : null
            };
        }

        // =====================
        // Move Between Rooms
        // =====================

        public NavigationResult Move(
            Guid characterId,
            Direction direction)
        {
            var log = new List<string>();
            var character = _characterService
                .GetCharacter(characterId);

            if (character == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = "Character not found."
                };

            var currentRoom = GetCurrentRoom(characterId);
            if (currentRoom == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = "You are not in a room. " +
                              "Enter a location first."
                };

            // Check exit exists
            var exit = currentRoom.GetExit(direction);
            if (exit == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = $"There is no exit to the {direction}.",
                    Room    = currentRoom,
                    AvailableDirections = currentRoom
                        .AvailableDirections
                };

            // Check exit is not hidden
            if (exit.IsHidden &&
                currentRoom.State != RoomState.Explored)
                return new NavigationResult
                {
                    Success = false,
                    Message = "You do not see an exit that way.",
                    Room    = currentRoom,
                    AvailableDirections = currentRoom
                        .AvailableDirections
                };

            // Get destination room
            var location = GetCurrentLocation(characterId);
            if (location == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = "Current location not found."
                };

            var destinationRoom = location
                .GetRoom(exit.TargetRoomId);

            if (destinationRoom == null)
                return new NavigationResult
                {
                    Success = false,
                    Message = "Destination room not found."
                };

            // Check if room can be entered
            if (!destinationRoom.CanEnter(character))
            {
                log.Add(exit.Description
                    ?? $"A door blocks the way {direction}.");

                if (destinationRoom.RequiredKeyName != null)
                    log.Add(
                        $"🔒 Requires: {destinationRoom.RequiredKeyName}");

                return new NavigationResult
                {
                    Success  = false,
                    Message  = $"You cannot go {direction}.",
                    EventLog = log,
                    Room     = currentRoom,
                    AvailableDirections = currentRoom
                        .AvailableDirections
                };
            }

            // Check if current room has active enemies
            if (currentRoom.HasHostileNpcs)
                return new NavigationResult
                {
                    Success = false,
                    Message = "You cannot flee — " +
                              "enemies block your path!",
                    Room    = currentRoom,
                    AvailableDirections = currentRoom
                        .AvailableDirections
                };

            // Move character
            currentRoom.PlayerLeft(characterId);
            _characterRooms[characterId] = destinationRoom.Id;
            destinationRoom.PlayerEntered(characterId);

            log.Add($"➡️ You move {direction}.");
            log.Add($"📍 {destinationRoom.TypeDisplay}: " +
                    $"{destinationRoom.Name}");

            // First visit message
            if (!destinationRoom.IsExplored)
            {
                log.Add(destinationRoom.Description);
                if (destinationRoom.OnEnterMessage != null)
                    log.Add(destinationRoom.OnEnterMessage);
            }
            else
            {
                log.Add(destinationRoom.ExploredDescription
                    ?? destinationRoom.Description);
            }

            // Dark room warning
            if (destinationRoom.IsDark)
                log.Add(
                    "🕯️ This room is pitch black. " +
                    "You need a light source to see clearly.");

            // Loot on ground
            if (destinationRoom.HasLoot)
                log.Add(
                    $"💎 You notice {destinationRoom.GroundLoot.Count} " +
                    $"item(s) on the ground.");

            // Friendly NPCs present
            if (destinationRoom.HasFriendlyNpcs)
            {
                var npcs = destinationRoom.GetFriendlyNpcs();
                log.Add("👤 You see: " + string.Join(", ",
                    npcs.Select(n => n.Name)));
            }

            // Check for on enter encounters
            var encounters = GetOnEnterEncounters(destinationRoom);

            // Check if room is now cleared
            if (!destinationRoom.HasHostileNpcs &&
                !destinationRoom.HasActiveEncounters &&
                destinationRoom.State == RoomState.Explored)
            {
                destinationRoom.MarkCleared();
                if (destinationRoom.OnClearMessage != null)
                    log.Add(destinationRoom.OnClearMessage);
            }

            return new NavigationResult
            {
                Success             = true,
                Message             = $"You move {direction}.",
                Room                = destinationRoom,
                EventLog            = log,
                TriggeredEncounters = encounters,
                AvailableDirections = destinationRoom
                    .AvailableDirections,
                SoundToPlay = destinationRoom.AmbientSoundEffect != null
                    ? Enum.TryParse<SoundEffect>(
                        destinationRoom.AmbientSoundEffect,
                        out var sound) ? sound : null
                    : null
            };
        }

        // =====================
        // Search Room
        // =====================

        public SearchResult SearchRoom(
            Guid characterId)
        {
            var log = new List<string>();
            var character = _characterService
                .GetCharacter(characterId);

            if (character == null)
                return new SearchResult
                {
                    Success = false,
                    Message = "Character not found."
                };

            var room = GetCurrentRoom(characterId);
            if (room == null)
                return new SearchResult
                {
                    Success = false,
                    Message = "You are not in a room."
                };

            log.Add("🔍 You search the room carefully...");

            var result = new SearchResult { Success = true };

            // Perception roll for searching
            var searchRoll = _diceService.Roll(
                DiceType.D20, 1,
                character.Stats.WisdomModifier);

            log.Add($"👁️ Perception: {searchRoll.Total}");

            // Search for hidden exits
            var hiddenExits = room.Exits
                .Where(e => e.IsHidden).ToList();

            foreach (var exit in hiddenExits)
            {
                // DC 15 to find secret doors
                if (searchRoll.Total >= 15)
                {
                    result.SecretFound = true;
                    log.Add(
                        $"🚪 You discover a hidden exit " +
                        $"to the {exit.Direction}!");
                }
            }

            // Search for traps
            var trapEncounters = room.Encounters
                .Where(e => e.Type == EncounterType.Trap &&
                            !e.IsCompleted &&
                            !e.TrapIsSpotted)
                .ToList();

            foreach (var trap in trapEncounters)
            {
                if (searchRoll.Total >=
                    trap.TrapDifficultyClass - 2)
                {
                    trap.TrapIsSpotted  = true;
                    result.TrapFound    = true;
                    result.DiscoveredEncounters.Add(trap);
                    log.Add(
                        $"⚠️ You spot a trap: {trap.Name}! " +
                        $"(DC {trap.TrapDifficultyClass})");
                }
            }

            // Search for hidden loot
            if (searchRoll.Total >= 12)
            {
                var hiddenEncounters = room.Encounters
                    .Where(e => e.Type == EncounterType.Treasure &&
                                !e.IsCompleted &&
                                e.Trigger == EncounterTrigger.OnSearch)
                    .ToList();

                foreach (var treasure in hiddenEncounters)
                {
                    result.LootFound = true;
                    result.DiscoveredEncounters.Add(treasure);
                    log.Add($"💎 You find something: {treasure.Name}!");
                }
            }

            // Search for lore
            if (searchRoll.Total >= 10)
            {
                var loreEntries = room.Encounters
                    .Where(e => e.Type == EncounterType.LoreEntry &&
                                !e.IsCompleted &&
                                e.Trigger == EncounterTrigger.OnSearch)
                    .ToList();

                foreach (var lore in loreEntries)
                {
                    result.DiscoveredEncounters.Add(lore);
                    log.Add($"📖 You find an inscription: " +
                            $"{lore.LoreTitle}");
                }
            }

            // Nothing found
            if (!result.SecretFound &&
                !result.TrapFound &&
                !result.LootFound &&
                !result.DiscoveredEncounters.Any())
                log.Add("Nothing of interest found.");

            result.EventLog = log;
            result.Message  = result.SecretFound
                ? "Your search reveals hidden secrets!"
                : result.TrapFound
                    ? "You find a trap before it finds you!"
                    : result.LootFound
                        ? "You find hidden treasure!"
                        : "Nothing of note here.";

            return result;
        }

        // =====================
        // Pick Up Loot
        // =====================

        public (bool Success, string Message) PickUpItem(
            Guid characterId,
            Guid itemId)
        {
            var character = _characterService
                .GetCharacter(characterId);
            var room = GetCurrentRoom(characterId);

            if (character == null)
                return (false, "Character not found.");
            if (room == null)
                return (false, "You are not in a room.");

            var item = room.PickUpLoot(itemId);
            if (item == null)
                return (false, "Item not found on the ground.");

            var result = _characterService
                .AddItemToCharacter(characterId, item);

            if (!result.Success)
            {
                // Put it back if cant carry
                room.AddLoot(item);
                return (false, result.Message);
            }

            return (true,
                $"{character.Name} picks up {item.Name}.");
        }

        // =====================
        // Interact With NPC
        // =====================

        public (bool Success, string Message) InteractWithNpc(
            Guid characterId,
            Guid npcId)
        {
            var character = _characterService
                .GetCharacter(characterId);
            var room = GetCurrentRoom(characterId);
            var location = GetCurrentLocation(characterId);

            if (character == null)
                return (false, "Character not found.");
            if (room == null || location == null)
                return (false, "You are not in a room.");

            // Check room NPCs first then location NPCs
            var npc = room.GetNpc(npcId)
                ?? location.GetNpc(npcId);

            if (npc == null)
                return (false, "NPC not found here.");

            if (!npc.Stats.IsAlive)
                return (false,
                    $"{npc.Name} is dead. " +
                    $"They have nothing to say.");

            var greeting = _worldService.GetNpcGreeting(
                location.Id, npcId, character);

            return (true, greeting);
        }

        // =====================
        // Party Navigation
        // =====================

        public List<NavigationResult> PartyMove(
            List<Guid> characterIds,
            Direction direction)
        {
            return characterIds
                .Select(id => Move(id, direction))
                .ToList();
        }

        public List<NavigationResult> PartyEnterLocation(
            List<Guid> characterIds,
            Guid locationId)
        {
            return characterIds
                .Select(id => EnterLocation(id, locationId))
                .ToList();
        }

        // =====================
        // Room Description
        // =====================

        public string DescribeCurrentRoom(Guid characterId)
        {
            var room = GetCurrentRoom(characterId);
            var location = GetCurrentLocation(characterId);

            if (room == null || location == null)
                return "You are nowhere.";

            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"📍 {location.Name} — " +
                          $"{room.TypeDisplay}: {room.Name}");
            sb.AppendLine(room.IsExplored && room.ExploredDescription != null
                ? room.ExploredDescription
                : room.Description);

            // State
            sb.AppendLine(room.StateDisplay);

            // Darkness
            if (room.IsDark)
                sb.AppendLine("🕯️ The room is very dark.");

            // Exits
            sb.AppendLine($"Exits: {room.DirectionsDisplay}");

            // Enemies
            var hostiles = room.GetHostileNpcs();
            if (hostiles.Any())
            {
                sb.AppendLine("⚔️ Enemies here:");
                foreach (var enemy in hostiles)
                    sb.AppendLine(
                        $"  • {enemy.Name} — " +
                        $"HP: {enemy.Stats.CurrentHitPoints}/" +
                        $"{enemy.Stats.MaxHitPoints} — " +
                        $"{enemy.HealthStatus}");
            }

            // Friendly NPCs
            var friendlies = room.GetFriendlyNpcs();
            if (friendlies.Any())
            {
                sb.AppendLine("👤 Present:");
                foreach (var npc in friendlies)
                    sb.AppendLine($"  • {npc.Name} ({npc.NpcType})");
            }

            // Ground loot
            if (room.HasLoot)
            {
                sb.AppendLine("💎 Items on the ground:");
                foreach (var item in room.GroundLoot)
                    sb.AppendLine(
                        $"  • {item.Name} ({item.Rarity})");
            }

            return sb.ToString().Trim();
        }

        // =====================
        // Private Helpers
        // =====================

        private List<Encounter> GetOnEnterEncounters(Room room)
        {
            return room.Encounters
                .Where(e => e.Trigger == EncounterTrigger.OnEnter &&
                            !e.IsCompleted &&
                            (e.IsRepeatable || e.Outcome ==
                                EncounterOutcome.Pending))
                .ToList();
        }
    }
}
using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;

namespace RealmsEdge.Shared.Models.World
{
    public enum WorldEvent
    {
        None,               // Nothing happening
        Eclipse,            // Undead stronger, holy magic weaker
        HarvestFestival,    // Shop prices reduced, taverns busy
        WarDeclared,        // Guards aggressive, travel dangerous
        Plague,             // Disease encounters more frequent
        DragonSighting,     // High danger areas even more dangerous
        MagicSurge,         // Spells more powerful but unpredictable
        FamineYear,         // Food prices high, beggars everywhere
        TournamentDay,      // Arena events available, prizes high
        DarkOmen            // All dice rolls at slight disadvantage
    }

    public enum TravelDifficulty
    {
        Easy,               // Roads, safe paths
        Moderate,           // Worn paths, some hazards
        Hard,               // Off road, frequent encounters
        Treacherous,        // Dangerous terrain, high encounter rate
        Impassable          // Cannot travel without special ability
    }

    public class WorldPath
    {
        // Connects two WorldLocations on the map
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid FromLocationId { get; set; }
        public Guid ToLocationId { get; set; }
        public TravelDifficulty Difficulty { get; set; }
            = TravelDifficulty.Moderate;
        public int TravelTimeMinutes { get; set; } = 30;    // Real time travel
        public int EncounterChance { get; set; } = 25;      // % per travel segment
        public bool IsDiscovered { get; set; } = false;
        public bool IsBlocked { get; set; } = false;
        public string? BlockedReason { get; set; }          // Why path is blocked
        public List<EncounterType> PossibleEncounters { get; set; } = new();

        public string DifficultyDisplay => Difficulty switch
        {
            TravelDifficulty.Easy => "🟢 Easy",
            TravelDifficulty.Moderate => "🟡 Moderate",
            TravelDifficulty.Hard => "🟠 Hard",
            TravelDifficulty.Treacherous => "🔴 Treacherous",
            TravelDifficulty.Impassable => "⛔ Impassable",
            _ => "Unknown"
        };
    }

    public class WorldMapRegion
    {
        // Groups locations into named regions
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public List<Guid> LocationIds { get; set; } = new();
        public int MinDangerLevel { get; set; } = 1;
        public int MaxDangerLevel { get; set; } = 5;
        public int RecommendedLevel { get; set; } = 1;
        public bool IsDiscovered { get; set; } = false;
        public string? LoreText { get; set; }

        // Region colour for map display
        public string MapColour { get; set; } = "#4a7c59";  // Default green

        public string RegionSummary =>
            $"{Name} | Levels {RecommendedLevel}+ | " +
            $"Danger {MinDangerLevel}-{MaxDangerLevel} | " +
            $"{LocationIds.Count} locations";
    }

    public class WorldMap
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "The World of Realm's Edge";
        public string Description { get; set; } = string.Empty;
        public string? MapImagePath { get; set; }           // Full world map image
        public int Width { get; set; } = 100;               // Map grid width
        public int Height { get; set; } = 100;              // Map grid height
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // =====================
        // Locations
        // =====================

        public List<WorldLocation> Locations { get; set; } = new();
        public Guid? StartingLocationId { get; set; }       // New player spawn

        public WorldLocation? StartingLocation => Locations
            .FirstOrDefault(l => l.Id == StartingLocationId);

        public WorldLocation? GetLocation(Guid locationId) => Locations
            .FirstOrDefault(l => l.Id == locationId);

        public WorldLocation? GetLocationByName(string name) => Locations
            .FirstOrDefault(l => l.Name
                .Equals(name, StringComparison.OrdinalIgnoreCase));

        public List<WorldLocation> GetLocationsByType(LocationType type) => Locations
            .Where(l => l.Type == type).ToList();

        public List<WorldLocation> GetSafeZones() => Locations
            .Where(l => l.IsSafeZone).ToList();

        public List<WorldLocation> GetLocationsByDangerLevel(
            int minLevel, int maxLevel) => Locations
                .Where(l => l.DangerLevel >= minLevel &&
                            l.DangerLevel <= maxLevel)
                .OrderBy(l => l.DangerLevel)
                .ToList();

        public List<WorldLocation> GetLocationsForLevel(int playerLevel) => Locations
            .Where(l => l.RecommendedLevel <= playerLevel + 2 &&
                        l.RecommendedLevel >= playerLevel - 2)
            .OrderBy(l => l.RecommendedLevel)
            .ToList();

        public void AddLocation(WorldLocation location)
        {
            Locations.Add(location);
            // First location added becomes starting location
            if (!StartingLocationId.HasValue)
                StartingLocationId = location.Id;
        }

        // =====================
        // Regions
        // =====================

        public List<WorldMapRegion> Regions { get; set; } = new();

        public WorldMapRegion? GetRegion(Guid regionId) => Regions
            .FirstOrDefault(r => r.Id == regionId);

        public WorldMapRegion? GetRegionForLocation(Guid locationId) => Regions
            .FirstOrDefault(r => r.LocationIds.Contains(locationId));

        public void AddRegion(WorldMapRegion region)
            => Regions.Add(region);

        // =====================
        // Paths
        // =====================

        public List<WorldPath> Paths { get; set; } = new();

        public WorldPath? GetPath(Guid fromId, Guid toId) => Paths
            .FirstOrDefault(p =>
                (p.FromLocationId == fromId && p.ToLocationId == toId) ||
                (p.FromLocationId == toId   && p.ToLocationId == fromId));

        public List<WorldLocation> GetNeighbours(Guid locationId) => Paths
            .Where(p => p.FromLocationId == locationId ||
                        p.ToLocationId == locationId)
            .Where(p => !p.IsBlocked && p.IsDiscovered)
            .Select(p => p.FromLocationId == locationId
                ? GetLocation(p.ToLocationId)
                : GetLocation(p.FromLocationId))
            .Where(l => l != null)
            .ToList()!;

        public void AddPath(
            Guid fromLocationId,
            Guid toLocationId,
            TravelDifficulty difficulty = TravelDifficulty.Moderate,
            int travelTimeMinutes = 30,
            int encounterChance = 25)
        {
            Paths.Add(new WorldPath
            {
                FromLocationId   = fromLocationId,
                ToLocationId     = toLocationId,
                Difficulty       = difficulty,
                TravelTimeMinutes = travelTimeMinutes,
                EncounterChance  = encounterChance,
                IsDiscovered     = true             // Paths discovered by default
            });
        }

        public bool CanTravel(Guid fromId, Guid toId,
            PlayerCharacter player)
        {
            var path = GetPath(fromId, toId);
            if (path == null) return false;
            if (path.IsBlocked) return false;
            if (!path.IsDiscovered) return false;

            var destination = GetLocation(toId);
            if (destination == null) return false;

            return destination.CanEnter(player);
        }

        public string CannotTravelReason(Guid fromId,
            Guid toId, PlayerCharacter player)
        {
            var path = GetPath(fromId, toId);
            if (path == null)
                return "There is no path to that location.";
            if (!path.IsDiscovered)
                return "You have not discovered this path yet.";
            if (path.IsBlocked)
                return path.BlockedReason ?? "The path is blocked.";

            var destination = GetLocation(toId);
            if (destination == null)
                return "That location does not exist.";

            return destination.CannotEnterReason(player);
        }

        // =====================
        // World Events
        // (L.O.R.D inspired)
        // =====================

        public WorldEvent CurrentEvent { get; set; }
            = WorldEvent.None;
        public DateTime? EventStartTime { get; set; }
        public DateTime? EventEndTime { get; set; }
        public string? EventDescription { get; set; }

        public bool HasActiveEvent => CurrentEvent != WorldEvent.None
            && EventEndTime.HasValue
            && DateTime.UtcNow < EventEndTime.Value;

        public void SetWorldEvent(
            WorldEvent worldEvent,
            int durationMinutes,
            string? description = null)
        {
            CurrentEvent     = worldEvent;
            EventStartTime   = DateTime.UtcNow;
            EventEndTime     = DateTime.UtcNow.AddMinutes(durationMinutes);
            EventDescription = description ?? GetDefaultEventDescription(worldEvent);
        }

        public void ClearWorldEvent()
        {
            CurrentEvent     = WorldEvent.None;
            EventStartTime   = null;
            EventEndTime     = null;
            EventDescription = null;
        }

        // World event modifies dice rolls globally
        public int GetEventDiceModifier(DiceType diceType)
        {
            if (!HasActiveEvent) return 0;

            return CurrentEvent switch
            {
                WorldEvent.Eclipse => diceType == DiceType.D20 ? -2 : 0,
                WorldEvent.MagicSurge => diceType == DiceType.D20 ? 2 : 0,
                WorldEvent.DarkOmen => -1,
                WorldEvent.TournamentDay => 1,
                _ => 0
            };
        }

        private static string GetDefaultEventDescription(WorldEvent worldEvent)
            => worldEvent switch
            {
                WorldEvent.Eclipse =>
                    "A blood red eclipse darkens the sky. " +
                    "The undead grow restless and holy magic falters.",
                WorldEvent.HarvestFestival =>
                    "The harvest festival fills every town with music and ale. " +
                    "Merchants offer discounts and the inns are packed.",
                WorldEvent.WarDeclared =>
                    "War horns echo across the realm. " +
                    "Guards are on edge and the roads are dangerous.",
                WorldEvent.Plague =>
                    "A sickness spreads through the land. " +
                    "Disease encounters grow more frequent.",
                WorldEvent.DragonSighting =>
                    "A dragon has been spotted circling the mountains. " +
                    "Even the hardiest adventurers travel in groups.",
                WorldEvent.MagicSurge =>
                    "Wild magic tears through the weave. " +
                    "Spells are more powerful but harder to control.",
                WorldEvent.FamineYear =>
                    "The crops have failed. " +
                    "Food is scarce and desperation grows.",
                WorldEvent.TournamentDay =>
                    "The Grand Tournament is underway! " +
                    "Glory and riches await the victorious.",
                WorldEvent.DarkOmen =>
                    "A dark omen hangs over the realm. " +
                    "Fortune seems to turn against all who venture out.",
                _ => string.Empty
            };

        // =====================
        // Player Tracking
        // =====================

        public Dictionary<Guid, Guid> PlayerLocations { get; set; } = new();

        public void SetPlayerLocation(Guid characterId, Guid locationId)
        {
            // Remove from old location
            if (PlayerLocations.TryGetValue(characterId,
                out var oldLocationId))
            {
                var oldLocation = GetLocation(oldLocationId);
                oldLocation?.PlayerLeft(characterId);
            }

            // Add to new location
            PlayerLocations[characterId] = locationId;
            var newLocation = GetLocation(locationId);
            newLocation?.PlayerEntered(characterId);
        }

        public WorldLocation? GetPlayerLocation(Guid characterId)
        {
            if (!PlayerLocations.TryGetValue(characterId,
                out var locationId)) return null;
            return GetLocation(locationId);
        }

        public List<PlayerCharacter> GetPlayersAtLocation(
            Guid locationId,
            List<PlayerCharacter> allPlayers) => allPlayers
                .Where(p => PlayerLocations.TryGetValue(
                    p.Id, out var locId) && locId == locationId)
                .ToList();

        // =====================
        // Discovery
        // =====================

        public HashSet<Guid> GlobalDiscoveredLocations { get; set; } = new();

        public void DiscoverLocation(Guid locationId)
            => GlobalDiscoveredLocations.Add(locationId);

        public bool IsLocationDiscovered(Guid locationId)
            => GlobalDiscoveredLocations.Contains(locationId);

        public int TotalLocations => Locations.Count;
        public int DiscoveredLocations => GlobalDiscoveredLocations.Count;

        public double ExplorationPercent => TotalLocations > 0
            ? Math.Round((double)DiscoveredLocations
                / TotalLocations * 100, 1)
            : 0;

        // =====================
        // World Statistics
        // =====================

        public int TotalRooms => Locations.Sum(l => l.Rooms.Count);

        public int TotalNpcs => Locations
            .Sum(l => l.PermanentNpcs.Count +
                l.Rooms.Sum(r => r.Npcs.Count));

        public int TotalEncounters => Locations
            .Sum(l => l.Rooms.Sum(r => r.Encounters.Count));

        public List<WorldLocation> MostDangerousLocations => Locations
            .OrderByDescending(l => l.DangerLevel)
            .Take(5)
            .ToList();

        public List<WorldLocation> BeginnersLocations => Locations
            .Where(l => l.RecommendedLevel <= 5)
            .OrderBy(l => l.RecommendedLevel)
            .ToList();

        // =====================
        // Factory — Starter World
        // =====================

        public static WorldMap CreateStarterWorld()
        {
            var world = new WorldMap
            {
                Name        = "The World of Realm's Edge",
                Description =
                    "A land of ancient magic, crumbling empires and " +
                    "forgotten gods. Heroes rise and fall like seasons " +
                    "here — but legends endure forever.",
                Width  = 100,
                Height = 100
            };

            // Starting town
            var startTown = new WorldLocation
            {
                Name              = "Ashenvale",
                Description       =
                    "A modest town nestled between ancient forests and " +
                    "rolling hills. The smell of woodsmoke and ale drifts " +
                    "from the Broken Flagon tavern. This is where all " +
                    "adventurers begin their journey.",
                Type              = LocationType.Town,
                WorldX            = 50,
                WorldY            = 50,
                DangerLevel       = 1,
                RecommendedLevel  = 1,
                IsSafeZone        = true,
                IsRespawnPoint    = true,
                HasInn            = true,
                HasMerchant       = true,
                HasBlacksmith     = true,
                HasTemple         = true,
                HasTrainer        = true,
                HasTavern         = true,
                AmbientSoundEffect = "TavernAmbience"
            };

            // First dungeon
            var firstDungeon = new WorldLocation
            {
                Name              = "The Sunken Crypt",
                Description       =
                    "An ancient burial site disturbed by dark magic. " +
                    "The dead do not rest here. Locals whisper of " +
                    "glowing eyes in the darkness below.",
                Type              = LocationType.Crypt,
                WorldX            = 55,
                WorldY            = 45,
                DangerLevel       = 3,
                RecommendedLevel  = 1,
                IsSafeZone        = false,
                AmbientSoundEffect = "DungeonAmbience"
            };

            // First wilderness
            var firstForest = new WorldLocation
            {
                Name              = "Thornwood Forest",
                Description       =
                    "A dense and ancient forest. The canopy is so thick " +
                    "that even at midday the light struggles through. " +
                    "Rangers feel at home here — everyone else feels watched.",
                Type              = LocationType.Forest,
                WorldX            = 45,
                WorldY            = 48,
                DangerLevel       = 2,
                RecommendedLevel  = 1,
                IsSafeZone        = false
            };

            // Second town
            var secondTown = new WorldLocation
            {
                Name              = "Irongate",
                Description       =
                    "A fortified mining town carved into the mountainside. " +
                    "Dwarves and humans work side by side here. " +
                    "The smiths of Irongate are famous across the realm.",
                Type              = LocationType.Town,
                WorldX            = 60,
                WorldY            = 40,
                DangerLevel       = 1,
                RecommendedLevel  = 5,
                IsSafeZone        = true,
                HasInn            = true,
                HasMerchant       = true,
                HasBlacksmith     = true,
                HasTemple         = true,
                HasTrainer        = true,
                HasBank           = true
            };

            // High level dungeon
            var highDungeon = new WorldLocation
            {
                Name              = "The Obsidian Tower",
                Description       =
                    "A black spire that appeared overnight fifty years ago. " +
                    "No one knows who built it or what dwells within. " +
                    "Many have entered. None have returned.",
                Type              = LocationType.Tower,
                WorldX            = 70,
                WorldY            = 30,
                DangerLevel       = 9,
                RecommendedLevel  = 15,
                IsSafeZone        = false,
                RequiredLevel     = 12
            };

            // Add locations
            world.AddLocation(startTown);
            world.AddLocation(firstDungeon);
            world.AddLocation(firstForest);
            world.AddLocation(secondTown);
            world.AddLocation(highDungeon);

            world.StartingLocationId = startTown.Id;
            world.DiscoverLocation(startTown.Id);

            // Add paths
            world.AddPath(startTown.Id, firstDungeon.Id,
                TravelDifficulty.Moderate, 20, 30);
            world.AddPath(startTown.Id, firstForest.Id,
                TravelDifficulty.Easy, 15, 20);
            world.AddPath(startTown.Id, secondTown.Id,
                TravelDifficulty.Hard, 60, 40);
            world.AddPath(secondTown.Id, highDungeon.Id,
                TravelDifficulty.Treacherous, 90, 75);

            // Add regions
            world.AddRegion(new WorldMapRegion
            {
                Name              = "The Heartlands",
                Description       = "The relatively safe central region " +
                                    "where most new adventurers begin.",
                LocationIds       = new() {
                    startTown.Id,
                    firstForest.Id,
                    firstDungeon.Id },
                MinDangerLevel    = 1,
                MaxDangerLevel    = 4,
                RecommendedLevel  = 1,
                MapColour         = "#4a7c59",
                IsDiscovered      = true
            });

            world.AddRegion(new WorldMapRegion
            {
                Name              = "The Iron Reaches",
                Description       = "A harsh mountainous region of mines, " +
                                    "strongholds and ancient secrets.",
                LocationIds       = new() {
                    secondTown.Id,
                    highDungeon.Id },
                MinDangerLevel    = 5,
                MaxDangerLevel    = 10,
                RecommendedLevel  = 5,
                MapColour         = "#7c5a4a",
                IsDiscovered      = false
            });

            return world;
        }

        // =====================
        // Display Helpers
        // =====================

        public string WorldEventDisplay => HasActiveEvent
            ? $"🌍 {CurrentEvent}: {EventDescription}"
            : "🌍 No active world event";

        public string WorldSummary =>
            $"{Name} | " +
            $"{TotalLocations} locations | " +
            $"{TotalRooms} rooms | " +
            $"{TotalNpcs} NPCs | " +
            $"{ExplorationPercent}% explored";

        public override string ToString() => WorldSummary;
    }
}
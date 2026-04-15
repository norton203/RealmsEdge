using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;

namespace RealmsEdge.Shared.Models.World
{
    public class WorldLocation
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImagePath { get; set; }          // Location artwork
        public LocationType Type { get; set; }

        // =====================
        // World Position
        // =====================

        public int WorldX { get; set; }                 // Position on world map
        public int WorldY { get; set; }                 // Position on world map
        public int DangerLevel { get; set; } = 1;       // 1-10, affects encounter difficulty
        public int RecommendedLevel { get; set; } = 1;  // Suggested player level

        // =====================
        // Rooms
        // =====================

        public List<Room> Rooms { get; set; } = new();
        public Guid? EntryRoomId { get; set; }          // Where players spawn in
        public Guid? ExitRoomId { get; set; }           // Where players leave from

        public Room? EntryRoom => Rooms
            .FirstOrDefault(r => r.Id == EntryRoomId);

        public Room? ExitRoom => Rooms
            .FirstOrDefault(r => r.Id == ExitRoomId);

        public Room? GetRoom(Guid roomId) => Rooms
            .FirstOrDefault(r => r.Id == roomId);

        public void AddRoom(Room room)
        {
            Rooms.Add(room);
            // First room added becomes entry by default
            if (!EntryRoomId.HasValue)
                EntryRoomId = room.Id;
        }

        // =====================
        // Access Control
        // =====================

        public bool IsLocked { get; set; } = false;
        public int RequiredLevel { get; set; } = 1;
        public Alignment? RequiredAlignment { get; set; }   // null = any alignment
        public List<CharacterClass> ForbiddenClasses { get; set; } = new();
        public Guid? RequiredQuestId { get; set; }          // Must have quest to enter
        public bool IsSafeZone { get; set; } = false;       // No combat allowed

        public bool CanEnter(PlayerCharacter player)
        {
            if (IsLocked) return false;
            if (player.Level < RequiredLevel) return false;
            if (ForbiddenClasses.Contains(player.Class)) return false;
            if (RequiredAlignment.HasValue &&
                player.Alignment != RequiredAlignment.Value) return false;
            if (RequiredQuestId.HasValue &&
                !player.HasQuest(RequiredQuestId.Value)) return false;
            return true;
        }

        public string CannotEnterReason(PlayerCharacter player)
        {
            if (IsLocked)
                return $"{Name} is locked.";
            if (player.Level < RequiredLevel)
                return $"{Name} requires level {RequiredLevel}. " +
                       $"You are level {player.Level}.";
            if (ForbiddenClasses.Contains(player.Class))
                return $"{player.Class} characters are not welcome in {Name}.";
            if (RequiredAlignment.HasValue &&
                player.Alignment != RequiredAlignment.Value)
                return $"Only {RequiredAlignment} characters may enter {Name}.";
            if (RequiredQuestId.HasValue &&
                !player.HasQuest(RequiredQuestId.Value))
                return $"You must have the required quest to enter {Name}.";
            return string.Empty;
        }

        // =====================
        // Services Available
        // (Town / City / Village)
        // =====================

        public bool HasInn { get; set; } = false;
        public bool HasMerchant { get; set; } = false;
        public bool HasBlacksmith { get; set; } = false;
        public bool HasTemple { get; set; } = false;
        public bool HasTrainer { get; set; } = false;
        public bool HasTavern { get; set; } = false;
        public bool HasBank { get; set; } = false;
        public bool HasGuild { get; set; } = false;

        public List<string> AvailableServices
        {
            get
            {
                var services = new List<string>();
                if (HasInn) services.Add("🏨 Inn");
                if (HasMerchant) services.Add("🛒 Merchant");
                if (HasBlacksmith) services.Add("⚒️ Blacksmith");
                if (HasTemple) services.Add("⛪ Temple");
                if (HasTrainer) services.Add("📚 Trainer");
                if (HasTavern) services.Add("🍺 Tavern");
                if (HasBank) services.Add("💰 Bank");
                if (HasGuild) services.Add("⚔️ Guild");
                return services;
            }
        }

        // =====================
        // NPCs
        // =====================

        public List<NpcCharacter> PermanentNpcs { get; set; } = new();

        public NpcCharacter? GetNpc(Guid npcId) => PermanentNpcs
            .FirstOrDefault(n => n.Id == npcId);

        public List<NpcCharacter> GetNpcsByType(NpcType type) => PermanentNpcs
            .Where(n => n.NpcType == type).ToList();

        // =====================
        // Players Present
        // =====================

        public List<Guid> PlayerCharacterIds { get; set; } = new();

        public void PlayerEntered(Guid characterId)
        {
            if (!PlayerCharacterIds.Contains(characterId))
                PlayerCharacterIds.Add(characterId);
        }

        public void PlayerLeft(Guid characterId)
            => PlayerCharacterIds.Remove(characterId);

        public bool HasPlayers => PlayerCharacterIds.Any();
        public int PlayerCount => PlayerCharacterIds.Count;

        // =====================
        // Atmosphere
        // =====================

        public string? AmbientSoundEffect { get; set; }    // SoundEffect enum name
        public string? BackgroundMusicTrack { get; set; }  // SoundEffect enum name
        public string? WeatherDescription { get; set; }    // Shown on entry
        public bool IsIndoor { get; set; } = false;
        public bool IsDark { get; set; } = false;          // Torches needed

        // =====================
        // Respawn Point
        // =====================

        public bool IsRespawnPoint { get; set; } = false;
        public Guid? RespawnLocationId { get; set; }       // Where to send dead players

        // =====================
        // Display Helpers
        // =====================

        public string DangerDisplay => DangerLevel switch
        {
            1 or 2 => "🟢 Safe",
            3 or 4 => "🟡 Moderate",
            5 or 6 => "🟠 Dangerous",
            7 or 8 => "🔴 Very Dangerous",
            9 or 10 => "💀 Deadly",
            _ => "Unknown"
        };

        public string TypeDisplay => Type switch
        {
            LocationType.Town => "🏘️ Town",
            LocationType.Village => "🏡 Village",
            LocationType.City => "🏙️ City",
            LocationType.Castle => "🏰 Castle",
            LocationType.Inn => "🏨 Inn",
            LocationType.Temple => "⛪ Temple",
            LocationType.Guild => "⚔️ Guild",
            LocationType.Dungeon => "🗝️ Dungeon",
            LocationType.Cave => "🪨 Cave",
            LocationType.Crypt => "💀 Crypt",
            LocationType.Tower => "🗼 Tower",
            LocationType.Ruins => "🏚️ Ruins",
            LocationType.Forest => "🌲 Forest",
            LocationType.Mountains => "⛰️ Mountains",
            LocationType.Swamp => "🌿 Swamp",
            LocationType.Plains => "🌾 Plains",
            _ => "📍 Location"
        };

        public override string ToString()
            => $"{TypeDisplay} {Name} | " +
               $"Level {RecommendedLevel}+ | " +
               $"{DangerDisplay} | " +
               $"{Rooms.Count} rooms";
    }
}
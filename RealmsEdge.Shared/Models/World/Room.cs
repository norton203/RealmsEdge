using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;
using System.Diagnostics.Metrics;

namespace RealmsEdge.Shared.Models.World
{
    public enum RoomType
    {
        // =====================
        // General
        // =====================

        Corridor,           // Connecting passage, usually no encounters
        Chamber,            // Standard room, encounters possible
        Hall,               // Large open area, multiple exits
        Entrance,           // Entry point to location
        Exit,               // Leave point from location

        // =====================
        // Town / City
        // =====================

        TavernRoom,         // Rest, drink, rumours
        ShopRoom,           // Buy and sell items
        InnRoom,            // Rest and recover
        TempleRoom,         // Healing and resurrection
        GuildHall,          // Class quests and training
        ThroneRoom,         // Noble NPC, political quests
        Prison,             // Rescue quests, locked NPCs

        // =====================
        // Dungeon
        // =====================

        BossRoom,           // Named boss encounter
        TreasureRoom,       // High value loot
        TrapRoom,           // Trap heavy, careful navigation
        PuzzleRoom,         // Puzzle blocks progress
        AltarRoom,          // Dark ritual, cursed loot
        Armoury,            // Weapon and armour focused loot
        Library,            // Lore entries and scroll loot

        // =====================
        // Wilderness
        // =====================

        Clearing,           // Open outdoor area
        Campsite,           // Rest point in wilderness
        Shrine,             // Blessing or stat boost
        AncientRuin,        // Hidden lore and rare loot
        Cave,               // Natural shelter
    }

    public enum RoomState
    {
        Unexplored,         // Player has never been here
        Explored,           // Player has visited
        Cleared,            // All enemies defeated
        Locked,             // Requires key or quest to enter
        Collapsed,          // Blocked, cannot enter
        Hidden              // Not visible on map until discovered
    }

    public class Room
    {
        // =====================
        // Identity
        // =====================

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ExploredDescription { get; set; }    // Shown on revisit
        public string? ImagePath { get; set; }
        public RoomType Type { get; set; }
        public RoomState State { get; set; } = RoomState.Unexplored;

        // =====================
        // Position
        // =====================

        public int X { get; set; }                          // Grid position
        public int Y { get; set; }                          // Grid position
        public int Floor { get; set; } = 0;                 // 0 = ground, negative = underground

        // =====================
        // Exits
        // =====================

        public List<RoomExit> Exits { get; set; } = new();

        public RoomExit? GetExit(Direction direction) => Exits
            .FirstOrDefault(e => e.Direction == direction);

        public bool HasExit(Direction direction) => Exits
            .Any(e => e.Direction == direction);

        public List<Direction> AvailableDirections => Exits
            .Where(e => !e.IsHidden || State == RoomState.Explored)
            .Select(e => e.Direction)
            .ToList();

        public void AddExit(Direction direction, Guid targetRoomId,
            bool isLocked = false, bool isHidden = false,
            string? keyName = null)
        {
            Exits.Add(new RoomExit
            {
                Direction    = direction,
                TargetRoomId = targetRoomId,
                IsLocked     = isLocked,
                IsHidden     = isHidden,
                RequiredKeyName = keyName
            });
        }

        // =====================
        // Encounters
        // =====================

        public List<Encounter> Encounters { get; set; } = new();
        public bool HasActiveEncounters => Encounters
            .Any(e => !e.IsCompleted);

        public Encounter? GetNextEncounter() => Encounters
            .FirstOrDefault(e => !e.IsCompleted);

        public void AddEncounter(Encounter encounter)
            => Encounters.Add(encounter);

        // =====================
        // NPCs
        // =====================

        public List<NpcCharacter> Npcs { get; set; } = new();

        public NpcCharacter? GetNpc(Guid npcId) => Npcs
            .FirstOrDefault(n => n.Id == npcId);

        public List<NpcCharacter> GetHostileNpcs() => Npcs
            .Where(n => n.NpcType == NpcType.Enemy ||
                        n.NpcType == NpcType.Elite  ||
                        n.NpcType == NpcType.Boss)
            .Where(n => n.Stats.IsAlive)
            .ToList();

        public List<NpcCharacter> GetFriendlyNpcs() => Npcs
            .Where(n => n.NpcType == NpcType.Merchant   ||
                        n.NpcType == NpcType.QuestGiver  ||
                        n.NpcType == NpcType.Trainer     ||
                        n.NpcType == NpcType.Innkeeper   ||
                        n.NpcType == NpcType.Companion)
            .ToList();

        public bool HasHostileNpcs => GetHostileNpcs().Any();
        public bool HasFriendlyNpcs => GetFriendlyNpcs().Any();

        // =====================
        // Loot
        // =====================

        public List<InventoryItem> GroundLoot { get; set; } = new();
        public bool HasLoot => GroundLoot.Any();

        public void AddLoot(InventoryItem item)
            => GroundLoot.Add(item);

        public InventoryItem? PickUpLoot(Guid itemId)
        {
            var item = GroundLoot.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return null;
            GroundLoot.Remove(item);
            return item;
        }

        // =====================
        // Players Present
        // =====================

        public List<Guid> PlayerCharacterIds { get; set; } = new();

        public void PlayerEntered(Guid characterId)
        {
            if (!PlayerCharacterIds.Contains(characterId))
                PlayerCharacterIds.Add(characterId);

            // Mark as explored on first visit
            if (State == RoomState.Unexplored)
                State = RoomState.Explored;
        }

        public void PlayerLeft(Guid characterId)
            => PlayerCharacterIds.Remove(characterId);

        public bool HasPlayers => PlayerCharacterIds.Any();

        // =====================
        // Room Properties
        // =====================

        public bool IsLocked { get; set; } = false;
        public string? RequiredKeyName { get; set; }        // Item name needed to unlock
        public bool IsDark { get; set; } = false;           // Torch needed to see
        public bool IsSafeZone { get; set; } = false;       // No combat
        public bool IsRespawnPoint { get; set; } = false;
        public int TrapDifficulty { get; set; } = 0;        // 0 = no trap, 1-20 DC check
        public int PuzzleDifficulty { get; set; } = 0;      // 0 = no puzzle

        // =====================
        // Atmosphere
        // =====================

        public string? AmbientSoundEffect { get; set; }
        public string? OnEnterMessage { get; set; }         // Flavour text on entry
        public string? OnClearMessage { get; set; }         // Shown when room is cleared

        // =====================
        // State Helpers
        // =====================

        public bool IsCleared => State == RoomState.Cleared;
        public bool IsExplored => State == RoomState.Explored ||
                                  State == RoomState.Cleared;

        public void MarkCleared()
        {
            State = RoomState.Cleared;
            // Remove hostile NPCs that are dead
            Npcs.RemoveAll(n => !n.Stats.IsAlive &&
                (n.NpcType == NpcType.Enemy ||
                 n.NpcType == NpcType.Elite  ||
                 n.NpcType == NpcType.Boss));
        }

        public bool CanEnter(PlayerCharacter player)
        {
            if (State == RoomState.Collapsed) return false;
            if (!IsLocked) return true;
            if (RequiredKeyName == null) return false;

            return player.Inventory
                .Any(i => i.Name.Equals(RequiredKeyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        // =====================
        // Display Helpers
        // =====================

        public string StateDisplay => State switch
        {
            RoomState.Unexplored => "❓ Unexplored",
            RoomState.Explored => "👁️ Explored",
            RoomState.Cleared => "✅ Cleared",
            RoomState.Locked => "🔒 Locked",
            RoomState.Collapsed => "🚫 Collapsed",
            RoomState.Hidden => "👁️ Hidden",
            _ => "Unknown"
        };

        public string TypeDisplay => Type switch
        {
            RoomType.BossRoom => "👑 Boss Chamber",
            RoomType.TreasureRoom => "💎 Treasure Room",
            RoomType.TrapRoom => "⚠️ Trap Room",
            RoomType.PuzzleRoom => "🧩 Puzzle Room",
            RoomType.TavernRoom => "🍺 Tavern",
            RoomType.ShopRoom => "🛒 Shop",
            RoomType.TempleRoom => "⛪ Temple",
            RoomType.AltarRoom => "🕯️ Dark Altar",
            RoomType.Library => "📚 Library",
            RoomType.Shrine => "✨ Shrine",
            RoomType.Prison => "⛓️ Prison",
            _ => "🚪 Room"
        };

        public string DirectionsDisplay => AvailableDirections.Any()
            ? string.Join(", ", AvailableDirections)
            : "No exits";

        public override string ToString()
            => $"{TypeDisplay}: {Name} | " +
               $"{StateDisplay} | " +
               $"Exits: {DirectionsDisplay}";
    }

    public class RoomExit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Direction Direction { get; set; }
        public Guid TargetRoomId { get; set; }
        public bool IsLocked { get; set; } = false;
        public bool IsHidden { get; set; } = false;         // Secret door
        public string? RequiredKeyName { get; set; }
        public string? Description { get; set; }            // e.g. "A heavy oak door"
        public bool IsOneWay { get; set; } = false;         // Cannot go back

        public string Display => IsHidden ? "???" :
            IsLocked ? $"🔒 {Direction}" :
            $"➡️ {Direction}";
    }
}
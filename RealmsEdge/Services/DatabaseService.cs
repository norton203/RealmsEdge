using SQLite;
using System.Text.Json;
using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Interfaces;
using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Items;

namespace RealmsEdge.Maui.Services
{
    // =====================
    // SQLite Row Model
    // =====================
    // Flat table — complex objects are JSON columns.
    // Keep all SQLite attributes here so the Shared models
    // stay clean and free of SQLite dependencies.

    [Table("characters")]
    public class CharacterRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        // ── Identity ──
        public string PlayerName        { get; set; } = string.Empty;
        public string Name              { get; set; } = string.Empty;
        public string? Title            { get; set; }
        public string? Description      { get; set; }
        public int    Race              { get; set; }   // CharacterRace enum
        public int    Class             { get; set; }   // CharacterClass enum
        public int    Alignment         { get; set; }   // Alignment enum
        public int    Gender            { get; set; }   // Gender enum

        // ── Progression ──
        public int    Level             { get; set; }
        public long   ExperiencePoints  { get; set; }

        // ── Currency ──
        public int    Gold              { get; set; }
        public int    Silver            { get; set; }
        public int    Copper            { get; set; }

        // ── Timestamps ──
        public string CreatedAt         { get; set; } = string.Empty;
        public string LastPlayedAt      { get; set; } = string.Empty;
        public long   TotalPlayTimeTicks { get; set; }

        // ── Stats ──
        public int    TotalKills        { get; set; }
        public int    TotalDeaths       { get; set; }

        // ── JSON Columns ──
        // Everything that is a nested object or collection
        public string StatsJson         { get; set; } = "{}";
        public string InventoryJson     { get; set; } = "[]";
        public string SkillsJson        { get; set; } = "{}";
        public string EquipmentJson     { get; set; } = "{}";
        public string ActiveStatusesJson { get; set; } = "[]";
    }

    // =====================
    // Equipment DTO
    // =====================
    // A simple bag-of-slots we serialise into EquipmentJson.

    public class EquipmentDto
    {
        public InventoryItem? Weapon    { get; set; }
        public InventoryItem? Offhand   { get; set; }
        public InventoryItem? Helmet    { get; set; }
        public InventoryItem? Chest     { get; set; }
        public InventoryItem? Legs      { get; set; }
        public InventoryItem? Boots     { get; set; }
        public InventoryItem? Gloves    { get; set; }
        public InventoryItem? Ring1     { get; set; }
        public InventoryItem? Ring2     { get; set; }
        public InventoryItem? Amulet    { get; set; }
    }

    // =====================
    // DatabaseService
    // =====================

    public class DatabaseService : IDatabaseService
    {
        private SQLiteAsyncConnection? _db;

        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented        = false,
            PropertyNameCaseInsensitive = true
        };

        // ── Path ──────────────────────────────────────

        private static string DbPath =>
            Path.Combine(
                FileSystem.AppDataDirectory,
                "realmsedge.db3");

        // =====================
        // Initialisation
        // =====================

        public async Task InitialiseAsync()
        {
            if (_db is not null) return;
            // Required by SQLitePCLRaw.bundle_green
            // Must be called before any connection is opened
            SQLitePCL.Batteries_V2.Init();

            _db = new SQLiteAsyncConnection(
                DbPath,
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create   |
                SQLiteOpenFlags.SharedCache);

            await _db.CreateTableAsync<CharacterRow>();
        }

        private SQLiteAsyncConnection Db =>
            _db ?? throw new InvalidOperationException(
                "DatabaseService not initialised. " +
                "Call InitialiseAsync() first.");

        // =====================
        // Characters — Read
        // =====================

        public async Task<List<PlayerCharacter>> GetAllCharactersAsync()
        {
            var rows = await Db.Table<CharacterRow>().ToListAsync();
            return rows.Select(ToCharacter).ToList();
        }

        public async Task<PlayerCharacter?> GetCharacterAsync(Guid id)
        {
            var row = await Db
                .Table<CharacterRow>()
                .Where(r => r.Id == id.ToString())
                .FirstOrDefaultAsync();

            return row is null ? null : ToCharacter(row);
        }

        public async Task<bool> CharacterExistsAsync(Guid id)
        {
            var count = await Db
                .Table<CharacterRow>()
                .Where(r => r.Id == id.ToString())
                .CountAsync();
            return count > 0;
        }

        // =====================
        // Characters — Write
        // =====================

        public async Task SaveCharacterAsync(PlayerCharacter character)
        {
            var row = ToRow(character);
            await Db.InsertOrReplaceAsync(row);
        }

        public async Task DeleteCharacterAsync(Guid id)
        {
            await Db.DeleteAsync<CharacterRow>(id.ToString());
        }

        // =====================
        // Mapping — Model → Row
        // =====================

        private static CharacterRow ToRow(PlayerCharacter c)
        {
            var equipment = new EquipmentDto
            {
                Weapon  = c.EquippedWeapon,
                Offhand = c.EquippedOffhand,
                Helmet  = c.EquippedHelmet,
                Chest   = c.EquippedChest,
                Legs    = c.EquippedLegs,
                Boots   = c.EquippedBoots,
                Gloves  = c.EquippedGloves,
                Ring1   = c.EquippedRing1,
                Ring2   = c.EquippedRing2,
                Amulet  = c.EquippedAmulet,
            };

            return new CharacterRow
            {
                Id                  = c.Id.ToString(),
                PlayerName          = c.PlayerName,
                Name                = c.Name,
                Title               = c.Title,
                Description         = c.Description,
                Race                = (int)c.Race,
                Class               = (int)c.Class,
                Alignment           = (int)c.Alignment,
                Gender              = (int)c.Gender,
                Level               = c.Level,
                ExperiencePoints    = c.ExperiencePoints,
                Gold                = c.Gold,
                Silver              = c.Silver,
                Copper              = c.Copper,
                CreatedAt           = c.CreatedAt.ToString("O"),
                LastPlayedAt        = c.LastPlayedAt.ToString("O"),
                TotalPlayTimeTicks  = c.TotalPlayTime.Ticks,
                TotalKills          = c.TotalKills,
                TotalDeaths         = c.TotalDeaths,
                StatsJson           = Serialize(c.Stats),
                InventoryJson       = Serialize(c.Inventory),
                SkillsJson          = Serialize(c.Skills),
                EquipmentJson       = Serialize(equipment),
                ActiveStatusesJson  = Serialize(c.ActiveStatuses),
            };
        }

        // =====================
        // Mapping — Row → Model
        // =====================

        private static PlayerCharacter ToCharacter(CharacterRow row)
        {
            var c = new PlayerCharacter
            {
                Id               = Guid.Parse(row.Id),
                PlayerName       = row.PlayerName,
                Name             = row.Name,
                Title            = row.Title,
                Description      = row.Description,
                Race             = (CharacterRace)row.Race,
                Class            = (CharacterClass)row.Class,
                Alignment        = (Alignment)row.Alignment,
                Gender           = (Gender)row.Gender,
                Level            = row.Level,
                ExperiencePoints = row.ExperiencePoints,
                Gold             = row.Gold,
                Silver           = row.Silver,
                Copper           = row.Copper,
                TotalKills       = row.TotalKills,
                TotalDeaths      = row.TotalDeaths,
                TotalPlayTime    = TimeSpan.FromTicks(row.TotalPlayTimeTicks),
            };

            if (DateTime.TryParse(row.CreatedAt, out var created))
                c.CreatedAt = created;

            if (DateTime.TryParse(row.LastPlayedAt, out var lastPlayed))
                c.LastPlayedAt = lastPlayed;

            // ── Deserialise JSON columns ──

            if (Deserialize<CharacterStats>(row.StatsJson) is { } stats)
                c.Stats = stats;

            if (Deserialize<List<InventoryItem>>(row.InventoryJson) is { } inv)
                c.Inventory = inv;

            if (Deserialize<Dictionary<string, int>>(row.SkillsJson) is { } skills)
                c.Skills = skills;

            if (Deserialize<List<CharacterStatus>>(row.ActiveStatusesJson) is { } statuses)
                c.ActiveStatuses = statuses;

            if (Deserialize<EquipmentDto>(row.EquipmentJson) is { } eq)
            {
                c.EquippedWeapon  = eq.Weapon;
                c.EquippedOffhand = eq.Offhand;
                c.EquippedHelmet  = eq.Helmet;
                c.EquippedChest   = eq.Chest;
                c.EquippedLegs    = eq.Legs;
                c.EquippedBoots   = eq.Boots;
                c.EquippedGloves  = eq.Gloves;
                c.EquippedRing1   = eq.Ring1;
                c.EquippedRing2   = eq.Ring2;
                c.EquippedAmulet  = eq.Amulet;
            }

            return c;
        }

        // =====================
        // JSON Helpers
        // =====================

        private static string Serialize<T>(T obj)
        {
            try   { return JsonSerializer.Serialize(obj, _json); }
            catch { return typeof(T).IsValueType ? "{}" : "[]";  }
        }

        private static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try   { return JsonSerializer.Deserialize<T>(json, _json); }
            catch { return default; }
        }
    }
}

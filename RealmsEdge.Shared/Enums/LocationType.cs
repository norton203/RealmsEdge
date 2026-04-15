namespace RealmsEdge.Shared.Enums
{
    public enum LocationType
    {
        // =====================
        // Safe Zones
        // =====================

        Town,               // Main hub, shops, quests, inn, safe
        Village,            // Smaller settlement, limited services
        City,               // Large hub, guilds, multiple districts
        Castle,             // Noble residence, political quests
        Inn,                // Rest, rumours, tavern games (L.O.R.D nod)
        Temple,             // Healing, resurrection, holy quests
        Guild,              // Class specific training and quests

        // =====================
        // Dungeons
        // =====================

        Dungeon,            // Classic underground dungeon
        Cave,               // Natural cave system
        Crypt,              // Undead heavy, cursed loot
        Tower,              // Mage tower, arcane enemies
        Ruins,              // Ancient crumbled structure
        Sewers,             // Urban dungeon, rogues and rats
        Lair,               // Boss creature home territory

        // =====================
        // Wilderness
        // =====================

        Forest,             // Rangers feel at home, ambush encounters
        Mountains,          // Hard terrain, dwarves and giants
        Swamp,              // Poison heavy, disease risk
        Desert,             // Stamina drain, heat encounters
        Tundra,             // Cold damage, sparse encounters
        Plains,             // Open travel, bandit encounters
        Coast,              // Sea encounters, pirate quests

        // =====================
        // Special
        // =====================

        Portal,             // Fast travel point between locations
        BattleGround,       // PvP or large scale combat zone
        HiddenArea,         // Discovered only by high perception
        DivineRealm,        // Holy or demonic plane, endgame content
    }
}
namespace RealmsEdge.Shared.Enums
{
    public enum QuestType
    {
        // =====================
        // Combat Quests
        // =====================

        Kill,               // Slay a specific number of enemies
        BossKill,           // Defeat a named boss enemy
        Exterminate,        // Clear an entire location of enemies
        Bounty,             // Hunt a specific named target
        Survive,            // Survive waves of enemies

        // =====================
        // Collection Quests
        // =====================

        Fetch,              // Retrieve a specific item
        Collect,            // Gather multiple items
        Craft,              // Craft a specific item
        Steal,              // Rogue aligned — take without buying
        Deliver,            // Take item from A to B

        // =====================
        // Exploration Quests
        // =====================

        Explore,            // Discover a new location
        MapArea,            // Reveal a region of the world map
        FindSecret,         // Locate a hidden area or item
        Investigate,        // Search for clues in a location
        Patrol,             // Visit multiple locations

        // =====================
        // Social Quests
        // =====================

        Escort,             // Protect an NPC to a destination
        Rescue,             // Free a captured NPC
        Negotiate,          // Resolve a conflict without combat
        Persuade,           // Talk an NPC into something
        Reputation,         // Build faction standing

        // =====================
        // Story Quests
        // =====================

        Story,              // Main narrative quest
        ChapterQuest,       // Major story chapter
        SideStory,          // Optional narrative content
        CharacterQuest,     // Class or race specific story

        // =====================
        // L.O.R.D Inspired
        // =====================

        Daily,              // Resets every real world day
        Weekly,             // Resets every real world week
        EventQuest,         // Tied to a world event
        GuildQuest,         // From a specific faction or guild
        RandomQuest         // Procedurally generated quest
    }
}
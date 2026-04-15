namespace RealmsEdge.Shared.Enums
{
    public enum GameState
    {
        // =====================
        // Application States
        // =====================

        Initialising,       // App starting up, loading assets
        MainMenu,           // Main menu screen
        Loading,            // Loading screen between states
        Saving,             // Saving game data

        // =====================
        // Character States
        // =====================

        CharacterSelect,    // Choose existing character
        CharacterCreate,    // Creating a new character
        CharacterLevel,     // Level up screen
        CharacterSheet,     // Viewing character stats

        // =====================
        // World States
        // =====================

        WorldMap,           // Viewing the world map
        Travelling,         // Moving between locations
        Exploring,          // Moving room to room
        Resting,            // Taking a rest

        // =====================
        // Interaction States
        // =====================

        Dialogue,           // Talking to an NPC
        Shopping,           // At a merchant
        Training,           // At a trainer
        Banking,            // At a bank
        Crafting,           // Crafting items

        // =====================
        // Quest States
        // =====================

        QuestJournal,       // Viewing quest log
        QuestComplete,      // Quest completion screen
        QuestFailed,        // Quest failure screen

        // =====================
        // Combat States
        // =====================

        CombatStart,        // Combat beginning, initiative roll
        CombatPlayerTurn,   // Waiting for player action
        CombatEnemyTurn,    // Enemy AI processing
        CombatResolving,    // Action being resolved
        CombatVictory,      // Combat won
        CombatDefeat,       // Combat lost
        CombatFled,         // Escaped combat

        // =====================
        // Party States
        // =====================

        PartyLobby,         // Forming or managing party
        PartyInvite,        // Sending or receiving invite

        // =====================
        // End States
        // =====================

        GameOver,           // Character died permanently
        Victory,            // Game completed
        Paused              // Game paused
    }
}
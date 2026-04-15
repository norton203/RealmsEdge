namespace RealmsEdge.Shared.Enums
{
    public enum EncounterType
    {
        // =====================
        // Combat
        // =====================

        Combat,             // Standard enemy fight
        EliteCombat,        // Stronger enemy group
        BossCombat,         // Named boss encounter
        Ambush,             // Enemies get a surprise round
        Patrol,             // Moving enemy group

        // =====================
        // Non Combat
        // =====================

        NpcDialogue,        // Friendly NPC interaction
        Merchant,           // Travelling merchant
        QuestGiver,         // Quest available here
        Companion,          // Potential party member found here

        // =====================
        // Environmental
        // =====================

        Trap,               // Hidden trap, dexterity or perception check
        Puzzle,             // Logic puzzle blocks progress
        SecretDoor,         // Hidden exit, perception check to find
        HazardZone,         // Ongoing damage area, fire/poison/cold

        // =====================
        // Reward
        // =====================

        Treasure,           // Loot chest or hidden cache
        RareItem,           // Guaranteed rare or better item
        GoldCache,          // Large gold reward
        Shrine,             // Stat boost or blessing

        // =====================
        // Story
        // =====================

        CutScene,           // Story moment, no interaction
        Choice,             // Moral decision with consequences
        RandomEvent,        // L.O.R.D inspired random world event
        LoreEntry           // World building text, optional reading
    }
}
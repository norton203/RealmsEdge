namespace RealmsEdge.Shared.Enums
{
    public enum QuestStatus
    {
        // =====================
        // Pre Acceptance
        // =====================

        Hidden,             // Not yet discovered by player
        Available,          // Can be accepted from quest giver
        Locked,             // Requirements not yet met

        // =====================
        // Active
        // =====================

        Active,             // Currently being pursued
        OnHold,             // Paused, waiting for condition
        ReadyToComplete,    // All objectives done, return to NPC

        // =====================
        // Resolved
        // =====================

        Completed,          // Successfully finished
        Failed,             // Time ran out or conditions failed
        Abandoned,          // Player gave up
        Expired             // Was available but time ran out
    }
}
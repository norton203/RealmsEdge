namespace RealmsEdge.Shared.Enums
{
    public enum Gender
    {
        // =====================
        // Player Choices
        // =====================

        Male,               // He/Him pronouns in dialogue
        Female,             // She/Her pronouns in dialogue
        NonBinary,          // They/Them pronouns in dialogue
        Unspecified,        // No pronoun preference, name only in dialogue

        // =====================
        // NPC / Creature Only
        // =====================

        None,               // Used for monsters, undead, constructs
                            // Example: Skeleton, Golem, Demon spawn
    }
}
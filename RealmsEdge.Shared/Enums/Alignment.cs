namespace RealmsEdge.Shared.Enums
{
    public enum Alignment
    {
        // =====================
        // Lawful
        // =====================

        LawfulGood,     // Honour, compassion, follows rules to help others
                        // Example: Paladin, town guard captain
                        // Effect: Best prices in shops, quests from nobles

        LawfulNeutral,  // Order above all, neither kind nor cruel
                        // Example: Judge, soldier following orders
                        // Effect: Neutral reactions, law based quests only

        LawfulEvil,     // Uses rules and power for personal gain
                        // Example: Corrupt king, crime lord
                        // Effect: Access to darker quests, feared by commoners

        // =====================
        // Neutral
        // =====================

        NeutralGood,    // Does good without bias toward law or chaos
                        // Example: Wandering healer, helpful hermit
                        // Effect: Welcomed most places, no extremes

        TrueNeutral,    // Balance in all things, druid philosophy
                        // Example: Druid, nature spirit
                        // Effect: Access to nature quests, neutral to all factions

        NeutralEvil,    // Pure self interest, no moral code
                        // Example: Mercenary, opportunist
                        // Effect: Will take any job, trusted by nobody fully

        // =====================
        // Chaotic
        // =====================

        ChaoticGood,    // Free spirit, does right by their own rules
                        // Example: Robin Hood, roguish hero
                        // Effect: Loved by common folk, distrusted by nobility

        ChaoticNeutral, // Random, unpredictable, follows own whims
                        // Example: Wild mage, trickster
                        // Effect: Unpredictable quest outcomes, wild surge bonuses

        ChaoticEvil,    // Destruction and cruelty for its own sake
                        // Example: Demon, chaos warrior
                        // Effect: Feared by all, access to darkest content
    }
}
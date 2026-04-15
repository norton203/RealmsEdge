namespace RealmsEdge.Shared.Enums
{
    public enum CharacterRace
    {
        // =====================
        // Common Races
        // =====================

        Human,          // Versatile, balanced stats, extra skill point
        Elf,            // High dexterity, intelligence, low constitution
        Dwarf,          // High constitution, strength, low charisma
        Halfling,       // High dexterity, charisma, low strength
        Gnome,          // High intelligence, low strength, magic affinity

        // =====================
        // Half Breeds
        // =====================

        HalfElf,        // Balanced elf/human traits, high charisma
        HalfOrc,        // High strength, constitution, low intelligence

        // =====================
        // Warhammer / Dark Races
        // =====================

        Orc,            // Very high strength, very low intelligence
        Goblin,         // High dexterity, very low strength/constitution
        Undead,         // No constitution, immune to poison, high dark magic
        Vampire,        // High charisma, dexterity, weakness to holy/sunlight
        Demon,          // High strength, intelligence, chaotic alignment only

        // =====================
        // High Fantasy Races
        // =====================

        Dragonborn,     // High strength, constitution, breath weapon ability
        Tiefling,       // High intelligence, charisma, dark magic affinity
        Aasimar,        // High charisma, wisdom, holy magic affinity
    }
}
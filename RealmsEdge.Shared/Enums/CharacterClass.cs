namespace RealmsEdge.Shared.Enums
{
    public enum CharacterClass
    {
        // =====================
        // Warrior Classes
        // =====================

        Fighter,        // Master of weapons and armour, high HP, D10 hit die
        Paladin,        // Holy warrior, heals and fights, D10 hit die
        Ranger,         // Skilled hunter, ranged combat, dual wield, D10 hit die
        Barbarian,      // Rage mechanic, highest HP, D12 hit die

        // =====================
        // Rogue Classes
        // =====================

        Rogue,          // Stealth, backstab, lockpicking, D8 hit die
        Bard,           // Charisma based, buffs party, D8 hit die
        Monk,           // Unarmed combat, ki points, D8 hit die
        Assassin,       // High single hit damage, poison use, D8 hit die

        // =====================
        // Mage Classes
        // =====================

        Mage,           // Pure arcane magic, very low HP, D6 hit die
        Sorcerer,       // Innate magic, wild surge table, D6 hit die
        Warlock,        // Pact magic, dark patron, D8 hit die
        Necromancer,    // Raises undead, dark magic, D6 hit die
        Illusionist,    // Deception and mind magic, D6 hit die

        // =====================
        // Cleric Classes
        // =====================

        Cleric,         // Divine healing and combat, D8 hit die
        Druid,          // Nature magic, shapeshifting, D8 hit die
        Shaman,         // Spirit magic, buffs, elemental, D8 hit die

        // =====================
        // Warhammer Inspired
        // =====================

        Witchhunter,    // Anti-magic specialist, high will saves, D10 hit die
        Chaos,          // Unpredictable, wild magic, dark alignment, D8 hit die
        BountyHunter,   // Tracker, trapper, mercenary skills, D10 hit die
    }
}
namespace RealmsEdge.Shared.Enums
{
    public enum DamageType
    {
        // =====================
        // Physical
        // =====================

        Slashing,           // Swords, axes — reduced by heavy armour
        Piercing,           // Daggers, arrows — bypasses some armour
        Bludgeoning,        // Maces, hammers — effective vs undead

        // =====================
        // Elemental
        // =====================

        Fire,               // Burns, applies Burning status
        Ice,                // Slows, applies Frozen status
        Lightning,          // Ignores metal armour bonus
        Acid,               // Reduces armour class over time
        Poison,             // Applies Poisoned status
        Thunder,            // Area effect, knocks back

        // =====================
        // Magical
        // =====================

        Arcane,             // Pure magic, no physical resistance
        Holy,               // Extra damage to undead and demons
        Shadow,             // Extra damage to living, heals undead
        Necrotic,           // Reduces max HP temporarily
        Psychic,            // Targets wisdom, ignores physical armour
        Force,              // Pure magical force, never resisted

        // =====================
        // Special
        // =====================

        True,               // Ignores ALL resistances — very rare
        Chaos,              // Random damage type each hit
        Bleed,              // Applies Bleeding status
        Curse               // Applies Cursed status
    }
}
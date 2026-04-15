namespace RealmsEdge.Shared.Enums
{
    public enum CombatActionType
    {
        // =====================
        // Attack Actions
        // =====================

        MeleeAttack,        // Standard weapon attack, uses Strength
        RangedAttack,       // Bow or thrown, uses Dexterity
        UnarmedStrike,      // Fists, monk attack, uses Strength
        DualWield,          // Two weapon attack, bonus action
        PowerAttack,        // High damage, lower accuracy
        PrecisionAttack,    // High accuracy, lower damage

        // =====================
        // Magic Actions
        // =====================

        CastSpell,          // Standard spell cast, uses Mana
        QuickSpell,         // Bonus action spell, half mana cost
        ChannelDivinity,    // Cleric / Paladin divine power
        WildSurge,          // Chaos class — unpredictable magic

        // =====================
        // Defensive Actions
        // =====================

        Defend,             // +4 AC until next turn, no attack
        Dodge,              // Full dodge attempt, uses Stamina
        Parry,              // Weapon parry, reduces damage
        Shield,             // Shield bash, stuns on hit
        Brace,              // Prepare for hit, reduces damage

        // =====================
        // Support Actions
        // =====================

        HealAlly,           // Restore HP to party member
        BuffAlly,           // Apply positive status to ally
        DebuffEnemy,        // Apply negative status to enemy
        TauntEnemy,         // Force enemy to target you
        BardSong,           // Bard party wide buff
        BattleCry,          // Warrior morale boost to party

        // =====================
        // Item Actions
        // =====================

        UsePotion,          // Consume a potion
        UseScroll,          // Cast from a scroll, one use
        ThrowItem,          // Throw an item as attack or utility
        UseSpecialItem,     // Unique item ability

        // =====================
        // Movement Actions
        // =====================

        Flee,               // Attempt to escape combat
        Reposition,         // Move to better position
        TakeCover,          // Reduce ranged damage taken

        // =====================
        // Special Actions
        // =====================

        Rage,               // Barbarian rage activation
        Stealth,            // Attempt to hide mid combat
        Pickpocket,         // Steal from enemy mid combat
        Intimidate,         // Charisma check to frighten enemy
        Negotiate,          // Attempt to end fight peacefully
        SummonAlly,         // Necromancer / Warlock summon
        Sacrifice,          // Dark magic — spend HP for power

        // =====================
        // Passive
        // =====================

        Wait,               // Skip turn, act later
        Guard               // Protect an ally, intercept attacks
    }
}
namespace RealmsEdge.Shared.Enums
{
    public enum CharacterStatus
    {
        // =====================
        // Normal States
        // =====================

        Healthy,            // No conditions, full combat effectiveness
        Resting,            // Recovering HP/Mana, cannot act
        Dead,               // HP = 0, requires resurrection spell or item
        Unconscious,        // HP below threshold, stable but cannot act
                            // Dice: D20 Constitution check each turn to stabilise

        // =====================
        // Physical Conditions
        // =====================

        Poisoned,           // Loses HP each turn, reduced strength
                            // Dice: D20 Constitution check to resist each turn
                            // Cured by: Antidote, Cleric spell, Rest

        Bleeding,           // Loses HP each turn until bound or healed
                            // Dice: D6 damage per turn
                            // Cured by: Bandage, Heal spell

        Burning,            // Fire damage each turn, spreads to others
                            // Dice: D6 fire damage per turn
                            // Cured by: Water, ice spell, rolling action

        Frozen,             // Cannot move, takes extra damage from physical hits
                            // Dice: D20 Constitution check to break free
                            // Cured by: Fire damage, Thaw spell

        Paralysed,          // Cannot act at all, auto fails dexterity saves
                            // Dice: D20 Constitution check each turn to recover
                            // Cured by: Cleric spell, specific antidote

        Weakened,           // Strength and attack reduced by half
                            // Duration: D4 turns
                            // Cured by: Buff spell, rest

        Diseased,           // Slowly reduces all stats over time
                            // Dice: D20 Constitution check daily to resist spread
                            // Cured by: Cleric spell, rare herbs

        // =====================
        // Mental Conditions
        // =====================

        Stunned,            // Loses next turn, reduced armour class
                            // Dice: D20 Constitution check to recover early
                            // Cured by: Time, Clarity spell

        Feared,             // Forced to flee combat for D4 turns
                            // Dice: D20 Wisdom check to resist fear
                            // Cured by: Bard song, Courage spell

        Confused,           // Random actions each turn, may attack allies
                            // Dice: D6 to determine action: 1-2 attack ally,
                            //       3-4 do nothing, 5-6 act normally
                            // Cured by: Clarity spell, Wisdom check

        Charmed,            // Treats enemy as ally, will not attack them
                            // Dice: D20 Wisdom check each turn to break
                            // Cured by: Dispel Magic, taking damage from charmer

        Berserk,            // Must attack nearest target, friend or foe
                            // Dice: D20 Wisdom check to regain control
                            // Note: Barbarian rage is a controlled version

        // =====================
        // Magical Conditions
        // =====================

        Cursed,             // Reduces all dice rolls by 2, bad luck events
                            // Dice: All rolls at disadvantage
                            // Cured by: Remove Curse spell, holy shrine

        Silenced,           // Cannot cast spells or use verbal abilities
                            // Duration: D4 turns
                            // Cured by: Dispel Magic, duration expiry

        Invisible,          // Cannot be targeted directly, bonus to stealth
                            // Dice: Attackers roll at disadvantage
                            // Broken by: Attacking or casting

        Hasted,             // Extra action per turn, increased speed
                            // Duration: D6 turns
                            // Source: Haste spell, speed potion

        Slowed,             // Half movement, one action per two turns
                            // Duration: D4 turns
                            // Cured by: Dispel Magic, Haste spell

        Blessed,            // All dice rolls +2, positive luck events
                            // Duration: D6 turns
                            // Source: Cleric spell, holy shrine

        Shielded,           // Temporary armour class bonus
                            // Bonus: +4 AC for D4 turns
                            // Source: Shield spell, Guardian ability

        // =====================
        // Warhammer Dark Conditions
        // =====================

        Corrupted,          // Slowly shifting toward chaos alignment
                            // Dice: D20 Wisdom check daily to resist
                            // Cured by: Witchhunter ritual, holy water

        Possessed,          // Demon controls character actions
                            // Dice: D20 Wisdom check each turn to resist
                            // Cured by: Exorcism, holy weapon hit

        Marked,             // Chaos mark visible, NPCs react with fear/hostility
                            // Duration: Until removed by ritual
                            // Effect: Price increases, some quests unavailable

        Drained,            // Undead life drain, max HP reduced until long rest
                            // Dice: D6 max HP lost per drain hit
                            // Cured by: Restoration spell, long rest at inn
    }
}
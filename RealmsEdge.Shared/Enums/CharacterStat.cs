namespace RealmsEdge.Shared.Enums
{
    public enum CharacterStat
    {
        // =====================
        // Core Stats (D&D Classic Six)
        // =====================

        Strength,           // Physical power, melee damage, carry weight
                            // Primary for: Fighter, Barbarian, Paladin
                            // Dice: D20 + modifier vs enemy armour class

        Dexterity,          // Speed, agility, ranged attack, armour class
                            // Primary for: Rogue, Ranger, Assassin, Monk
                            // Dice: D20 + modifier vs trap/dodge checks

        Constitution,       // Health points, stamina, poison resistance
                            // Primary for: All classes, especially Barbarian
                            // Dice: D20 + modifier vs endurance checks

        Intelligence,       // Spell power, skill points, lore knowledge
                            // Primary for: Mage, Necromancer, Illusionist
                            // Dice: D20 + modifier vs arcane checks

        Wisdom,             // Perception, willpower, divine magic power
                            // Primary for: Cleric, Druid, Shaman
                            // Dice: D20 + modifier vs mind/perception checks

        Charisma,           // Leadership, persuasion, NPC reactions
                            // Primary for: Bard, Paladin, Sorcerer
                            // Dice: D20 + modifier vs social checks

        // =====================
        // Derived Stats (calculated, not rolled)
        // =====================

        HitPoints,          // Max HP = Constitution modifier x hit die
                            // Rolled on level up using class hit die

        Mana,               // Spell points = Intelligence + Wisdom modifier
                            // Regenerates on rest

        Stamina,            // Physical action points = Constitution modifier
                            // Depletes in combat, recovers on short rest

        ArmourClass,        // Defence = 10 + Dexterity modifier + armour bonus
                            // Enemy must beat this to land a hit

        Initiative,         // Turn order = D20 + Dexterity modifier
                            // Rolled at combat start

        Speed,              // Movement per turn in tiles
                            // Base 6, modified by race and equipment

        // =====================
        // Secondary Stats
        // =====================

        AttackBonus,        // Melee hit chance = Strength modifier + level bonus
        RangedBonus,        // Ranged hit chance = Dexterity modifier + level bonus
        SpellBonus,         // Spell hit/power = Intelligence or Wisdom modifier
        CriticalChance,     // % chance to land a critical hit, D20 natural 20 base
        DodgeChance,        // % chance to fully avoid an attack
        ResistPoison,       // Constitution based poison resistance
        ResistMagic,        // Wisdom based magic resistance
        ResistFear,         // Wisdom based fear resistance
        Luck,               // L.O.R.D inspired — affects random events and loot
    }
}
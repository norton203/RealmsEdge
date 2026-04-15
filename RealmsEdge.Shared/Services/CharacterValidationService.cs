using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models.Characters;

namespace RealmsEdge.Shared.Services
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failure(params string[] errors) => new()
        {
            IsValid = false,
            Errors  = errors.ToList()
        };

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public void AddWarning(string warning)
            => Warnings.Add(warning);

        public string ErrorSummary => string.Join(", ", Errors);
    }

    public class CharacterValidationService
    {
        // =====================
        // Main Validation Entry
        // =====================

        public ValidationResult ValidateNewCharacter(
            string characterName,
            CharacterRace race,
            CharacterClass charClass,
            Alignment alignment,
            Gender gender,
            List<int> statValues)
        {
            var result = new ValidationResult { IsValid = true };

            ValidateName(characterName, result);
            ValidateRaceClassCombination(race, charClass, result);
            ValidateAlignmentRestrictions(charClass, alignment, result);
            ValidateRaceAlignmentRestrictions(race, alignment, result);
            ValidateStats(statValues, result);
            AddWarnings(race, charClass, alignment, result);

            return result;
        }

        // =====================
        // Name Validation
        // =====================

        private static void ValidateName(string name, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                result.AddError("Character name cannot be empty.");
                return;
            }

            if (name.Length < 2)
                result.AddError("Character name must be at least 2 characters.");

            if (name.Length > 24)
                result.AddError("Character name cannot exceed 24 characters.");

            if (!name.All(c => char.IsLetter(c) || c == '\'' || c == '-' || c == ' '))
                result.AddError("Character name can only contain letters, spaces, hyphens and apostrophes.");

            if (name.StartsWith(' ') || name.EndsWith(' '))
                result.AddError("Character name cannot start or end with a space.");

            // Reserved NPC names
            var reservedNames = new[]
            {
                "Admin", "System", "God", "Devil", "Satan",
                "Narrator", "DungeonMaster", "DM"
            };

            if (reservedNames.Any(r => r.Equals(name, StringComparison.OrdinalIgnoreCase)))
                result.AddError($"'{name}' is a reserved name and cannot be used.");
        }

        // =====================
        // Race / Class Combinations
        // =====================

        private static void ValidateRaceClassCombination(
            CharacterRace race,
            CharacterClass charClass,
            ValidationResult result)
        {
            // Classes forbidden to certain races
            var forbiddenCombinations = new Dictionary<CharacterRace, List<CharacterClass>>
            {
                [CharacterRace.Orc] = new()
                {
                    CharacterClass.Mage,
                    CharacterClass.Illusionist,
                    CharacterClass.Bard
                },
                [CharacterRace.Goblin] = new()
                {
                    CharacterClass.Paladin,
                    CharacterClass.Cleric
                    
                },
                [CharacterRace.Undead] = new()
                {
                    CharacterClass.Paladin,
                    CharacterClass.Cleric,
                    CharacterClass.Druid
                },
                [CharacterRace.Demon] = new()
                {
                    CharacterClass.Paladin,
                    CharacterClass.Cleric
                    
                },
                [CharacterRace.Aasimar] = new()
                {
                    CharacterClass.Necromancer,
                    CharacterClass.Chaos,
                    CharacterClass.Warlock
                },
                [CharacterRace.Halfling] = new()
                {
                    CharacterClass.Barbarian,
                    CharacterClass.Chaos
                },
            };

            if (forbiddenCombinations.TryGetValue(race, out var forbidden)
                && forbidden.Contains(charClass))
            {
                result.AddError(
                    $"{race} cannot be a {charClass}. " +
                    $"This combination is not permitted in Realm's Edge.");
            }

            // Classes exclusive to certain races
            var exclusiveCombinations = new Dictionary<CharacterClass, List<CharacterRace>>
            {
                [CharacterClass.Chaos] = new()
                {
                    CharacterRace.Demon,
                    CharacterRace.Orc,
                    CharacterRace.HalfOrc,
                    CharacterRace.Tiefling,
                    CharacterRace.Undead,
                    CharacterRace.Vampire
                },
                [CharacterClass.Witchhunter] = new()
                {
                    CharacterRace.Human,
                    CharacterRace.HalfElf,
                    CharacterRace.Dwarf
                }
            };

            if (exclusiveCombinations.TryGetValue(charClass, out var allowed)
                && !allowed.Contains(race))
            {
                result.AddError(
                    $"Only specific races can be a {charClass}. " +
                    $"{race} does not qualify.");
            }
        }

        // =====================
        // Class Alignment Restrictions
        // =====================

        private static void ValidateAlignmentRestrictions(
            CharacterClass charClass,
            Alignment alignment,
            ValidationResult result)
        {
            // Paladins must be Lawful Good — classic D&D rule
            if (charClass == CharacterClass.Paladin &&
                alignment != Alignment.LawfulGood &&
                alignment != Alignment.LawfulNeutral &&
                alignment != Alignment.NeutralGood)
            {
                result.AddError(
                    "Paladins must have a Good or Lawful alignment. " +
                    "Their holy oath forbids darker paths.");
            }

            // Druids must be some form of Neutral
            if (charClass == CharacterClass.Druid &&
                alignment != Alignment.TrueNeutral &&
                alignment != Alignment.NeutralGood &&
                alignment != Alignment.NeutralEvil &&
                alignment != Alignment.LawfulNeutral &&
                alignment != Alignment.ChaoticNeutral)
            {
                result.AddError(
                    "Druids must have a Neutral alignment. " +
                    "Nature demands balance above all else.");
            }

            // Chaos class must be Chaotic
            if (charClass == CharacterClass.Chaos &&
                alignment != Alignment.ChaoticEvil &&
                alignment != Alignment.ChaoticNeutral)
            {
                result.AddError(
                    "The Chaos class demands a Chaotic alignment. " +
                    "Order is their enemy.");
            }

            // Witchhunters cannot be Chaotic Evil
            if (charClass == CharacterClass.Witchhunter &&
                alignment == Alignment.ChaoticEvil)
            {
                result.AddError(
                    "Witchhunters cannot be Chaotic Evil. " +
                    "They exist to destroy chaos, not embody it.");
            }

            // Necromancers cannot be Lawful Good
            if (charClass == CharacterClass.Necromancer &&
                alignment == Alignment.LawfulGood)
            {
                result.AddError(
                    "Necromancers cannot be Lawful Good. " +
                    "The raising of the dead is inherently a dark art.");
            }
        }

        // =====================
        // Race Alignment Restrictions
        // =====================

        private static void ValidateRaceAlignmentRestrictions(
            CharacterRace race,
            Alignment alignment,
            ValidationResult result)
        {
            // Demons must be Chaotic
            if (race == CharacterRace.Demon &&
                alignment != Alignment.ChaoticEvil &&
                alignment != Alignment.ChaoticNeutral)
            {
                result.AddError(
                    "Demons are creatures of chaos and cannot follow " +
                    "a Lawful or Neutral path.");
            }

            // Aasimar cannot be Evil
            if (race == CharacterRace.Aasimar &&
                (alignment == Alignment.LawfulEvil  ||
                 alignment == Alignment.NeutralEvil ||
                 alignment == Alignment.ChaoticEvil))
            {
                result.AddError(
                    "Aasimar are touched by celestial power and " +
                    "cannot follow an Evil alignment.");
            }

            // Undead lean toward evil or neutral
            if (race == CharacterRace.Undead &&
                (alignment == Alignment.LawfulGood ||
                 alignment == Alignment.NeutralGood ||
                 alignment == Alignment.ChaoticGood))
            {
                result.AddWarning(
                    "Undead with a Good alignment is unusual. " +
                    "Expect strong NPC hostility throughout the world.");
            }
        }

        // =====================
        // Stat Validation
        // =====================

        private static void ValidateStats(
            List<int> statValues,
            ValidationResult result)
        {
            if (statValues == null || statValues.Count < 6)
            {
                result.AddError("All six core stats must be assigned.");
                return;
            }

            foreach (var stat in statValues)
            {
                if (stat < 3)
                    result.AddError($"No stat can be below 3.");

                if (stat > 20)
                    result.AddError($"No stat can exceed 20 before racial bonuses.");
            }

            var total = statValues.Sum();
            if (total < 60)
                result.AddWarning(
                    "Your total stats are very low. " +
                    "This will be a challenging adventure.");

            if (total > 90)
                result.AddWarning(
                    "Your stats are exceptionally high. " +
                    "Fortune has smiled upon you, adventurer.");
        }

        // =====================
        // Informational Warnings
        // =====================

        private static void AddWarnings(
            CharacterRace race,
            CharacterClass charClass,
            Alignment alignment,
            ValidationResult result)
        {
            // Alignment based world warnings
            if (alignment == Alignment.ChaoticEvil)
                result.AddWarning(
                    "Chaotic Evil characters will be attacked on sight " +
                    "in most towns. The world fears you.");

            if (alignment == Alignment.LawfulGood)
                result.AddWarning(
                    "Lawful Good characters receive discounts in shops " +
                    "and are welcomed in most settlements.");

            // Difficult race warnings
            if (race == CharacterRace.Demon || race == CharacterRace.Undead)
                result.AddWarning(
                    $"{race} characters face extreme hostility from " +
                    $"most NPCs. Only the brave or foolish walk this path.");

            if (race == CharacterRace.Vampire)
                result.AddWarning(
                    "Vampires cannot enter buildings without invitation " +
                    "and suffer in daylight zones.");

            // Class difficulty warnings
            if (charClass == CharacterClass.Chaos)
                result.AddWarning(
                    "The Chaos class has unpredictable abilities. " +
                    "Wild surges can help or harm without warning.");

            if (charClass == CharacterClass.Necromancer)
                result.AddWarning(
                    "Necromancers are unwelcome in holy settlements. " +
                    "Clerics and Paladins will react with hostility.");

            // Unusual but valid combinations
            if (race == CharacterRace.Halfling &&
                charClass == CharacterClass.Fighter)
                result.AddWarning(
                    "A Halfling Fighter is an unusual sight. " +
                    "Enemies may underestimate you — use that.");

            if (race == CharacterRace.Orc &&
                charClass == CharacterClass.Bard)
                result.AddError(
                    "Even in Realm's Edge, an Orc Bard is a step too far.");
        }

        // =====================
        // Individual Validators
        // (used by UI for live validation)
        // =====================

        public ValidationResult ValidateName(string name)
        {
            var result = new ValidationResult { IsValid = true };
            ValidateName(name, result);
            return result;
        }

        public ValidationResult ValidateRaceClass(
            CharacterRace race, CharacterClass charClass)
        {
            var result = new ValidationResult { IsValid = true };
            ValidateRaceClassCombination(race, charClass, result);
            return result;
        }

        public ValidationResult ValidateAlignment(
            CharacterClass charClass,
            CharacterRace race,
            Alignment alignment)
        {
            var result = new ValidationResult { IsValid = true };
            ValidateAlignmentRestrictions(charClass, alignment, result);
            ValidateRaceAlignmentRestrictions(race, alignment, result);
            return result;
        }

        // =====================
        // Recommended Classes
        // (used by character creation UI)
        // =====================

        public List<CharacterClass> GetRecommendedClasses(CharacterRace race)
            => race switch
            {
                CharacterRace.Human => new() {
                CharacterClass.Fighter, CharacterClass.Paladin,
                CharacterClass.Rogue,   CharacterClass.Ranger },

                CharacterRace.Elf => new() {
                CharacterClass.Ranger,  CharacterClass.Mage,
                CharacterClass.Rogue,   CharacterClass.Illusionist },

                CharacterRace.Dwarf => new() {
                CharacterClass.Fighter, CharacterClass.Cleric,
                CharacterClass.Barbarian },

                CharacterRace.Halfling => new() {
                CharacterClass.Rogue,   CharacterClass.Bard,
                CharacterClass.Ranger },

                CharacterRace.HalfOrc => new() {
                CharacterClass.Barbarian, CharacterClass.Fighter,
                CharacterClass.BountyHunter },

                CharacterRace.Orc => new() {
                CharacterClass.Barbarian, CharacterClass.Fighter,
                CharacterClass.Shaman },

                CharacterRace.Demon => new() {
                CharacterClass.Chaos,   CharacterClass.Warlock,
                CharacterClass.Necromancer },

                CharacterRace.Vampire => new() {
                CharacterClass.Rogue,   CharacterClass.Sorcerer,
                CharacterClass.Assassin },

                CharacterRace.Dragonborn => new() {
                CharacterClass.Fighter, CharacterClass.Sorcerer,
                CharacterClass.Paladin },

                CharacterRace.Tiefling => new() {
                CharacterClass.Warlock, CharacterClass.Sorcerer,
                CharacterClass.Rogue },

                CharacterRace.Aasimar => new() {
                CharacterClass.Paladin, CharacterClass.Cleric,
                CharacterClass.Fighter },

                _ => new() {
                CharacterClass.Fighter, CharacterClass.Rogue,
                CharacterClass.Mage }
            };

        // =====================
        // Recommended Alignments
        // (used by character creation UI)
        // =====================

        public List<Alignment> GetRecommendedAlignments(
            CharacterRace race,
            CharacterClass charClass)
        {
            var recommendations = new List<Alignment>();

            // Class driven recommendations
            switch (charClass)
            {
                case CharacterClass.Paladin:
                    recommendations.Add(Alignment.LawfulGood);
                    recommendations.Add(Alignment.NeutralGood);
                    break;

                case CharacterClass.Druid:
                    recommendations.Add(Alignment.TrueNeutral);
                    recommendations.Add(Alignment.NeutralGood);
                    break;

                case CharacterClass.Rogue or CharacterClass.Assassin:
                    recommendations.Add(Alignment.ChaoticNeutral);
                    recommendations.Add(Alignment.TrueNeutral);
                    recommendations.Add(Alignment.NeutralEvil);
                    break;

                case CharacterClass.Chaos:
                    recommendations.Add(Alignment.ChaoticEvil);
                    recommendations.Add(Alignment.ChaoticNeutral);
                    break;

                case CharacterClass.Necromancer:
                    recommendations.Add(Alignment.NeutralEvil);
                    recommendations.Add(Alignment.ChaoticEvil);
                    break;

                default:
                    recommendations.Add(Alignment.TrueNeutral);
                    recommendations.Add(Alignment.ChaoticGood);
                    recommendations.Add(Alignment.LawfulNeutral);
                    break;
            }

            return recommendations;
        }
    }
}
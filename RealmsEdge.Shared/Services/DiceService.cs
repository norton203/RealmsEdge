using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Models;

namespace RealmsEdge.Shared.Services
{
    public class DiceService
    {
        private readonly Random _random = new();

        // Roll a single type of dice, multiple times with an optional modifier
        public DiceRoll Roll(DiceType diceType, int numberOfDice = 1, int modifier = 0)
        {
            var roll = new DiceRoll
            {
                DiceType = diceType,
                NumberOfDice = numberOfDice,
                Modifier = modifier
            };

            for (int i = 0; i < numberOfDice; i++)
                roll.IndividualResults.Add(_random.Next(1, (int)diceType + 1));

            return roll;
        }

        // Roll with advantage - roll twice, take the highest (D&D 5e rule)
        public DiceRoll RollWithAdvantage(DiceType diceType, int modifier = 0)
        {
            var roll1 = Roll(diceType, 1, modifier);
            var roll2 = Roll(diceType, 1, modifier);
            return roll1.Total >= roll2.Total ? roll1 : roll2;
        }

        // Roll with disadvantage - roll twice, take the lowest (D&D 5e rule)
        public DiceRoll RollWithDisadvantage(DiceType diceType, int modifier = 0)
        {
            var roll1 = Roll(diceType, 1, modifier);
            var roll2 = Roll(diceType, 1, modifier);
            return roll1.Total <= roll2.Total ? roll1 : roll2;
        }

        // Roll 4D6 drop lowest - classic D&D character stat generation
        public DiceRoll RollStatBlock()
        {
            var roll = new DiceRoll
            {
                DiceType = DiceType.D6,
                NumberOfDice = 4,
                Modifier = 0
            };

            for (int i = 0; i < 4; i++)
                roll.IndividualResults.Add(_random.Next(1, 7));

            // Drop the lowest result
            roll.IndividualResults.Remove(roll.IndividualResults.Min());

            return roll;
        }

        // Roll a full character stat array (6 stats, classic RPG)
        public List<DiceRoll> RollFullStatArray()
        {
            return Enumerable.Range(0, 6)
                .Select(_ => RollStatBlock())
                .ToList();
        }

        // Percentage roll - useful for L.O.R.D style chance events
        public bool RollPercentageChance(int successChance)
        {
            return Roll(DiceType.D100).Total <= successChance;
        }
    }
}
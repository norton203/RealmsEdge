using RealmsEdge.Shared.Enums;

namespace RealmsEdge.Shared.Models
{
    public class DiceRoll
    {
        public DiceType DiceType { get; set; }
        public int NumberOfDice { get; set; } = 1;
        public int Modifier { get; set; } = 0;
        public List<int> IndividualResults { get; set; } = new();
        public int Total => IndividualResults.Sum() + Modifier;
        public bool IsCriticalHit => DiceType == DiceType.D20 && IndividualResults.Any(r => r == 20);
        public bool IsCriticalFail => DiceType == DiceType.D20 && IndividualResults.Any(r => r == 1);
        public DateTime RolledAt { get; set; } = DateTime.Now;
        public string RollDescription => BuildDescription();

        private string BuildDescription()
        {
            var diceNotation = $"{NumberOfDice}{DiceType}";
            var modifierText = Modifier != 0 ? $" {(Modifier > 0 ? "+" : "")}{Modifier}" : "";
            var resultText = IndividualResults.Count > 1
                ? $"[{string.Join(", ", IndividualResults)}]"
                : $"{IndividualResults.FirstOrDefault()}";

            return $"{diceNotation}{modifierText} = {resultText} → Total: {Total}";
        }
    }
}
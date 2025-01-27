using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    public class AIPLayerModel
    {
        public string? Address { get; set; }
        public int Size { get; set; }
        public int Weight { get; set; }
        public int Age { get; set; }
        public int UserScore { get; set; }
        public List<AITeamModel> Match { get; set; }
    }
}

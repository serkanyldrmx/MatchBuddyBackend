using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Api.Model
{
    public class AITeamModel
    {
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int PlayerById {  get; set; }
        public List<Player> Player { get; set; }
        public List<AIMatchModel> Match { get; set; }
    }
}

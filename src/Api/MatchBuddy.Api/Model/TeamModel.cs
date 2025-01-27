namespace MatchBuddy.Api.Model
{
    public class TeamModel
    {
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int StadiumId {  get; set; }
        public int PlayerById {  get; set; }
        public List<int> PlayerId { get; set; }
    }
}

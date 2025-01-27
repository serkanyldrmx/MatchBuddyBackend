namespace MatchBuddy.Api.Model
{
    public class StadiumModel
    {
        public string? StadiumName { get; set; }
        public string? Location { get; set; }
        public int? City { get; set; }
        public int? District { get; set; }
        public string? Address { get; set; }
        public TimeOnly? OpeningTime { get; set; }
        public TimeOnly? ClosingTime { get; set; }
        public string? Description { get; set; }
    }
}

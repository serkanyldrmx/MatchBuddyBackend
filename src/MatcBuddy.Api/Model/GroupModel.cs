using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Api.Model
{
    public class GroupModel
    {
        public int GroupId { get; set; }
        public string? GroupName { get; set; }
        public List<int> PlayerList { get; set; }
    }
}

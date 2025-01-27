using MatchBuddy.Core.Entities;

namespace MatchBuddy.Api.Model
{
    public class GetGroupMessageModel:IDto
    {
        public int GroupId { get; set; }
        public int? SendPlayerId { get; set; }
    }
}

using MatcBuddy.Core.Entities;

namespace MatcBuddy.Api.Model
{
    public class GetGroupMessageModel:IDto
    {
        public int GroupId { get; set; }
        public int? SendPlayerId { get; set; }
    }
}

using MatcBuddy.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Entities.DTOs
{
    public class GroupMessageModelDto:IDto
    {
        public int GroupMessageId { get; set; }
        public int GroupId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public int? SendPlayerId { get; set; }
        public string? UserName { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public byte Status { get; set; }
        public string? ImageUrl { get; set; }
    }
}

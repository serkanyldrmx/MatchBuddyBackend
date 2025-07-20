using MatchBuddy.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchBuddy.Entities.DTOs
{
    public class MatchTeamDto:IDto
    {
        public int TeamId { get; set; }
        public int MatchId { get; set; }
        public string TeamName { get; set; }
        public int PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public int UserScore { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}

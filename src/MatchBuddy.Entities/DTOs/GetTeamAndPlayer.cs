using MatchBuddy.Core.Entities;
using MatchBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchBuddy.Entities.DTOs
{
    public class GetTeamAndPlayer : IDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; }
        public List<string> PlayerName { get; set; }
    }
}

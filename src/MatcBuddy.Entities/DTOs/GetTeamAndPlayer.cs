using MatcBuddy.Core.Entities;
using MatcBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Entities.DTOs
{
    public class GetTeamAndPlayer : IDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; }
        public List<int> PlayerId { get; set; }
        public List<string> PlayerName { get; set; }
        public List<string> ProfilePictureUrl { get; set; }
    }
}

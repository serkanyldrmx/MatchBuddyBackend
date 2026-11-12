using MatcBuddy.Core.Entities;
using MatcBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Entities.DTOs
{
    public class GetMatchModel : IDto
    {
        public int MatchId { get; set; }
        public string MatchName { get; set; }
        public int IsActive { get; set; }
    }
}

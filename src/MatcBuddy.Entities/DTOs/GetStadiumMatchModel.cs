using MatcBuddy.Core.Entities;
using MatcBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Entities.DTOs
{
    public class GetStadiumMatchModel : IDto
    {
        public int StadiumId { get; set; }
        public string StadiumName { get; set; }
        public List<GetMatchModel> MatchModel { get; set; }
    }
}

using MatchBuddy.Core.Entities;
using MatchBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchBuddy.Entities.DTOs
{
    public class GetStadiumMatchModel : IDto
    {
        public int StadiumId { get; set; }
        public string StadiumName { get; set; }
        public List<GetMatchModel> MatchModel { get; set; }
    }
}

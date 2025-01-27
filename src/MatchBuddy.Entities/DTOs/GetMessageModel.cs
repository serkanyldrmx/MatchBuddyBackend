using MatchBuddy.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchBuddy.Entities.DTOs
{
    public class GetMessageModel:IDto
    {
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
    }
}

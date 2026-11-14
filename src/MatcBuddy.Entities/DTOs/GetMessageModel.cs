using MatcBuddy.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Entities.DTOs
{
    public class GetMessageModel:IDto
    {
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
    }
}

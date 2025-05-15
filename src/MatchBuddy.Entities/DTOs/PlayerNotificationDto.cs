using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Entities.DTOs
{
    public class PlayerNotificationDto
    {
        public int PlayerNotificationId { get; set; }
        public bool IsRead { get; set; }
        public Notification Notification { get; set; }
    }

}

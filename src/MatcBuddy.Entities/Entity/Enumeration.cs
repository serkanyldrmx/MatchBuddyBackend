using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Entities.Entity
{
    public enum NotificationType
    {
        Message,
        MatchJoin,
        MatchApproval,
        MatchRejection,
        StadiumAdded,
        MatchCreated,
        // Yeni değerler sona eklenir; mevcut tamsayı değerleri kaymamalı
        MatchReminder = 6,
        WaitlistPromoted = 7,
        MatchResult = 8,
        NewFollower = 9,
        MatchInvite = 10,
        TeamInvite = 11,
        TournamentUpdate = 12,
        ReservationRequest = 13,
        VenueReview = 14,
        SlotDeal = 15
    }
}

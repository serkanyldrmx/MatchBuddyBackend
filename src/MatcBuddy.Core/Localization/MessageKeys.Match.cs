namespace MatcBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        public static partial class Match
        {
            // Başarı / bilgi
            public const string Joined = "MSG_MATCH_JOINED";
            public const string JoinedWaitlist = "MSG_MATCH_JOINED_WAITLIST";
            public const string Left = "MSG_MATCH_LEFT";
            public const string LeftWaitlist = "MSG_MATCH_LEFT_WAITLIST";
            public const string Confirmed = "MSG_MATCH_CONFIRMED";
            public const string SideUpdated = "MSG_MATCH_SIDE_UPDATED";
            public const string LineupUpdated = "MSG_MATCH_LINEUP_UPDATED";
            public const string GuestAdded = "MSG_MATCH_GUEST_ADDED";
            public const string GuestUpdated = "MSG_MATCH_GUEST_UPDATED";
            public const string GuestRemoved = "MSG_MATCH_GUEST_REMOVED";
            public const string ResultSaved = "MSG_MATCH_RESULT_SAVED";
            public const string MvpVoted = "MSG_MATCH_MVP_VOTED";
            public const string RatingsSaved = "MSG_MATCH_RATINGS_SAVED";
            public const string AlreadyLiked = "MSG_MATCH_ALREADY_LIKED";
            public const string Liked = "MSG_MATCH_LIKED";
            public const string Unliked = "MSG_MATCH_UNLIKED";
            public const string Deleted = "MSG_MATCH_DELETED";

            // Bildirim metinleri (veritabanına yazılır: Msg.GetDefault)
            public const string NotifJoined = "NOTIF_MATCH_JOINED";                     // {0} oyuncu, {1} maç adı
            public const string NotifJoinedWaitlist = "NOTIF_MATCH_JOINED_WAITLIST";    // {0} oyuncu, {1} maç adı
            public const string NotifWaitlistPromoted = "NOTIF_MATCH_WAITLIST_PROMOTED"; // {0} maç adı, {1} tarih
            public const string NotifResult = "NOTIF_MATCH_RESULT";                     // {0} ev, {1} deplasman, {2} maç adı
            public const string NotifReminder = "NOTIF_MATCH_REMINDER";                 // {0} maç adı, {1} tarih, {2} stadyum
            public const string NotifTeamAdded = "NOTIF_MATCH_TEAM_ADDED";              // {0} maç, {1} tarih, {2} stadyum, {3} adres, {4} takım
            public const string NotifApproved = "NOTIF_MATCH_APPROVED";                 // {0} maç, {1} tarih, {2} stadyum, {3} adres
            public const string NotifRejected = "NOTIF_MATCH_REJECTED";                 // {0} maç, {1} tarih, {2} stadyum, {3} adres

            // Etiketler
            public const string LabelSomePlayer = "LABEL_MATCH_SOME_PLAYER";
            public const string LabelGuestNote = "LABEL_MATCH_GUEST_NOTE";
            public const string LabelScores = "LABEL_MATCH_SCORES";
            public const string LabelGoalsAssists = "LABEL_MATCH_GOALS_ASSISTS";
            public const string LabelRatingScore = "LABEL_MATCH_RATING_SCORE";
        }

        public static partial class Notification
        {
            public const string Added = "MSG_NOTIFICATION_ADDED";
            public const string Deleted = "MSG_NOTIFICATION_DELETED";
            public const string Updated = "MSG_NOTIFICATION_UPDATED";
            public const string MarkedRead = "MSG_NOTIFICATION_MARKED_READ";
            public const string Sent = "MSG_NOTIFICATION_SENT";
        }
    }
}

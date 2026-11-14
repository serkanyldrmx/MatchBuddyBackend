namespace MatcBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        public static partial class Reservation
        {
            public const string RequestSent = "MSG_RESERVATION_REQUEST_SENT";
            public const string OpponentAdded = "MSG_RESERVATION_OPPONENT_ADDED";
            public const string Approved = "MSG_RESERVATION_APPROVED";
            public const string Rejected = "MSG_RESERVATION_REJECTED";
            public const string SetPending = "MSG_RESERVATION_SET_PENDING";
            public const string ReviewSaved = "MSG_RESERVATION_REVIEW_SAVED";
            public const string ReviewUpdated = "MSG_RESERVATION_REVIEW_UPDATED";

            public const string NotifRequest = "NOTIF_RESERVATION_REQUEST";                       // {0} oyuncu, {1} tesis, {2} saha, {3} başlangıç, {4} dk
            public const string NotifApproved = "NOTIF_RESERVATION_APPROVED";                     // {0} maç, {1} tarih, {2} tesis
            public const string NotifRejected = "NOTIF_RESERVATION_REJECTED";                     // {0} maç, {1} tarih, {2} tesis
            public const string NotifRejectedWithReason = "NOTIF_RESERVATION_REJECTED_WITH_REASON"; // + {3} sebep
            public const string NotifTeamSquadAdded = "NOTIF_RESERVATION_TEAM_SQUAD_ADDED";       // {0} ekleyen, {1} maç
            public const string NotifTeamSquadWaitlisted = "NOTIF_RESERVATION_TEAM_SQUAD_WAITLISTED"; // {0} ekleyen, {1} maç
            public const string NotifOpponentJoined = "NOTIF_RESERVATION_OPPONENT_JOINED";        // {0} takım, {1} maç
            public const string NotifVenueReview = "NOTIF_RESERVATION_VENUE_REVIEW";              // {0} oyuncu, {1} tesis, {2} puan

            public const string LabelUserCount = "LABEL_RESERVATION_USER_COUNT";
            public const string LabelRejectReason = "LABEL_RESERVATION_REJECT_REASON";
            public const string LabelVenueFallback = "LABEL_RESERVATION_VENUE_FALLBACK";
        }

        public static partial class Owner
        {
            public const string VenueUpdated = "MSG_OWNER_VENUE_UPDATED";
            public const string PhotoUploaded = "MSG_OWNER_PHOTO_UPLOADED";
            public const string PhotoDeleted = "MSG_OWNER_PHOTO_DELETED";
            public const string CoverUpdated = "MSG_OWNER_COVER_UPDATED";
            public const string PitchSaved = "MSG_OWNER_PITCH_SAVED";
            public const string PitchDeleted = "MSG_OWNER_PITCH_DELETED";
            public const string OpeningHoursSaved = "MSG_OWNER_OPENING_HOURS_SAVED";
            public const string PriceRuleSaved = "MSG_OWNER_PRICE_RULE_SAVED";
            public const string PriceRuleDeleted = "MSG_OWNER_PRICE_RULE_DELETED";
            public const string CampaignSaved = "MSG_OWNER_CAMPAIGN_SAVED";
            public const string CampaignDeleted = "MSG_OWNER_CAMPAIGN_DELETED";
            public const string ManualReservationSaved = "MSG_OWNER_MANUAL_RESERVATION_SAVED";
            public const string SlotClosed = "MSG_OWNER_SLOT_CLOSED";
            public const string PaymentReceived = "MSG_OWNER_PAYMENT_RECEIVED";
            public const string PaymentPending = "MSG_OWNER_PAYMENT_PENDING";
            public const string BlockRemoved = "MSG_OWNER_BLOCK_REMOVED";
            public const string ReviewReplySaved = "MSG_OWNER_REVIEW_REPLY_SAVED";
            public const string ExpenseSaved = "MSG_OWNER_EXPENSE_SAVED";
            public const string ExpenseDeleted = "MSG_OWNER_EXPENSE_DELETED";

            public const string LabelClosedBlockReason = "LABEL_OWNER_CLOSED_BLOCK_REASON";
        }

        public static partial class Admin
        {
            public const string OwnerCreated = "MSG_ADMIN_OWNER_CREATED";
            public const string MembershipUpdated = "MSG_ADMIN_MEMBERSHIP_UPDATED";
            public const string VenueAssigned = "MSG_ADMIN_VENUE_ASSIGNED";
            public const string VenueListed = "MSG_ADMIN_VENUE_LISTED";
            public const string VenueUnlisted = "MSG_ADMIN_VENUE_UNLISTED";

            public const string LabelDefaultPitchName = "LABEL_ADMIN_DEFAULT_PITCH_NAME";
        }

        public static partial class Osm
        {
            public const string Imported = "MSG_OSM_IMPORTED";

            public const string LabelUnnamedPitch = "LABEL_OSM_UNNAMED_PITCH";
        }
    }
}

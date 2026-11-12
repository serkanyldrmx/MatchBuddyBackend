namespace MatcBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        public static partial class Tournament
        {
            public const string Created = "MSG_TOURNAMENT_CREATED";
            public const string TeamRegistered = "MSG_TOURNAMENT_TEAM_REGISTERED";
            public const string TeamWithdrawn = "MSG_TOURNAMENT_TEAM_WITHDRAWN";
            public const string Started = "MSG_TOURNAMENT_STARTED";
            public const string ResultSaved = "MSG_TOURNAMENT_RESULT_SAVED";
            public const string ResultSavedFinished = "MSG_TOURNAMENT_RESULT_SAVED_FINISHED";   // {0} şampiyon
            public const string FixtureDateUpdated = "MSG_TOURNAMENT_FIXTURE_DATE_UPDATED";
            public const string Deleted = "MSG_TOURNAMENT_DELETED";

            public const string NotifStarted = "NOTIF_TOURNAMENT_STARTED";      // {0} turnuva, {1} başlangıç tarihi
            public const string NotifResult = "NOTIF_TOURNAMENT_RESULT";        // {0} turnuva, {1} ev, {2}-{3} skor, {4} deplasman
            public const string NotifFinished = "NOTIF_TOURNAMENT_FINISHED";    // {0} turnuva, {1} şampiyon

            public const string LabelName = "LABEL_TOURNAMENT_NAME";
            public const string LabelDescription = "LABEL_TOURNAMENT_DESCRIPTION";
        }
    }
}

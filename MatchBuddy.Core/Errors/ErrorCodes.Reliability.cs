namespace MatchBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>Güvenilirlik puanı — docs/reliability-api.md.</summary>
        public static class Reliability
        {
            public const string InvalidReason = "RELIABILITY_INVALID_REASON";
            public const string NoteTooLong = "RELIABILITY_NOTE_TOO_LONG";            // {0} en fazla
            public const string TooLow = "RELIABILITY_TOO_LOW";                         // {0} istenen, {1} puanın
            public const string NoShowNotStarted = "RELIABILITY_NOSHOW_NOT_STARTED";
            public const string NoShowWindowClosed = "RELIABILITY_NOSHOW_WINDOW_CLOSED"; // {0} saat
            public const string NoShowNotParticipant = "RELIABILITY_NOSHOW_NOT_PARTICIPANT";
            public const string NoPenalty = "RELIABILITY_NO_PENALTY";
            public const string CannotExcuseSelf = "RELIABILITY_CANNOT_EXCUSE_SELF";
            public const string MinOutOfRange = "RELIABILITY_MIN_OUT_OF_RANGE";
        }
    }
}

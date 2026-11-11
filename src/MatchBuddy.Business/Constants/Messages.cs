namespace MatchBuddy.Business.Constants
{
    /// <summary>
    /// Eski sabit mesajlar. Metinler artık Resources/Messages.resx'te:
    /// hata alanları ErrorCodes'a işaret eden kodlardır (ErrorResult(kod) ile kullanılır),
    /// başarı mesajları istek dilinde kaynaktan çözülen özelliklerdir.
    /// </summary>
    public static class Messages
    {
        public static string Added => Msg.Get(MessageKeys.Common.Added);
        public static string Update => Msg.Get(MessageKeys.Common.Updated);
        public static string Deleted => Msg.Get(MessageKeys.Common.Deleted);
        public static string PlayersListed => Msg.Get(MessageKeys.Common.PlayersListed);
        public static string GroupsListed => Msg.Get(MessageKeys.Common.GroupsListed);

        // Hata kodları (ErrorCodes)
        public const string PlayerNameInvalid = ErrorCodes.Player.NameInvalid;
        public const string GroupNameInvalid = ErrorCodes.Group.NameInvalid;
        public const string MatchNameInvalid = ErrorCodes.Match.NameInvalid;
        public const string TeamNameInvalid = ErrorCodes.Team.NameInvalid;
        public const string TeamInTournament = ErrorCodes.Team.InTournament;
        public const string MaintenanceTime = ErrorCodes.Common.Maintenance;
        public const string MatchTimeConflict = ErrorCodes.Match.TimeConflict;
    }
}

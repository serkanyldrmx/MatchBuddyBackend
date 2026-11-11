namespace MatchBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        /// <summary>Harici (anlaşmasız) sahada maç (docs/external-venues-api.md).</summary>
        public static partial class External
        {
            public const string Created = "MSG_EXTERNAL_MATCH_CREATED";
            public const string Updated = "MSG_EXTERNAL_MATCH_UPDATED";
            public const string NotifUpdated = "NOTIF_EXTERNAL_MATCH_UPDATED";       // {0} maç, {1} tarih, {2} saha
        }

        public static partial class Group
        {
            public const string MembersAdded = "MSG_GROUP_MEMBERS_ADDED";            // {0} kişi
            public const string Left = "MSG_GROUP_LEFT";
            public const string MemberRemoved = "MSG_GROUP_MEMBER_REMOVED";
            public const string Renamed = "MSG_GROUP_RENAMED";
        }
    }
}

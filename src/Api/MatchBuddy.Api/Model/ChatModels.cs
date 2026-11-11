namespace MatchBuddy.Api.Model
{
    /// <summary>Birebir konuşmayı okundu yap: karşı taraf.</summary>
    public class MarkReadModel
    {
        public int PlayerId { get; set; }
    }

    /// <summary>Grup üyeliği işlemleri: grup + oyuncu(lar).</summary>
    public class GroupMembersModel
    {
        public int GroupId { get; set; }
        public int PlayerId { get; set; }
        public List<int>? PlayerList { get; set; }
    }
}

namespace MatchBuddy.Api.Model
{
    /// <summary>Mesaj silme: birebir MessageId ya da grup GroupMessageId.</summary>
    public class DeleteMessageModel
    {
        public int? MessageId { get; set; }
        public int? GroupMessageId { get; set; }
    }
}

namespace MatchBuddy.Api.Model
{
    /// <summary>Davet linki kodu (/m/{kod}).</summary>
    public class JoinByCodeModel
    {
        public string? Code { get; set; }
    }
}

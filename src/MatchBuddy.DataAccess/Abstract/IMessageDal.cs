using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface IMessageDal: IEntityRepository<Message>
    {
    }
}

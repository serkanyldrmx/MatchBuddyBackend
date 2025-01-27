using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFGroupPLayerDal : EfEntityRepositoryBase<GroupPlayer, MatchBuddyContext>, IGroupPlayerDal
    {
        
    }
}

using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFBusinessApplicationDal : EfEntityRepositoryBase<BusinessApplication, MatcBuddyContext>, IBusinessApplicationDal
    {
        public List<BusinessApplication> List(byte? status)
        {
            using var context = new MatcBuddyContext();
            var q = context.BusinessApplications.AsNoTracking();
            if (status.HasValue) q = q.Where(a => a.Status == status.Value);
            return q.OrderBy(a => a.Status == BusinessApplicationStatus.New ? 0 : 1)
                .ThenByDescending(a => a.CreatedAt)
                .Take(300)
                .ToList();
        }

        public int NewCount()
        {
            using var context = new MatcBuddyContext();
            return context.BusinessApplications.Count(a => a.Status == BusinessApplicationStatus.New);
        }

        public bool RecentlyApplied(string phone)
        {
            using var context = new MatcBuddyContext();
            var since = DateTime.Now.AddHours(-24);
            return context.BusinessApplications.Any(a => a.Phone == phone && a.CreatedAt >= since);
        }

        public List<(string Email, string Name)> AdminEmails()
        {
            using var context = new MatcBuddyContext();
            return context.Players.AsNoTracking()
                .Where(p => p.Role == PlayerRoles.Admin && !p.IsDeleted && p.Email != null && p.Email != "")
                .Select(p => new { p.Email, Name = (p.PlayerName + " " + p.PlayerSurname).Trim() })
                .AsEnumerable()
                .Select(p => (p.Email!, p.Name))
                .ToList();
        }
    }
}

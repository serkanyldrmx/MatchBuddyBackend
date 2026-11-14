using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IBusinessApplicationDal : IEntityRepository<BusinessApplication>
    {
        /// <summary>Yeni başvurular önce, sonra en yeni; en fazla 300 kayıt</summary>
        List<BusinessApplication> List(byte? status);
        int NewCount();
        /// <summary>Aynı telefondan son 24 saatte başvuru var mı (çift gönderim)</summary>
        bool RecentlyApplied(string phone);
        /// <summary>Başvuru bildirimi gidecek yönetici e-postaları</summary>
        List<(string Email, string Name)> AdminEmails();
    }
}

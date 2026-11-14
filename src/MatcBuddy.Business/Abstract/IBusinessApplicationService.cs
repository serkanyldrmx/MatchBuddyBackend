using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Abstract
{
    public interface IBusinessApplicationService
    {
        /// <summary>İşletme başvurusu al, yöneticilere e-posta gönder</summary>
        OperationResult<bool> Submit(BusinessApplicationInput input);
        List<BusinessApplicationDto> List(byte? status);
        int NewCount();
        OperationResult<bool> Update(BusinessApplicationUpdateInput input);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using MusicLounge.Application.Common;
using MusicLounge.Application.DTOs.AccountManagement;

namespace MusicLounge.Application.Interfaces;

public interface IS_AccountManagement
{
    Task<ResponseData<MRes_AccountManagement>> Create(MReq_AccountManagementCreate request);

    Task<ResponseData<List<MRes_AccountManagement>>> GetAll(MReq_AccountManagementGetAll request);

    Task<ResponseData<MRes_AccountManagement>> GetDetail(int id);

    Task<ResponseData<MRes_AccountManagement>> Update(MReq_AccountManagementUpdate request);

    Task<ResponseData<object>> Delete(int id);
}

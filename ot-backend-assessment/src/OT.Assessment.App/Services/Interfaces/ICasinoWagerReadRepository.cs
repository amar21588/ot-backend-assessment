using OT.Assessment.App.Models.Responses;
using System;

namespace OT.Assessment.App.Services.Interfaces;

public interface ICasinoWagerReadRepository
{
    Task<PagedCasinoWagerResponse> GetPlayerWagersAsync( Guid accountId,int page, int pageSize, CancellationToken cancellationToken);

    Task<IEnumerable<TopSpenderResponse>> GetTopSpendersAsync(int count, CancellationToken cancellationToken);
}


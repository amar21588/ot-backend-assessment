using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.App.Models.Responses;
using OT.Assessment.App.Services.Interfaces;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace OT.Assessment.App.Services.Implementations;

public sealed class CasinoWagerReadRepository
    : ICasinoWagerReadRepository
{
    private readonly string _connectionString;

    public CasinoWagerReadRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string is missing.");
    }

    public async Task<PagedCasinoWagerResponse> GetPlayerWagersAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();

        parameters.Add("@AccountId", accountId);
        parameters.Add("@Page", page);
        parameters.Add("@PageSize", pageSize);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition("dbo.usp_GetPlayerCasinoWagers", parameters, commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var data = (await multi.ReadAsync<CasinoWagerResponse>()).ToList();

        var total = await multi.ReadSingleAsync<long>();

        return new PagedCasinoWagerResponse
        {
            Data = data,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<IEnumerable<TopSpenderResponse>> GetTopSpendersAsync(int count, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);

        var parameters = new
        {
            Count = count
        };

        var result = await connection.QueryAsync<TopSpenderResponse>(
            new CommandDefinition("dbo.usp_GetTopSpenders", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return result;
    }
}


using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.Consumer.Data.Interfaces;
using OT.Assessment.Contracts.Models;
using System;

namespace OT.Assessment.Consumer.Data.Repositories;

public sealed class CasinoWagerRepository : ICasinoWagerRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CasinoWagerRepository> _logger;

    public CasinoWagerRepository(IConfiguration configuration, ILogger<CasinoWagerRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException( "SQL Server connection string is missing.");

        _logger = logger;
    }

    public async Task SaveAsync(CasinoWagerMessage wager, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving start. WagerId: {WagerId}", wager.WagerId);
        const string sql = """
            INSERT INTO dbo.CasinoWagers
            (
                WagerId,
                AccountId,
                Username,
                Theme,
                Provider,
                GameName,
                TransactionId,
                BrandId,
                ExternalReferenceId,
                TransactionTypeId,
                Amount,
                CreatedDateTime,
                NumberOfBets,
                CountryCode,
                Duration
            )
            SELECT
                @WagerId,
                @AccountId,
                @Username,
                @Theme,
                @Provider,
                @GameName,
                @TransactionId,
                @BrandId,
                @ExternalReferenceId,
                @TransactionTypeId,
                @Amount,
                @CreatedDateTime,
                @NumberOfBets,
                @CountryCode,
                @Duration
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.CasinoWagers WITH (UPDLOCK, HOLDLOCK)
                WHERE WagerId = @WagerId
            );
            """;

        await using var connection = new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(sql, wager, cancellationToken: cancellationToken);

        var affectedRows = await connection.ExecuteAsync(command);
        _logger.LogInformation("Saving completed. WagerId: {WagerId}, AffectedRows: {AffectedRows}", wager.WagerId, affectedRows);
        if (affectedRows == 0)
        {
            _logger.LogInformation( "Duplicate wager ignored. WagerId: {WagerId}", wager.WagerId);

            return;
        }

        _logger.LogInformation("Wager saved successfully. WagerId: {WagerId}", wager.WagerId);
    }
}

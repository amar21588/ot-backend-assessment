using Microsoft.AspNetCore.Mvc;
using OT.Assessment.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OT.Assessment.App.Models.Responses;
using OT.Assessment.App.Services.Interfaces;

namespace OT.Assessment.App.Controllers;

[ApiController]
[Route("api/player")]
public sealed class PlayerController : ControllerBase
{
    private readonly ICasinoWagerPublisher _publisher;
    private readonly ILogger<PlayerController> _logger;
    private readonly ICasinoWagerReadRepository _readRepository;

    public PlayerController(ICasinoWagerPublisher publisher, ICasinoWagerReadRepository readRepository, ILogger<PlayerController> logger)
    {
        _publisher = publisher;
        _readRepository = readRepository;
        _logger = logger;
    }

    [HttpPost("casinowager")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitCasinoWager(
        [FromBody] CasinoWagerMessage wager,
        CancellationToken cancellationToken)
    {
        // Validate required identifiers.
        if (wager.WagerId == Guid.Empty || wager.AccountId == Guid.Empty || wager.TransactionId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "WagerId, AccountId and TransactionId are required."
            });
        }

        // Validate required text fields.
        if (string.IsNullOrWhiteSpace(wager.Username) || string.IsNullOrWhiteSpace(wager.GameName) || string.IsNullOrWhiteSpace(wager.Provider))
        {
            return BadRequest(new
            {
                message = "Username, GameName and Provider are required."
            });
        }

        // Validate financial and numeric fields.
        if (wager.Amount < 0 || wager.NumberOfBets < 0 || wager.Duration < 0)
        {
            return BadRequest(new
            {
                message = "Invalid wager amount or numeric values."
            });
        }

        if (wager.CreatedDateTime == default)
        {
            return BadRequest(new
            {
                message = "CreatedDateTime is required."
            });
        }

        try
        {
            await _publisher.PublishAsync(wager, cancellationToken);

            _logger.LogInformation("Wager {WagerId} accepted for processing", wager.WagerId);

            return Accepted(new
            {
                message = "Casino wager accepted successfully.",
                wagerId = wager.WagerId,
                status = "Accepted"
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to publish wager {WagerId}", wager.WagerId);

            return StatusCode( StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Wager service is temporarily unavailable."
                });
        }
    }


    [HttpGet("{playerId:guid}/casino")]
    [ProducesResponseType(typeof(PagedCasinoWagerResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlayerCasinoWagers(Guid playerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Invalid player ID."
            });
        }

        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Page must be positive and pageSize must be between 1 and 100."
            });
        }

        var result = await _readRepository.GetPlayerWagersAsync(playerId, page, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpGet("topSpenders")]
    [ProducesResponseType(typeof(IEnumerable<TopSpenderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopSpenders([FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > 100)
        {
            return BadRequest(new
            {
                message = "Count must be between 1 and 100."
            });
        }

        var result = await _readRepository.GetTopSpendersAsync(count, cancellationToken);

        return Ok(result);
    }

}


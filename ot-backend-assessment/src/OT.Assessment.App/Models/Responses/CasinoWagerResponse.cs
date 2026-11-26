
using System;
using System.Collections.Generic;

namespace OT.Assessment.App.Models.Responses;

public sealed class CasinoWagerResponse
{
    public Guid WagerId { get; set; }

    public string Game { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}

public sealed class PagedCasinoWagerResponse
{
    public IEnumerable<CasinoWagerResponse> Data { get; set; }
        = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public long Total { get; set; }

    public int TotalPages { get; set; }
}

public sealed class TopSpenderResponse
{
    public Guid AccountId { get; set; }

    public string Username { get; set; } = string.Empty;

    public decimal TotalAmountSpend { get; set; }
}

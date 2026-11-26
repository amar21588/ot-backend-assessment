using OT.Assessment.Contracts.Models;
using System;

namespace OT.Assessment.Consumer.Data.Interfaces;

public interface ICasinoWagerRepository
{
    Task SaveAsync(CasinoWagerMessage wager, CancellationToken cancellationToken);
}

using System;
using OT.Assessment.Contracts.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OT.Assessment.App.Services.Interfaces;

public interface ICasinoWagerPublisher
{
    Task PublishAsync(CasinoWagerMessage wager,CancellationToken cancellationToken = default);
}

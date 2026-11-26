
using Microsoft.Extensions.Options;
using OT.Assessment.App.Configuration;
using OT.Assessment.App.Services.Interfaces;
using OT.Assessment.Contracts.Models;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OT.Assessment.App.Messaging;

public sealed class RabbitMqPublisher : ICasinoWagerPublisher, IAsyncDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqPublisher> _logger;

    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqPublisher(IOptions<RabbitMqSettings> options, ILogger<RabbitMqPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.IsOpen == true &&
            _channel?.IsOpen == true)
        {
            return;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(queue: _settings.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var channelOptions = new CreateChannelOptions( publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
        _channel = await _connection.CreateChannelAsync(channelOptions);

        await DeclareTopologyAsync(_channel, cancellationToken);

        _logger.LogInformation("RabbitMQ connected. Queue: {QueueName}", _settings.QueueName);
    }

    public async Task PublishAsync(CasinoWagerMessage message, CancellationToken cancellationToken = default)
    {
        await _publishLock.WaitAsync(cancellationToken);

        try
        {
            await InitializeAsync(cancellationToken);

            var messageBody = JsonSerializer.SerializeToUtf8Bytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel.BasicPublishAsync(exchange: "casino-message-exchange", routingKey: "casino-message", mandatory: true,basicProperties: properties,body: messageBody, cancellationToken: cancellationToken);

            _logger.LogDebug("Wager {WagerId} published", message.WagerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message {WagerId}", message.WagerId);
            throw;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel,CancellationToken cancellationToken = default)
    {
        const string mainExchange = "casino-message-exchange";
        const string mainQueue = "casino-message-queue";
        const string mainRoutingKey = "casino-message";

        const string retryQueue = "casino-message-retry-queue";
        const string deadQueue = "casino-message-dead";

        await channel.ExchangeDeclareAsync(exchange: mainExchange, type: ExchangeType.Direct, durable: true,autoDelete: false,cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(queue: mainQueue, durable: true, exclusive: false, autoDelete: false,cancellationToken: cancellationToken);

        await channel.QueueBindAsync(queue: mainQueue, exchange: mainExchange, routingKey: mainRoutingKey,cancellationToken: cancellationToken);

        var retryArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = 5000,
            ["x-dead-letter-exchange"] = mainExchange,
            ["x-dead-letter-routing-key"] = mainRoutingKey
        };

        await channel.QueueDeclareAsync(queue: retryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryArguments, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(queue: deadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        _publishLock.Dispose();
    }
}

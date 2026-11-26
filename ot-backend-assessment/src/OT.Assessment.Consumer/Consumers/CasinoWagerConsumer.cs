
using Microsoft.Extensions.Options;
using OT.Assessment.Consumer.Configuration;
using OT.Assessment.Consumer.Data.Interfaces;
using OT.Assessment.Contracts.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OT.Assessment.Consumer.Consumers;

public sealed class CasinoWagerConsumer : BackgroundService
{
    private readonly RabbitMqSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CasinoWagerConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CasinoWagerConsumer(IOptions<RabbitMqSettings> options, IServiceScopeFactory scopeFactory, ILogger<CasinoWagerConsumer> logger)
    {
        _settings = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);

        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(queue: _settings.QueueName, durable: true, exclusive: false,autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(prefetchSize: 0,prefetchCount: _settings.PrefetchCount, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            await ProcessMessageAsync(eventArgs, stoppingToken);
        };

        await _channel.BasicConsumeAsync(queue: _settings.QueueName, autoAck: false, consumer: consumer,cancellationToken: stoppingToken);

        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

        _channel = await _connection.CreateChannelAsync(channelOptions, stoppingToken);

        await DeclareTopologyAsync(_channel, stoppingToken);

        _logger.LogInformation("Casino wager consumer started. Queue: {QueueName}", _settings.QueueName);

        try
        {
            await Task.Delay(Timeout.Infinite,stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Casino wager consumer is stopping.");
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Deserialize<CasinoWagerMessage>(eventArgs.Body.Span,JsonOptions);

            if (json is null || json.WagerId == Guid.Empty)
            {
                throw new InvalidDataException("Invalid wager message.");
            }

            using var scope = _scopeFactory.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<ICasinoWagerRepository>();

            await repository.SaveAsync(json,cancellationToken);

            // ACK only after SQL Server operation succeeds.
            await _channel!.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false,cancellationToken: cancellationToken);

            _logger.LogInformation("Wager processed successfully: {WagerId}",json.WagerId);
        }
        catch (JsonException ex)
        {
            // Invalid JSON is not a temporary database failure.
            await RetryOrDeadLetterAsync(eventArgs, ex, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            await RetryOrDeadLetterAsync(eventArgs, ex, cancellationToken);
        }
        catch (Exception ex)
        {
            // SQL/network failures get bounded delayed retries.
            await RetryOrDeadLetterAsync(eventArgs, ex, cancellationToken);
        }
    }

    private async Task RejectMessageAsync(ulong deliveryTag, CancellationToken cancellationToken)
    {
        await _channel!.BasicRejectAsync(deliveryTag, requeue: false, cancellationToken: cancellationToken);
    }

    private async Task RetryOrDeadLetterAsync(BasicDeliverEventArgs args, Exception exception, CancellationToken cancellationToken)
    {
        const int maxRetryCount = 2;
        const string retryQueue = "casino-wager-retry-queue";
        const string deadQueue = "casino-wager-dead";

        var headers = args.BasicProperties.Headers;

        var retryCount = 0;

        if (headers != null &&
            headers.TryGetValue("x-retry-count", out var retryValue) &&
            retryValue != null)
        {
            retryCount = Convert.ToInt32(retryValue,CultureInfo.InvariantCulture);
        }

        var isDeadLetter = retryCount >= maxRetryCount;

        var targetQueue = isDeadLetter ? deadQueue: retryQueue;

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                ["x-retry-count"] = isDeadLetter ? retryCount : retryCount + 1,
                ["x-last-error"] = exception.Message
            }
        };

        // Publish first. Do not ACK the original until this succeeds.
        await _channel!.BasicPublishAsync(exchange: "",routingKey: targetQueue,mandatory: true, basicProperties: properties, body: args.Body,cancellationToken: cancellationToken);

        await _channel.BasicAckAsync( deliveryTag: args.DeliveryTag, multiple: false, cancellationToken: cancellationToken);

        _logger.LogWarning(exception, "Wager message moved to {TargetQueue}. Retry count: {RetryCount}",targetQueue, retryCount);
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        const string mainExchange = "casino-message-exchange";
        const string mainQueue = "casino-message-queue";
        const string mainRoutingKey = "casino-message";

        const string retryQueue = "casino-message-retry-queue";
        const string deadQueue = "casino-message-dead";

        await channel.ExchangeDeclareAsync(exchange: mainExchange, type: ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(queue: mainQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueBindAsync(queue: mainQueue, exchange: mainExchange, routingKey: mainRoutingKey, cancellationToken: cancellationToken);

        var retryArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = 5000,
            ["x-dead-letter-exchange"] = mainExchange,
            ["x-dead-letter-routing-key"] = mainRoutingKey
        };

        await channel.QueueDeclareAsync(queue: retryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryArguments, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(queue: deadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}

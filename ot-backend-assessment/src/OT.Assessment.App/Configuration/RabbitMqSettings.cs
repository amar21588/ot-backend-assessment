using System;
namespace OT.Assessment.App.Configuration;

public sealed class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "casino-wager-queue";

    public const string MainExchange = "casino-wager-exchange";
    public const string MainRoutingKey = "casino-wager";
    public const string RetryQueue = "casino-wager-retry-queue";
    public const string DeadLetterQueue = "casino-wager-dead";
    public const int RetryDelayMilliseconds = 5000;
    public const int MaxRetryCount = 2;
}

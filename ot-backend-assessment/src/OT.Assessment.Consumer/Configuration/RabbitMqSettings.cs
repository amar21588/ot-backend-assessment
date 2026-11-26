using System;

namespace OT.Assessment.Consumer.Configuration;

public sealed class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "casino-wager-queue";

    public ushort PrefetchCount { get; set; } = 50;
}


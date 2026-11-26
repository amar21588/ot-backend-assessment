
using OT.Assessment.Consumer.Configuration;
using OT.Assessment.Consumer.Consumers;
using OT.Assessment.Consumer.Data.Interfaces;
using OT.Assessment.Consumer.Data.Repositories;

var builder = Host.CreateApplicationBuilder(args);

// RabbitMQ configuration
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

// SQL repository
builder.Services.AddScoped<ICasinoWagerRepository, CasinoWagerRepository>();

// Register the background consumer
builder.Services.AddHostedService<CasinoWagerConsumer>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var host = builder.Build();

host.Run();

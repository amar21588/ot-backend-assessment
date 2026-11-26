
using OT.Assessment.App.Configuration;
using OT.Assessment.App.Messaging;
using OT.Assessment.App.Services.Interfaces;
using OT.Assessment.App.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Register MVC controllers.
builder.Services.AddControllers();

// Bind RabbitMQ configuration.
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

// Register RabbitMQ publisher as Singleton.
builder.Services.AddSingleton<ICasinoWagerPublisher,RabbitMqPublisher>();
builder.Services.AddScoped<ICasinoWagerReadRepository, CasinoWagerReadRepository>();

// Configure logging.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Global HTTP pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    application = "OT.Assessment.App"
}));

app.Run();

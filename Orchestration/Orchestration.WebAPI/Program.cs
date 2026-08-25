using Orchestration.DomainServices;
using Orchestration.DomainServices.BusinessLogic;
using Orchestration.DomainServices.BusinessLogic.Core;
using RabbitMessaging;
using RabbitMessaging.Configuration;
using RabbitMessaging.Configuration.Models;
using RabbitMessaging.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<InMemoryDataStore>();
builder.Services.AddScoped<IInstrumentService, InstrumentService>();
builder.Services.AddScoped(typeof(IEventMessageProducer<>), typeof(EventMessageProducer<>));
builder.Services.AddSingleton<IRabbitConnectionProvider, RabbitConnectionProvider>();
builder.Services.AddSingleton<IRabbitTopologyInitializer, RabbitTopologyInitializer>();
builder.Services.AddHostedService<RabbitTopologyBackgroundInitializer>();

var rabbitSettings = builder.Configuration.GetSection("Rabbit").Get<RabbitSettings>()
    ?? throw new InvalidOperationException("Missing 'Rabbit' configuration section.");
builder.Services.AddSingleton(rabbitSettings);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
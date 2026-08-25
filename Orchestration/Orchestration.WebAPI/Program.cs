using Orchestration.DomainServices;
using Orchestration.DomainServices.BusinessLogic;
using Orchestration.DomainServices.BusinessLogic.Core;
using Orchestration.Infrastructure.Rabbit;
using Orchestration.Infrastructure.Rabbit.Core;
using Orchestration.Infrastructure.Rabbit.Models;

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

var rabbitSettings = builder.Configuration.GetSection("Rabbit").Get<RabbitSettings>()
    ?? throw new InvalidOperationException("Missing 'Rabbit' configuration section.");
builder.Services.AddSingleton(rabbitSettings);

var app = builder.Build();

await app.Services.GetRequiredService<IRabbitTopologyInitializer>().DeclareTopologyAsync();

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
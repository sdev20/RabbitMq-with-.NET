using InstrumentApp.DomainServices;
using InstrumentApp.DomainServices.Core;
using InstrumentApp.Infrastructure.Rabbit.Messages;
using InstrumentsApp.Components;
using InstrumentsApp.Services.MessageProcessors;
using InstrumentsApp.Services.Notifications;
using Orchestration.WebApiClient;
using RabbitMessaging;
using RabbitMessaging.Configuration;
using RabbitMessaging.Configuration.Models;
using RabbitMessaging.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IRabbitConnectionProvider, RabbitConnectionProvider>();
builder.Services.AddSingleton<IRabbitTopologyInitializer, RabbitTopologyInitializer>();
builder.Services.AddSingleton<IInstrumentStatusNotifier, InstrumentStatusNotifier>();
builder.Services.AddScoped<IMessageProcessor<InstrumentStatusChangeMessage>, InstrumentStatusChangeMessageProcessor>();
builder.Services.AddHostedService<RabbitConsumerHostedService<InstrumentStatusChangeMessage>>();
builder.Services.AddScoped<IInstrumentService, InstrumentService>();

var orchestrationApiBaseUrl = builder.Configuration["OrchestrationApi:BaseUrl"]
    ?? throw new InvalidOperationException("Missing 'OrchestrationApi:BaseUrl' configuration value.");
builder.Services.AddInstrumentsApiClient(new Uri(orchestrationApiBaseUrl));

var rabbitSettings = builder.Configuration.GetSection("Rabbit").Get<RabbitSettings>()
    ?? throw new InvalidOperationException("Missing 'Rabbit' configuration section.");
builder.Services.AddSingleton(rabbitSettings);

var app = builder.Build();

await app.Services.GetRequiredService<IRabbitTopologyInitializer>().DeclareTopologyAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
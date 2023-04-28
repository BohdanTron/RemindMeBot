using AzureMapsToolkit;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using RemindMeBot;
using RemindMeBot.Bots;
using RemindMeBot.Dialogs;
using RemindMeBot.Middlewares;
using RemindMeBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add localization
builder.Services.AddLocalization();
builder.Services.AddSingleton<LocalizationMiddleware>();

// Add services for geolocation
builder.Services.AddSingleton(new AzureMapsServices(builder.Configuration["AzureMapService:Key"]));
builder.Services.AddSingleton<ILocationService, AzureLocationService>();

// Configure translation service
builder.Services.AddHttpClient<ITranslationService, AzureTranslationService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Translation:Endpoint"]);
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", builder.Configuration["Translation:Key"]);
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", builder.Configuration["Translation:Location"]);
});

// Create the Bot Framework Authentication to be used with the Bot Adapter
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Bot Adapter with error handling enabled
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Configure State
builder.Services.AddSingleton<IStorage, MemoryStorage>();

builder.Services.AddSingleton<UserState>();
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton<IStateService, StateService>();

// Add the dialogs with the main bot to the container
builder.Services.AddSingleton<UserSettingsDialog>();
builder.Services.AddSingleton<MainDialog>();
builder.Services.AddTransient<IBot, MainBot<MainDialog>>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapPost("api/messages", (IBotFrameworkHttpAdapter adapter, IBot bot, HttpContext context) =>
    adapter.ProcessAsync(context.Request, context.Response, bot));

app.Run();
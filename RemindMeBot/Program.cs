using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using RemindMeBot;
using RemindMeBot.Bots;
using RemindMeBot.Dialogs;
using RemindMeBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Create the Bot Framework Authentication to be used with the Bot Adapter
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Bot Adapter with error handling enabled
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Configure State
builder.Services.AddSingleton<IStorage, MemoryStorage>();

builder.Services.AddSingleton<UserState>();
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton<StateService>();

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
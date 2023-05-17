using Azure.Storage.Queues;
using AzureMapsToolkit;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Azure;
using Newtonsoft.Json;
using RemindMeBot;
using RemindMeBot.Bots;
using RemindMeBot.Dialogs;
using RemindMeBot.Middlewares;
using RemindMeBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IClock, Clock>();

// Configure Azure Storage Clients
builder.Services.AddAzureClients(azureBuilder =>
{
    azureBuilder.AddBlobServiceClient(builder.Configuration["StorageConnectionString:blob"]);
    azureBuilder.AddTableServiceClient(builder.Configuration["StorageConnectionString"]);

    azureBuilder.AddQueueServiceClient(builder.Configuration["StorageConnectionString:queue"])
        .ConfigureOptions(options => options.MessageEncoding = QueueMessageEncoding.Base64);
});

builder.Services.AddSingleton<ReminderTableService>();
builder.Services.AddSingleton<ReminderQueueService>();

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
builder.Services.AddSingleton<ChangeUserSettingsDialog>();
builder.Services.AddSingleton<AddReminderDialog>();
builder.Services.AddSingleton<MainDialog>();
builder.Services.AddTransient<IBot, MainBot<MainDialog>>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapPost("api/messages", (IBotFrameworkHttpAdapter adapter, IBot bot, HttpContext context, CancellationToken cancellationToken) =>
    adapter.ProcessAsync(context.Request, context.Response, bot, cancellationToken));

app.MapGet("api/proactive-message/{partitionKey}/{rowKey}",
    async (string partitionKey, string rowKey, IBotFrameworkHttpAdapter adapter, ReminderTableService tableService, CancellationToken cancellationToken) =>
    {
        var reminder = await tableService.GetReminder(partitionKey, rowKey, cancellationToken);

        if (reminder is null) return Results.NotFound();

        var conversation = JsonConvert.DeserializeObject<ConversationReference>(reminder.ConversationReference);

        var appId = builder.Configuration["MicrosoftAppId"] ?? string.Empty;

        await ((BotAdapter) adapter).ContinueConversationAsync(appId, conversation,
            (context, token) =>
                context.SendActivityAsync(reminder.Text, cancellationToken: token), cancellationToken);

        return Results.Ok();
    });

app.Run();
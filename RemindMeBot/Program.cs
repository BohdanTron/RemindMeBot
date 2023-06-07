using Azure.Storage.Queues;
using AzureMapsToolkit;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.ApplicationInsights;
using Microsoft.Bot.Builder.Azure.Blobs;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
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
using RemindMeBot.Services.Recognizers;

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

// Add Telegram middleware
builder.Services.AddSingleton<TelegramMiddleware>();

// Configure localization
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

// Configure recognizers
builder.Services.AddHttpClient<IReminderRecognizer, OpenAiRecognizer>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OpenAI:BaseAddress"]);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["OpenAI:ApiKey"]}");
});
builder.Services.AddSingleton<IReminderRecognizer, MicrosoftRecognizer>();
builder.Services.AddSingleton<ReminderRecognizersFactory>();

// Create the Bot Framework Authentication to be used with the Bot Adapter
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Bot Adapter with error handling enabled
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Configure Bot Telemetry via App Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]);

builder.Services.AddSingleton<IBotTelemetryClient, BotTelemetryClient>();
builder.Services.AddSingleton<ITelemetryInitializer, OperationCorrelationTelemetryInitializer>();
builder.Services.AddSingleton<ITelemetryInitializer, TelemetryBotIdInitializer>();
builder.Services.AddSingleton<TelemetryInitializerMiddleware>();
builder.Services.AddSingleton(sp =>
{
    var telemetryClient = sp.GetService<IBotTelemetryClient>();
    return new TelemetryLoggerMiddleware(telemetryClient, logPersonalInformation: true);
});

// Configure State
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<IStorage>(
    new BlobsStorage(
        builder.Configuration["StorageConnectionString:blob"],
        builder.Configuration["StateContainer"]));

builder.Services.AddSingleton<UserState>();
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton<IStateService, StateService>();

// Add dialogs with the main bot to container
builder.Services.AddSingleton<UserSettingsDialog>();
builder.Services.AddSingleton<ChangeUserSettingsDialog>();
builder.Services.AddSingleton<AddReminderDialog>();
builder.Services.AddSingleton<RemindersListDialog>();
builder.Services.AddSingleton<CreateQuickReminderDialog>();
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
        var reminder = await tableService.Get(partitionKey, rowKey, cancellationToken);

        if (reminder is null) return Results.NotFound();

        var conversation = JsonConvert.DeserializeObject<ConversationReference>(reminder.ConversationReference);

        var appId = builder.Configuration["MicrosoftAppId"] ?? string.Empty;

        await ((BotAdapter) adapter).ContinueConversationAsync(appId, conversation,
            (context, token) =>
                context.SendActivityAsync(reminder.Text, cancellationToken: token), cancellationToken);

        return Results.Ok();
    });

app.Run();
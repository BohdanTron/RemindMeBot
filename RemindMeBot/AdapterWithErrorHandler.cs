using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Localization;
using RemindMeBot.Middlewares;
using RemindMeBot.Resources;

namespace RemindMeBot;

public class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(
        BotFrameworkAuthentication auth,
        LocalizationMiddleware localizationMiddleware,
        TelegramMiddleware telegramMiddleware,
        ConversationState? conversationState,
        IStringLocalizer<BotMessages> localizer,
        ILogger<IBotFrameworkHttpAdapter> logger) : base(auth, logger)

    {
        Use(localizationMiddleware);
        Use(telegramMiddleware);

        OnTurnError = async (turnContext, exception) =>
        {
            // Log any leaked exception from the application.
            logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

            // Send a message to the user
            var errorMsg = localizer[ResourceKeys.UnexpectedError];
            await turnContext.SendActivityAsync(MessageFactory.Text(errorMsg, errorMsg));

            if (conversationState is not null)
            {
                try
                {
                    // Delete the conversationState for the current conversation to prevent the
                    // bot from getting stuck in a error-loop caused by being in a bad state.
                    // ConversationState should be thought of as similar to "cookie-state" in a Web pages.
                    await conversationState.DeleteAsync(turnContext);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Exception caught on attempting to Delete ConversationState : {e.Message}");
                }
            }

            // Send a trace activity, which will be displayed in the Bot Framework Emulator
            await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message,
                "https://www.botframework.com/schemas/error", "TurnError");
        };
    }
}
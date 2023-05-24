using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using IMiddleware = Microsoft.Bot.Builder.IMiddleware;

namespace RemindMeBot.Middlewares
{
    public class TelegramMiddleware : IMiddleware
    {
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = new())
        {
            turnContext.OnSendActivities(async (newContext, activities, nextSend) =>
            {
                if (turnContext.Activity.ChannelId != "telegram")
                {
                    return await nextSend();
                }

                var messageActivities = activities.Where(a => a.Type == ActivityTypes.Message).ToArray();
                foreach (var activity in messageActivities)
                {
                    activity.ChannelData = new JObject
                    {
                        ["method"] = "sendMessage",
                        ["parameters"] = new JObject
                        {
                            ["text"] = activity.Text,
                            ["parse_mode"] = "Markdown",
                            ["reply_markup"] = BuildReplyMarkup(activity)
                        }
                    };
                    activity.Text = string.Empty;
                    activity.SuggestedActions = null;
                }

                return await nextSend();
            });


            await next(cancellationToken);
        }

        private static JObject BuildReplyMarkup(Activity activity)
        {
            if (activity.SuggestedActions?.Actions is null)
            {
                return new JObject();
            }

            var buttonGroups = activity.SuggestedActions.Actions
                .Select((action, index) => (action, index))
                .GroupBy(x => x.index / 3)
                .Select(g => g.Select(x => x.action));

            var buttonRows = buttonGroups.Select(group =>
                new JArray(group.Select(action =>
                    new JObject
                    {
                        ["text"] = action.Title,
                        ["callback_data"] = action.Value?.ToString() ?? action.Title,
                    })
                ));

            return new JObject
            {
                ["inline_keyboard"] = new JArray(buttonRows)
            };
        }
    }
}

﻿using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class RemindersListDialog : CancelDialog
    {
        private readonly ReminderTableService _reminderTableService;
        private readonly RepeatedIntervalMapper _intervalMapper;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public RemindersListDialog(
            IStateService stateService,
            ReminderTableService reminderTableService,
            RepeatedIntervalMapper intervalMapper,
            IStringLocalizer<BotMessages> localizer) : base(nameof(AddReminderDialog), stateService, localizer)
        {
            _reminderTableService = reminderTableService;
            _intervalMapper = intervalMapper;
            _localizer = localizer;

            AddDialog(new WaterfallDialog($"{nameof(RemindersListDialog)}.main",
                new WaterfallStep[]
                {
                    CheckUserSettingsExistStep,
                    ShowRemindersStep,
                    HandleSelectionStep
                }));

            InitialDialogId = $"{nameof(RemindersListDialog)}.main";
        }

        private async Task<DialogTurnResult> ShowRemindersStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var conversation = stepContext.Context.Activity.GetConversationReference();

            var reminders = (await _reminderTableService.GetList(conversation.User.Id, cancellationToken))
                .Select(r => new
                {
                    r.RowKey,
                    r.Text,
                    r.RepeatedInterval,
                    Date = DateTime.ParseExact(r.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None)
                })
                .OrderBy(r => r.Date)
                .ToList();

            if (!reminders.Any())
            {
                var noRemindersMsg = _localizer[ResourceKeys.NoRemindersCreated];
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(noRemindersMsg, noRemindersMsg), cancellationToken: cancellationToken);

                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            var reminderList = string.Join("\n",
                reminders.Select((reminder, index) =>
                {
                    var date = reminder.Date.ToString("g", CultureInfo.CurrentCulture);

                    if (reminder.RepeatedInterval == RepeatedInterval.None)
                    {
                        return $"{index + 1}) *{reminder.Text}* \n📅  {date}\n"; ;
                    }

                    var interval = _intervalMapper.MapToLocalizedString(reminder.RepeatedInterval);

                    return $"{index + 1}) *{reminder.Text}* \n📅  {date} - {interval}\n";
                }));

            var reminderListMsg = $"{_localizer[ResourceKeys.RemindersList]}\n\n{reminderList}";

            var reply = MessageFactory.Text(reminderListMsg, reminderListMsg);
            reply.SuggestedActions = new SuggestedActions
            {
                Actions = reminders
                    .Select((item, index) => new CardAction
                    {
                        Title = $"🗑️ {index + 1}",
                        Type = ActionTypes.ImBack,
                        Value = item.RowKey
                    }).ToList()
            };

            await stepContext.Context.SendActivityAsync(reply, cancellationToken);

            return new DialogTurnResult(DialogTurnStatus.Waiting);
        }

        private async Task<DialogTurnResult> HandleSelectionStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var conversation = stepContext.Context.Activity.GetConversationReference();

            var partitionKey = conversation.User.Id;
            var rowKey = stepContext.Context.Activity.Text;

            if (!Guid.TryParse(rowKey, out _))
            {
                return await stepContext.EndDialogAsync(new RemindersListDialogResult
                {
                    ItemDeleted = false
                }, cancellationToken: cancellationToken);
            }

            var reminder = await _reminderTableService.Get(partitionKey, rowKey, cancellationToken);
            if (reminder is null)
            {
                return await stepContext.EndDialogAsync(new RemindersListDialogResult
                {
                    ItemDeleted = false
                }, cancellationToken: cancellationToken);
            }

            await _reminderTableService.Delete(partitionKey, rowKey, cancellationToken);

            var reminderDeletedMsg = _localizer[ResourceKeys.ReminderDeleted];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderDeletedMsg, reminderDeletedMsg),
                cancellationToken: cancellationToken);

            return await stepContext.ReplaceDialogAsync(Id, cancellationToken: cancellationToken);
        }
    }

    public record RemindersListDialogResult
    {
        public bool ItemDeleted { get; init; }
    }
}

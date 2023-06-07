using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using FluentAssertions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Testing;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using NSubstitute;
using RemindMeBot.Dialogs;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using RemindMeBot.Services.Recognizers;
using RemindMeBot.Tests.Unit.Common;
using Xunit;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Dialogs
{
    public class CreateQuickReminderDialogTests : BotTestBase
    {
        private readonly CreateQuickReminderDialog _sut;

        private readonly IStateService _stateService = Substitute.For<IStateService>();
        private readonly IClock _clock = Substitute.For<IClock>();
        private readonly ReminderTableService _reminderTableService = Substitute.ForPartsOf<ReminderTableService>(Substitute.For<TableServiceClient>());
        private readonly ReminderQueueService _reminderQueueService = Substitute.ForPartsOf<ReminderQueueService>(Substitute.For<QueueServiceClient>());

        private readonly ReminderRecognizersFactory _recognizersFactory = Substitute.ForPartsOf<ReminderRecognizersFactory>(new List<IReminderRecognizer> { Substitute.For<IReminderRecognizer>() });

        public CreateQuickReminderDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new CreateQuickReminderDialog(_stateService, _clock, _reminderTableService, _reminderQueueService, _recognizersFactory, Localizer);
        }


        [Theory]
        [MemberData(nameof(UserInputVariations.ValidUserInput), MemberType = typeof(UserInputVariations))]
        public async Task ShouldRecognizeTextWithDifferentDateInputs(string input, string text, int days, int hours, int minutes, string? interval = null)
        {
            // Arrange
            var today = new DateTimeOffset(2023, 5, 17, 0, 0, 0, TimeSpan.FromHours(1));

            ConfigureLocalization("en-US", "Europe/London", today);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = "Test culture",
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            _clock.GetLocalDateTime(Arg.Any<string>())
                .Returns(today);

            _recognizersFactory.CreateRecognizer(Arg.Any<string>())
                .Returns(new MicrosoftRecognizer());

            _reminderTableService.Add(Arg.Any<ReminderEntity>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var reminderDate = today.Date.AddDays(days).AddHours(hours).AddMinutes(minutes);
            var reminderDateString = reminderDate.ToString("G", CultureInfo.InvariantCulture);

            var displayDate = reminderDate.ToString("g", CultureInfo.CurrentCulture);
            var expectedReply = interval is null
                ? Localizer[ResourceKeys.ReminderAdded, text, displayDate]
                : Localizer[ResourceKeys.RepeatedReminderAdded, text, displayDate, interval];

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>(input);
            reply.Type.Should().Be(ActivityTypes.Typing);

            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be(expectedReply);

            // Check dialog result
            var conversation = testClient.DialogContext.Context.Activity.GetConversationReference();

            var result = (ReminderEntity) testClient.DialogTurnResult.Result;
            result.PartitionKey.Should().Be(conversation.User.Id);
            result.Text.Should().Be(text);
            result.DueDateTimeLocal.Should().Be(reminderDateString);
            result.RepeatInterval.Should().Be(interval);
        }

        [Theory]
        [MemberData(nameof(UserInputVariations.InvalidUserInputs), MemberType = typeof(UserInputVariations))]
        public async Task ShouldRejectInvalidUserInput(string input)
        {
            // Arrange
            var today = new DateTimeOffset(2023, 5, 17, 0, 0, 0, TimeSpan.FromHours(1));

            ConfigureLocalization("en-US", "Europe/London", today);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = "Test culture",
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            _recognizersFactory.CreateRecognizer(Arg.Any<string>())
                .Returns(new MicrosoftRecognizer());

            _clock.GetLocalDateTime(Arg.Any<string>())
                .Returns(today);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>(input);
            reply.Type.Should().Be(ActivityTypes.Typing);

            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderNotRecognized]);

            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
            testClient.DialogTurnResult.Result.Should().BeNull();
        }

        private class UserInputVariations
        {
            public static IEnumerable<object[]> ValidUserInput()
            {
                // Assuming today is 2023-05-17 12:00 AM

                // Relative time inputs
                yield return new object[] { "Call mom in 2 hours", "Call mom", 0, 2, 0 };
                yield return new object[] { "Buy milk in 15 minutes", "Buy milk", 0, 0, 15 };

                // Specific time inputs
                yield return new object[] { "Water the plants at 8 AM", "Water the plants", 0, 8, 0 };
                yield return new object[] { "Doctor's appointment at 10 PM", "Doctor's appointment", 0, 22, 0 };

                // Absolute date and time inputs with different formats
                yield return new object[] { "Dentist appointment on 2023-05-18 at 11:00", "Dentist appointment", 1, 11, 0 };
                yield return new object[] { "Pay the bills on 18/05/2023 14:00", "Pay the bills", 1, 14, 0 };

                // Relative day inputs
                yield return new object[] { "Grocery shopping tomorrow", "Grocery shopping", 1, 0, 0 };
                yield return new object[] { "Pick up laundry in 3 days", "Pick up laundry", 3, 0, 0 };

                // Relative day inputs with specific time
                yield return new object[] { "Meeting with John tomorrow at 9 AM", "Meeting with John", 1, 9, 0 };
                yield return new object[] { "Birthday party in 5 days at 6 PM", "Birthday party", 5, 18, 0 };

                // Days of the week
                yield return new object[] { "Gym session next Monday", "Gym session", 5, 0, 0 };
                yield return new object[] { "Haircut this Thursday", "Haircut", 1, 0, 0 };

                // Week-related inputs
                yield return new object[] { "Concert next week", "Concert", 5, 0, 0 };

                // Inputs with day and month
                yield return new object[] { "Anniversary on May 18", "Anniversary", 1, 0, 0 };
                yield return new object[] { "Conference on June 20", "Conference", 34, 0, 0 };

                // Additional date formats
                yield return new object[] { "Car maintenance on the 1st of July", "Car maintenance", 45, 0, 0 };
                yield return new object[] { "Vacation starts on 2023.07.10", "Vacation starts", 54, 0, 0 };

                // Longer period inputs
                yield return new object[] { "License renewal in 1 month", "License renewal", 31, 0, 0 };
                yield return new object[] { "Family reunion in 2 years", "Family reunion", 731, 0, 0 };

                // Dates with different separators
                yield return new object[] { "Parent-teacher meeting on 18.05.2023 at 16:00", "Parent-teacher meeting", 1, 16, 0 };
                yield return new object[] { "Dinner with friends on 18/06/2023, 20:00", "Dinner with friends", 32, 20, 0 };
                yield return new object[] { "Dentist appointment on 18-07-2023 9 AM", "Dentist appointment", 62, 9, 0 };

                // Dates with different case
                yield return new object[] { "Call Bob on MAY 18th at 10 AM", "Call Bob", 1, 10, 0 };
                yield return new object[] { "Buy gift for Alice on june 20th, 15:00", "Buy gift for Alice", 34, 15, 0 };

                // Times in 12 hour format
                yield return new object[] { "Visit grandma tomorrow at 2 PM", "Visit grandma", 1, 14, 0 };
                yield return new object[] { "Submit report day after tomorrow at 11:45 AM", "Submit report", 2, 11, 45 };

                // Times in 24 hour format
                yield return new object[] { "Check email in 3 days at 23:00 hours", "Check email", 3, 23, 0 };
                yield return new object[] { "Walk the dog in 4 days at 6:30 hours", "Walk the dog", 4, 6, 30 };

                // Inputs with different English dialects and spellings
                yield return new object[] { "Football match on 18 May, 2023 at half past 3", "Football match", 1, 3, 30 };
                yield return new object[] { "Cricket match on 10 July, 2023 at quarter to 2", "Cricket match", 54, 1, 45 };

                // Recurrent events
                yield return new object[] { "Check email every day at 9 AM", "Check email", 0, 9, 0, "daily" };
                yield return new object[] { "Team meeting every Monday at 10 AM", "Team meeting", 5, 10, 0, "weekly" };
                yield return new object[] { "Call mom every week on Sunday at 3 PM", "Call mom", 4, 15, 0, "weekly" };

                // Weekly recurrent events
                yield return new object[] { "Water the plants every week on Thursday at 8 AM", "Water the plants", 1, 8, 0, "weekly" };
                yield return new object[] { "Team meeting every Friday at 3 PM", "Team meeting", 2, 15, 0, "weekly" };
                yield return new object[] { "Tutoring session every Thursday at 4:30 PM", "Tutoring session", 1, 16, 30, "weekly" };

                // Yearly recurrent events
                yield return new object[] { "Health check-up every May 20th", "Health check-up", 3, 0, 0, "yearly" };
                yield return new object[] { "Birthday party every June 1st at 5 PM", "Birthday party", 15, 17, 0, "yearly" };


                // Not handled cases - area for improvements
                //yield return new object[] { "Pay bills every month on the 5th at 11 AM", "Pay bills", 19, 11, 0, "monthly" };
                //yield return new object[] { "Monthly review meeting every 1st at 10 AM", "Monthly review meeting", 15, 10, 0, "monthly" };
                //yield return new object[] { "Pay rent every 5th of the month", "Pay rent", 19, 0, 0, "monthly" };
                //yield return new object[] { "Monthly doctor's appointment every 15th at 2 PM", "Monthly doctor's appointment", 29, 14, 0, "monthly" };

                //yield return new object[] { "Buy groceries every first day of the month", "Buy groceries", 15, 0, 0, "monthly" };
                //yield return new object[] { "Book club every last Thursday of the month at 6 PM", "Book club", 8, 18, 0, "monthly" };
            }

            public static IEnumerable<object[]> InvalidUserInputs()
            {
                yield return new object[] { "" };
                yield return new object[] { "Call mom at" };
                yield return new object[] { "2023-02-30" }; // Non-existent date
                yield return new object[] { "2023-13-01" }; // Non-existent month
                yield return new object[] { "2023-02-28T25:00:00" }; // Non-existent hour
                yield return new object[] { "2023-02-28T12:60:00" }; // Non-existent minute
                yield return new object[] { "2023-02-28T12:00:60" }; // Non-existent second
                yield return new object[] { "Next Febtember" }; // Misspelled month
                yield return new object[] { "In a jiffy" }; // Ambiguous time
                yield return new object[] { "In the year 2525" }; // Far future date
                yield return new object[] { "When pigs fly" }; // Metaphorical expression
                yield return new object[] { "Buy milk at tea time" }; // Ambiguous time
                yield return new object[] { "2022-12-31T23:59:59" }; // Past date and time
                yield return new object[] { "Let's play a game" }; // No date or time information
                yield return new object[] { "How are you?" }; // No date or time information
            }
        }
    }
}

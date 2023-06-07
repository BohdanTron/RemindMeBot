using RemindMeBot.Models;

namespace RemindMeBot.Services.Recognizers
{
    public interface IReminderRecognizer
    {
        Task<RecognizedReminder?> RecognizeReminder(string input, DateTime refDateTime);

        string[] SupportedCultures { get; }
    }
}
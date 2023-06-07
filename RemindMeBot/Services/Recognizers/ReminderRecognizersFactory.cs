namespace RemindMeBot.Services.Recognizers
{
    public class ReminderRecognizersFactory
    {
        private readonly IEnumerable<IReminderRecognizer> _recognizers;

        public ReminderRecognizersFactory(IEnumerable<IReminderRecognizer> recognizers)
        {
            _recognizers = recognizers;
        }

        public virtual IReminderRecognizer CreateRecognizer(string culture)
        {
            foreach (var recognizer in _recognizers)
            {
                if (recognizer.SupportedCultures.Contains(culture, StringComparer.InvariantCultureIgnoreCase))
                {
                    return recognizer;
                }
            }

            throw new InvalidOperationException("None of existing recognizers can handle the given culture");
        }
    }
}

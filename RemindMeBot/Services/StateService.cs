using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using RemindMeBot.Models;

namespace RemindMeBot.Services
{
    public interface IStateService
    {
        public UserState UserState { get; }
        public ConversationState ConversationState { get; }

        public IStatePropertyAccessor<UserSettings> UserSettingsPropertyAccessor { get; }
        public IStatePropertyAccessor<DialogState> DialogStatePropertyAccessor { get; }
    }

    public class StateService : IStateService
    {
        public UserState UserState { get; }
        public ConversationState ConversationState { get; }

        public IStatePropertyAccessor<UserSettings> UserSettingsPropertyAccessor { get; }
        public IStatePropertyAccessor<DialogState> DialogStatePropertyAccessor { get; }

        public StateService((UserState UserState, ConversationState ConversationState) states)
            : this(states.UserState, states.ConversationState)
        {
        }

        public StateService(UserState userState, ConversationState conversationState)
        {
            UserState = userState;
            ConversationState = conversationState;

            UserSettingsPropertyAccessor = UserState.CreateProperty<UserSettings>($"{nameof(StateService)}.{nameof(UserSettings)}");
            DialogStatePropertyAccessor = ConversationState.CreateProperty<DialogState>($"{nameof(StateService)}.{nameof(DialogState)}");
        }
    }
}

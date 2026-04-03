using System;
using Windows.UI.Xaml.Controls;
using PocketTavern.UWP.Views;

namespace PocketTavern.UWP.Services
{
    public class NavigationService
    {
        private Frame _frame;

        public void SetFrame(Frame frame) => _frame = frame;

        public bool CanGoBack => _frame?.CanGoBack ?? false;
        public void GoBack() => _frame?.GoBack();

        public void NavigateTo(Type pageType, object parameter = null)
            => _frame?.Navigate(pageType, parameter);

        public void NavigateToMain()         => NavigateTo(typeof(MainPage));
        public void NavigateToCharacters()   => NavigateTo(typeof(CharactersPage));
        public void NavigateToRecentChats()  => NavigateTo(typeof(RecentChatsPage));
        public void NavigateToSettings()     => NavigateTo(typeof(SettingsPage));
        public void NavigateToChat(string characterAvatar) => NavigateTo(typeof(ChatPage), characterAvatar);
        public void NavigateToCreateCharacter() => NavigateTo(typeof(CreateCharacterPage));
        public void NavigateToEditCharacter(string avatarFileName) => NavigateTo(typeof(CreateCharacterPage), avatarFileName);
        public void NavigateToCharacterSettings(string avatarUrl) => NavigateTo(typeof(CharacterSettingsPage), avatarUrl);
        public void NavigateToApiConfig()           => NavigateTo(typeof(ApiConfigPage));
        public void NavigateToConnectionProfiles() => NavigateTo(typeof(ConnectionProfilesPage));
        public void NavigateToPersona()      => NavigateTo(typeof(PersonaPage));
        public void NavigateToTextGen()      => NavigateTo(typeof(TextGenPage));
        public void NavigateToOaiPreset()    => NavigateTo(typeof(OaiPresetPage));
        public void NavigateToFormatting()        => NavigateTo(typeof(FormattingPage));
        public void NavigateToContextSettings()   => NavigateTo(typeof(ContextSettingsPage));
        public void NavigateToWorldInfo()    => NavigateTo(typeof(WorldInfoPage));
        public void NavigateToExtensions()            => NavigateTo(typeof(ExtensionsPage));
        public void NavigateToQuickReplySettings()    => NavigateTo(typeof(QuickReplySettingsPage));
        public void NavigateToRegexSettings()         => NavigateTo(typeof(RegexSettingsPage));
        public void NavigateToCharaVault()            => NavigateTo(typeof(CharaVaultPage));
        public void NavigateToTheme()                 => NavigateTo(typeof(ThemePage));
        public void NavigateToImageGenSettings()      => NavigateTo(typeof(ImageGenSettingsPage));
        public void NavigateToTtsSettings()           => NavigateTo(typeof(TtsSettingsPage));
        public void NavigateToDebugLog()              => NavigateTo(typeof(DebugLogPage));
        public void NavigateToGroups()                => NavigateTo(typeof(GroupsPage));
        public void NavigateToGroupChat(string groupId) => NavigateTo(typeof(GroupChatPage), groupId);
        public void NavigateToStImport()              => NavigateTo(typeof(StImportPage));
        public void NavigateToSetupGuide()            => NavigateTo(typeof(SetupGuidePage));
    }
}

using System.Windows;
using System.Windows.Controls;

namespace PaperTodo;

public sealed partial class AppController
{
    private UIElement BuildSettingsSidebarGeneralPage()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(2, 4, 6, 0)
        };

        content.Children.Add(CreateUiLanguageSettingsRow());
        content.Children.Add(WrapWithHint(
            SettingsToggle(
                Strings.Get("TrayStartup"),
                SystemSettingsHelper.IsStartupEnabled(),
                ToggleStartup),
            "TipStartup"));
        content.Children.Add(WrapWithHint(
            SettingsToggle(
                Strings.Get("SettingsEnableToolTips"),
                State.EnableToolTips,
                ToggleToolTips),
            "TipEnableToolTips"));
        content.Children.Add(WrapWithHint(
            SettingsToggle(
                Strings.Get("SettingsEnableAnimations"),
                State.EnableAnimations,
                ToggleAnimations),
            "TipEnableAnimations"));

        return WithSettingsPageRestoreFooter(
            content,
            RestoreSettingsSidebarGeneralDefaults);
    }

    private void RestoreSettingsSidebarGeneralDefaults()
    {
        State.EnableToolTips = true;
        State.EnableAnimations = true;
        State.UiLanguage = UiLanguages.Default;

        SaveNow();
        RefreshToolTipSetting();
        RebuildTrayMenu();
        RefreshSettingsWindowContent();
    }
}

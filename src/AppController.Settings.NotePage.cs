using System.Windows;
using System.Windows.Controls;

namespace PaperTodo;

public sealed partial class AppController
{
    private UIElement BuildSettingsSidebarNotePage()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(2, 4, 6, 0)
        };

        content.Children.Add(SettingsSectionLabel(
            SettingsSidebarLocalized("Markdown", "Markdown", "Markdown", "Markdown")));
        content.Children.Add(WrapWithHint(
            SettingsFieldLabel(Strings.Get("TrayMarkdownRenderMode")),
            "TipMarkdownRender"));
        content.Children.Add(CreateMarkdownRenderSegmentSelector());

        content.Children.Add(SettingsSectionLabel(Strings.Get("SettingsExternalOpen")));
        content.Children.Add(WrapWithHint(
            SettingsFieldLabel(Strings.Get("SettingsExternalMarkdownExtension")),
            "TipExternalExtension"));
        content.Children.Add(CreateExternalMarkdownExtensionEditor());

        if (State.AdvancedSettingsMode)
        {
            content.Children.Add(AdvancedSettingsBlock(
                SettingsSectionLabel(
                    SettingsSidebarLocalized("图片", "Images", "画像", "이미지")),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsAutoCompressLargeImages"),
                        State.AutoCompressLargeImages,
                        ToggleAutoCompressLargeImages)),
                    "TipAutoCompressLargeImages")));

            content.Children.Add(AdvancedSettingsBlock(
                SettingsSectionLabel(Strings.Get("SettingsScriptCapsule")),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsPersistentPowerShellProcess"),
                        State.UsePersistentPowerShellProcess,
                        TogglePersistentPowerShellProcess)),
                    "TipPersistentPowerShellProcess"),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsPreferPowerShell7"),
                        State.PreferPowerShell7,
                        TogglePreferPowerShell7)),
                    "TipPreferPowerShell7"),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsHideScriptRunWindow"),
                        State.HideScriptRunWindow,
                        ToggleHideScriptRunWindow)),
                    "TipHideScriptRunWindow")));
        }

        return WithSettingsPageRestoreFooter(
            content,
            RestoreSettingsSidebarNoteDefaults);
    }

    private void RestoreSettingsSidebarNoteDefaults()
    {
        State.MarkdownRenderMode = MarkdownRenderModes.Enhanced;
        State.ExternalMarkdownExtension = ExternalMarkdownFileExtensions.Default;
        State.AutoCompressLargeImages = true;
        State.UsePersistentPowerShellProcess = false;
        State.PreferPowerShell7 = true;
        State.HideScriptRunWindow = true;
        _imageStore.AutoCompressLargeImages = true;

        PaperWindow.StopPersistentScriptProcesses();
        foreach (var window in _windows.Values)
        {
            window.UpdateMarkdownRenderMode();
            window.UpdateExternalMarkdownExtension();
        }

        SaveNow();
        RebuildTrayMenu();
        RefreshSettingsWindowContent();
    }
}

using System.Windows;
using System.Windows.Controls;

namespace PaperTodo;

public sealed partial class AppController
{
    private UIElement BuildSettingsSidebarLabsPage()
    {
        var root = new StackPanel
        {
            Margin = new Thickness(2, 4, 4, 0)
        };

        root.Children.Add(new TextBlock
        {
            Text = Strings.Get("SettingsLabsIntro"),
            Foreground = TrayWeakTextBrush,
            FontSize = AppTypography.Scale(12),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = AppTypography.Scale(19)
        });

        var columns = new Grid
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftColumn = new StackPanel
        {
            Margin = new Thickness(0, 0, 14, 0)
        };
        var rightColumn = new StackPanel
        {
            Margin = new Thickness(14, 0, 0, 0)
        };

        AddLabsMajorSection(
            leftColumn,
            Strings.Get("LabsFocusBehavior"),
            BuildSettingsLiveRegion("labs.focus", BuildLabsFocusBehaviorSettings));
        AddLabsMajorSection(
            leftColumn,
            Strings.Get("LabsEdgeCapsuleHoverIntent"),
            BuildSettingsLiveRegion(
                "labs.edgePreviewIntent",
                BuildLabsEdgeCapsuleHoverIntentSettings));
        AddLabsMajorSection(
            leftColumn,
            Strings.Get("LabsWindowCoordination"),
            BuildSettingsLiveRegion("labs.window", BuildLabsWindowCoordinationSettings));

        AddLabsMajorSection(
            rightColumn,
            Strings.Get("LabsMcp"),
            BuildSettingsLiveRegion("labs.mcp", BuildLabsMcpSettings));
        AddLabsMajorSection(
            rightColumn,
            Strings.Get("LabsAdvancedShortcuts"),
            BuildSettingsLiveRegion("labs.passive", BuildLabsPassiveModeSettings));

        var separator = new Border
        {
            Width = 1,
            Margin = new Thickness(0, 4, 0, 0),
            Background = TrayBorderBrush,
            Opacity = 0.65
        };

        Grid.SetColumn(leftColumn, 0);
        Grid.SetColumn(separator, 1);
        Grid.SetColumn(rightColumn, 2);
        columns.Children.Add(leftColumn);
        columns.Children.Add(separator);
        columns.Children.Add(rightColumn);
        root.Children.Add(columns);

        return WithSettingsPageRestoreFooter(
            root,
            RestoreSettingsSidebarLabsDefaults);
    }

    private void RestoreSettingsSidebarLabsDefaults()
    {
        State.ExperimentalInactivePaperOpacity = false;
        State.ExperimentalInactivePaperOpacityLevel =
            ExperimentalOpacityLevels.DefaultInactivePaper;
        State.ExperimentalRestingCapsuleOpacity = false;
        State.ExperimentalRestingCapsuleOpacityLevel =
            ExperimentalOpacityLevels.DefaultRestingCapsule;
        State.ExperimentalRestingCapsuleOpacityIncludesMaster = false;
        State.ExperimentalRestingCapsuleOpacityAlways = false;
        State.ExperimentalCollapsePaperOnDeactivate = false;
        State.ExperimentalHideInactiveTopBarButtons = false;
        State.ExperimentalHideInactiveTitleBar = false;
        State.ExperimentalEdgeCapsuleHoverPreview = true;
        State.ExperimentalEdgeCapsuleHoverIntent = true;
        State.ExperimentalEdgeCapsuleHoverIntentSensitivity =
            EdgeCapsuleHoverIntentSensitivities.Medium;
        State.ExperimentalAllowLockIconUnlock = true;
        State.ExperimentalShortcutOpacityLevel = 0.35;
        ClearAdvancedShortcutRuntimeState();
        State.McpEnabled = false;
        State.McpAllowBlankWrites = false;
        State.McpAllowFullWrites = false;
        State.McpAllowDeletes = false;
        State.ExperimentalCapsuleMagnetism = false;
        State.ExperimentalCapsuleMagnetScreenEdges = true;
        State.ExperimentalCapsuleMagnetWindowEdges = true;
        State.ExperimentalCapsuleMagnetDistance =
            ExperimentalWindowAttachmentOptions.DefaultSnapDistance;
        State.ExperimentalWindowTethering = false;
        State.ExperimentalWindowTetherPreferredEdge =
            ExperimentalWindowTetherOptions.Auto;
        State.ExperimentalWindowTetherGap =
            ExperimentalWindowTetherOptions.DefaultGap;
        State.ExperimentalTetherVisibilityLink = false;
        State.ExperimentalTetherMinimizedBehavior =
            ExperimentalTetherVisibilityModes.Hide;
        RestoreLabsShortcutDefaults();

        foreach (var window in _windows.Values.ToList())
        {
            window.DisableExperimentalCapsuleMagnet();
            window.DisableExperimentalTetherVisibilityLink();
            window.DisableExperimentalWindowTether();
        }
        RefreshExperimentalWindowRuntime();
        RefreshEdgeCapsuleHoverIntentRuntime();
        RefreshMcpRuntime();
        SaveNow();
        RefreshExperimentalOpacitySurfaces(animate: false);
        RefreshExperimentalFocusPresentationSurfaces();
        RefreshSettingsWindowContent();
    }
}

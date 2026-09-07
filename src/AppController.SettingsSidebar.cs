using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaperTodo;

public sealed partial class AppController
{
    private enum SettingsSidebarPage
    {
        General,
        Todo,
        Note,
        WindowCapsule,
        Visual,
        Shortcuts,
        Plugins,
        Labs
    }

    private readonly Dictionary<SettingsSidebarPage, double> _settingsSidebarScrollOffsets = new();
    private SettingsSidebarPage _settingsSidebarPage = SettingsSidebarPage.General;
    private ScrollViewer? _settingsSidebarScrollViewer;
    private Window? _settingsSidebarAttachedWindow;
    private DependencyPropertyDescriptor? _settingsSidebarContentDescriptor;
    private bool _settingsSidebarApplyingContent;
    private bool _settingsSidebarRefreshQueued;

    internal void AttachSettingsSidebarHost(Window window)
    {
        if (!ReferenceEquals(window, _settingsWindow) ||
            ReferenceEquals(window, _settingsSidebarAttachedWindow))
        {
            return;
        }

        DetachSettingsSidebarHost();
        _settingsSidebarAttachedWindow = window;
        _settingsSidebarContentDescriptor = DependencyPropertyDescriptor.FromProperty(
            ContentControl.ContentProperty,
            typeof(Window));
        _settingsSidebarContentDescriptor?.AddValueChanged(
            window,
            OnSettingsSidebarWindowContentChanged);
        window.Closed += OnSettingsSidebarWindowClosed;

        RebuildSettingsSidebarContent(window, preserveScroll: false);
    }

    private void OnSettingsSidebarWindowClosed(object? sender, EventArgs e)
    {
        DetachSettingsSidebarHost();
        _settingsSidebarScrollViewer = null;
        _settingsSidebarScrollOffsets.Clear();
        _settingsSidebarPage = SettingsSidebarPage.General;
    }

    private void DetachSettingsSidebarHost()
    {
        if (_settingsSidebarAttachedWindow is { } window)
        {
            _settingsSidebarContentDescriptor?.RemoveValueChanged(
                window,
                OnSettingsSidebarWindowContentChanged);
            window.Closed -= OnSettingsSidebarWindowClosed;
        }

        _settingsSidebarContentDescriptor = null;
        _settingsSidebarAttachedWindow = null;
        _settingsSidebarRefreshQueued = false;
    }

    private void OnSettingsSidebarWindowContentChanged(object? sender, EventArgs e)
    {
        if (_settingsSidebarApplyingContent ||
            sender is not Window window ||
            !ReferenceEquals(window, _settingsWindow))
        {
            return;
        }

        QueueSettingsSidebarRefresh(window);
    }

    private void QueueSettingsSidebarRefresh(Window window)
    {
        if (_settingsSidebarRefreshQueued)
        {
            return;
        }

        _settingsSidebarRefreshQueued = true;
        _ = window.Dispatcher.BeginInvoke(
            (Action)(() =>
            {
                _settingsSidebarRefreshQueued = false;
                if (!ReferenceEquals(window, _settingsWindow) || !window.IsVisible)
                {
                    return;
                }

                RebuildSettingsSidebarContent(window, preserveScroll: true);
            }),
            DispatcherPriority.DataBind);
    }

    private void RebuildSettingsSidebarContent(Window window, bool preserveScroll)
    {
        if (!State.AdvancedSettingsMode && _settingsSidebarPage == SettingsSidebarPage.Labs)
        {
            _settingsSidebarPage = SettingsSidebarPage.General;
        }

        if (preserveScroll)
        {
            RememberSettingsSidebarScrollOffset();
        }

        _settingsPage = LegacySettingsPageFor(_settingsSidebarPage);
        if (SupportsShortcutRecording(_settingsPage))
        {
            EnsureShortcutDraft();
        }

        InvalidateSystemThemeCacheIfNeeded();
        _settingsRegionRefreshers.Clear();
        _pluginStatusRefreshers.Clear();

        var content = BuildSettingsSidebarWindowContent(window);
        _settingsSidebarApplyingContent = true;
        try
        {
            window.Content = content;
        }
        finally
        {
            _settingsSidebarApplyingContent = false;
        }

        window.Title = Strings.Get("TraySettings");
        window.SizeToContent = SizeToContent.Manual;
        window.FontFamily = AppTypography.UiFontFamily;
        window.FontSize = AppTypography.Scale(12);
        window.Language = AppTypography.Language;
        AppTypography.ApplyTextRendering(window);
        ApplyToolTipSetting(window);
        ApplySettingsSidebarFrame(window);
    }

    private UIElement BuildSettingsSidebarWindowContent(Window window)
    {
        var frame = new Border
        {
            Background = TrayPaperBrush,
            BorderBrush = TrayBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            SnapsToDevicePixels = true
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleRow = BuildSettingsSidebarTitleRow(window);
        Grid.SetRow(titleRow, 0);
        root.Children.Add(titleRow);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(158) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var navigation = BuildSettingsSidebarNavigation();
        Grid.SetColumn(navigation, 0);
        body.Children.Add(navigation);

        var separator = new Border
        {
            Background = TrayBorderBrush,
            Opacity = 0.65
        };
        Grid.SetColumn(separator, 1);
        body.Children.Add(separator);

        var pageHost = BuildSettingsSidebarPageHost();
        Grid.SetColumn(pageHost, 2);
        body.Children.Add(pageHost);

        frame.Child = root;
        return frame;
    }

    private Grid BuildSettingsSidebarTitleRow(Window window)
    {
        var titleRow = new Grid
        {
            Height = 44,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            Margin = new Thickness(14, 4, 10, 0)
        };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            var previousWorkArea = WindowWorkAreaHelper.WorkAreaFor(window);
            try
            {
                window.DragMove();
            }
            catch
            {
                // DragMove can throw if the button is released before WPF enters the move loop.
            }

            if (!previousWorkArea.Equals(WindowWorkAreaHelper.WorkAreaFor(window)))
            {
                ApplySettingsSidebarFrame(window);
            }
        };

        titleRow.Children.Add(new TextBlock
        {
            Text = Strings.Get("TraySettings"),
            Foreground = TrayTextBrush,
            FontSize = AppTypography.Scale(15),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var closeButton = new Button
        {
            Content = "×",
            Width = 28,
            Height = 24,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            Foreground = TrayWeakTextBrush,
            FontFamily = AppTypography.SymbolFontFamily,
            FontSize = AppTypography.Scale(16),
            Cursor = Cursors.Hand,
            Focusable = false,
            Style = BuildSettingsCloseButtonStyle()
        };
        closeButton.Click += (_, _) => window.Close();
        Grid.SetColumn(closeButton, 1);
        titleRow.Children.Add(closeButton);
        return titleRow;
    }

    private UIElement BuildSettingsSidebarNavigation()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Background = Theme.Tint((byte)(Theme.IsDark ? 12 : 8)),
            Margin = new Thickness(1, 0, 0, 1)
        };

        var footer = new StackPanel
        {
            Margin = new Thickness(12, 8, 12, 12)
        };
        footer.Children.Add(new Border
        {
            Height = 1,
            Background = TrayBorderBrush,
            Opacity = 0.55,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var advancedModeToggle = SettingsToggle(
            Strings.Get("SettingsAdvancedMode"),
            State.AdvancedSettingsMode,
            ToggleAdvancedSettingsMode);
        advancedModeToggle.FontSize = AppTypography.Scale(11.5);
        advancedModeToggle.Margin = new Thickness(2, 2, 0, 0);
        advancedModeToggle.ToolTip = BuildSettingsHintTooltip(Strings.Get("TipAdvancedSettingsMode"));
        footer.Children.Add(advancedModeToggle);

        var signature = BuildSettingsSignature(reserveScrollBar: false);
        if (signature is FrameworkElement signatureElement)
        {
            signatureElement.Margin = new Thickness(2, 10, 0, 0);
            signatureElement.HorizontalAlignment = HorizontalAlignment.Left;
        }
        footer.Children.Add(signature);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var navigationItems = new StackPanel
        {
            Margin = new Thickness(10, 10, 10, 0)
        };
        foreach (var page in SettingsSidebarPages())
        {
            navigationItems.Children.Add(BuildSettingsSidebarNavigationItem(page));
        }
        root.Children.Add(navigationItems);
        return root;
    }

    private IEnumerable<SettingsSidebarPage> SettingsSidebarPages()
    {
        yield return SettingsSidebarPage.General;
        yield return SettingsSidebarPage.Todo;
        yield return SettingsSidebarPage.Note;
        yield return SettingsSidebarPage.WindowCapsule;
        yield return SettingsSidebarPage.Visual;
        yield return SettingsSidebarPage.Shortcuts;
        yield return SettingsSidebarPage.Plugins;
        if (State.AdvancedSettingsMode)
        {
            yield return SettingsSidebarPage.Labs;
        }
    }

    private UIElement BuildSettingsSidebarNavigationItem(SettingsSidebarPage page)
    {
        var active = page == _settingsSidebarPage;
        var border = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(6),
            Background = active
                ? Theme.Tint((byte)(Theme.IsDark ? 34 : 18))
                : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 1, 0, 1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var indicator = new Border
        {
            Width = 3,
            Height = 18,
            CornerRadius = new CornerRadius(1.5),
            Background = active ? Theme.ActiveBrush : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(indicator, 0);
        grid.Children.Add(indicator);

        var label = new TextBlock
        {
            Text = SettingsSidebarPageLabel(page),
            Foreground = active ? TrayTextBrush : TrayWeakTextBrush,
            FontSize = AppTypography.Scale(12.5),
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        border.Child = grid;

        border.MouseEnter += (_, _) =>
        {
            if (page != _settingsSidebarPage)
            {
                border.Background = TrayHoverBrush;
                label.Foreground = TrayTextBrush;
            }
        };
        border.MouseLeave += (_, _) =>
        {
            if (page != _settingsSidebarPage)
            {
                border.Background = Brushes.Transparent;
                label.Foreground = TrayWeakTextBrush;
            }
        };
        border.MouseLeftButtonDown += (_, e) =>
        {
            SelectSettingsSidebarPage(page);
            e.Handled = true;
        };
        return border;
    }

    private UIElement BuildSettingsSidebarPageHost()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(24, 12, 18, 14)
        };

        var title = new TextBlock
        {
            Text = SettingsSidebarPageLabel(_settingsSidebarPage),
            Foreground = TrayTextBrush,
            FontSize = AppTypography.Scale(19),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = PanningMode.VerticalOnly,
            Content = BuildSettingsSidebarPage()
        };
        _settingsSidebarScrollViewer = scrollViewer;
        if (_settingsSidebarScrollOffsets.TryGetValue(_settingsSidebarPage, out var offset) && offset > 0)
        {
            scrollViewer.Loaded += (_, _) => scrollViewer.Dispatcher.BeginInvoke(
                (Action)(() => scrollViewer.ScrollToVerticalOffset(
                    Math.Min(offset, scrollViewer.ScrollableHeight))),
                DispatcherPriority.ContextIdle);
        }
        root.Children.Add(scrollViewer);
        return root;
    }

    private UIElement BuildSettingsSidebarPage() => _settingsSidebarPage switch
    {
        SettingsSidebarPage.General => BuildSettingsSidebarGeneralPage(),
        SettingsSidebarPage.Todo => BuildSettingsSidebarTodoPage(),
        SettingsSidebarPage.Note => BuildSettingsSidebarNotePage(),
        SettingsSidebarPage.WindowCapsule => BuildSettingsSidebarWindowCapsulePage(),
        SettingsSidebarPage.Visual => BuildSettingsSidebarVisualPage(),
        SettingsSidebarPage.Shortcuts => BuildSettingsSidebarShortcutsPage(),
        SettingsSidebarPage.Plugins => BuildSettingsSidebarPluginsPage(),
        SettingsSidebarPage.Labs => BuildSettingsSidebarLabsPage(),
        _ => BuildSettingsSidebarGeneralPage()
    };

    private void SelectSettingsSidebarPage(SettingsSidebarPage page)
    {
        if (page == SettingsSidebarPage.Labs && !State.AdvancedSettingsMode)
        {
            return;
        }
        if (page == _settingsSidebarPage)
        {
            return;
        }

        RememberSettingsSidebarScrollOffset();
        var previousLegacyPage = _settingsPage;
        var nextLegacyPage = LegacySettingsPageFor(page);
        _settingsSidebarPage = page;
        _settingsPage = nextLegacyPage;

        if (previousLegacyPage != nextLegacyPage && SupportsShortcutRecording(previousLegacyPage))
        {
            _shortcutRecordingCommandId = null;
            ClearShortcutApplyFailure();
        }
        if (SupportsShortcutRecording(nextLegacyPage))
        {
            EnsureShortcutDraft();
        }

        if (_settingsWindow is { IsVisible: true } window)
        {
            RebuildSettingsSidebarContent(window, preserveScroll: false);
        }
    }

    private void RememberSettingsSidebarScrollOffset()
    {
        if (_settingsSidebarScrollViewer == null)
        {
            return;
        }

        _settingsSidebarScrollOffsets[_settingsSidebarPage] =
            _settingsSidebarScrollViewer.VerticalOffset;
    }

    private SettingsPage LegacySettingsPageFor(SettingsSidebarPage page) => page switch
    {
        SettingsSidebarPage.Visual => SettingsPage.Visual,
        SettingsSidebarPage.Shortcuts => SettingsPage.Shortcuts,
        SettingsSidebarPage.Plugins => SettingsPage.Plugins,
        SettingsSidebarPage.Labs => SettingsPage.Labs,
        _ => SettingsPage.General
    };

    private string SettingsSidebarPageLabel(SettingsSidebarPage page) => page switch
    {
        SettingsSidebarPage.General => SettingsSidebarLocalized(
            "常规", "General", "一般", "일반"),
        SettingsSidebarPage.Todo => Strings.Get("MenuTodo"),
        SettingsSidebarPage.Note => Strings.Get("PaperKindNote"),
        SettingsSidebarPage.WindowCapsule => SettingsSidebarLocalized(
            "窗口与胶囊", "Windows & Capsules", "ウィンドウとカプセル", "창 및 캡슐"),
        SettingsSidebarPage.Visual => Strings.Get("SettingsVisual"),
        SettingsSidebarPage.Shortcuts => Strings.Get("SettingsShortcuts"),
        SettingsSidebarPage.Plugins => Strings.Get("SettingsPlugins"),
        SettingsSidebarPage.Labs => Strings.Get("SettingsLabs"),
        _ => Strings.Get("TraySettings")
    };

    private static string SettingsSidebarLocalized(
        string chinese,
        string english,
        string japanese,
        string korean)
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "zh" => chinese,
            "ja" => japanese,
            "ko" => korean,
            _ => english
        };
    }

    private void ApplySettingsSidebarFrame(Window window)
    {
        var workArea = WindowWorkAreaHelper.WorkAreaFor(window);
        var targetWidth = Math.Min(840, Math.Max(360, workArea.Width - 48));
        var targetHeight = Math.Min(620, Math.Max(320, workArea.Height - 48));

        var wasVisible = window.IsVisible;
        var oldLeft = window.Left;
        var oldTop = window.Top;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = targetWidth;
        window.Height = targetHeight;

        if (!wasVisible || !double.IsFinite(oldLeft) || !double.IsFinite(oldTop))
        {
            window.Left = workArea.Left + (workArea.Width - targetWidth) / 2;
            window.Top = workArea.Top + (workArea.Height - targetHeight) / 2;
            return;
        }

        window.Left = ClampWindowCoordinate(
            oldLeft,
            workArea.Left + 16,
            workArea.Right - targetWidth - 16);
        window.Top = ClampWindowCoordinate(
            oldTop,
            workArea.Top + 16,
            workArea.Bottom - targetHeight - 16);
    }
}

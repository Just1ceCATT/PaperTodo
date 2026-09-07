using System.Windows;

namespace PaperTodo;

public sealed partial class AppController
{
    private UIElement BuildSettingsSidebarPluginsPage() =>
        BuildPluginsSettingsPage();
}

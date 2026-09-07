using System.Windows;

namespace PaperTodo;

public partial class App
{
    private static readonly bool SettingsSidebarBootstrapRegistered =
        RegisterSettingsSidebarBootstrap();

    private static bool RegisterSettingsSidebarBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoadedForSettingsSidebar));
        return true;
    }

    private static void OnWindowLoadedForSettingsSidebar(object sender, RoutedEventArgs e)
    {
        if (sender is Window window && Current is App app)
        {
            app._controller?.AttachSettingsSidebarHost(window);
        }
    }
}

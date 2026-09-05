using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PaperTodo;

public sealed partial class PaperWindow
{
    // Register before any PaperWindow instance is constructed. Class handlers run before the
    // instance PreviewKeyDown / paste handlers, so the lock can stop mutations at the window
    // boundary and paste-limit notices can be queued without changing the existing paste path.
    private static readonly bool TodoSafetyGuardsRegistered = RegisterTodoSafetyGuards();

    private static bool RegisterTodoSafetyGuards()
    {
        EventManager.RegisterClassHandler(
            typeof(PaperWindow),
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnTodoSafetyPreviewKeyDown),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(TodoTextBox),
            DataObject.PastingEvent,
            new DataObjectPastingEventHandler(OnTodoSafetyPasting),
            handledEventsToo: true);
        return true;
    }

    private static void OnTodoSafetyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not PaperWindow window ||
            window._paper.Type != PaperTypes.Todo ||
            !window._advancedInteractionLocked ||
            Keyboard.Modifiers != ModifierKeys.Control ||
            e.Key is not (Key.Z or Key.Y))
        {
            return;
        }

        // PaperWindow's Todo undo/redo handler is window-level and would otherwise run before
        // the focused lock shield gets a chance to consume the key.
        e.Handled = true;
    }

    private static void OnTodoSafetyPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TodoTextBox editor ||
            Window.GetWindow(editor) is not PaperWindow window ||
            window._paper.Type != PaperTypes.Todo)
        {
            return;
        }

        string? raw;
        try
        {
            raw = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
                ? e.DataObject.GetData(DataFormats.UnicodeText) as string
                : e.DataObject.GetDataPresent(DataFormats.Text)
                    ? e.DataObject.GetData(DataFormats.Text) as string
                    : null;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        var meaningfulLineCount = raw
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(CleanPastedTodoLine)
            .Count(line => !string.IsNullOrWhiteSpace(line));
        if (meaningfulLineCount <= MaxPastedTodoLines)
        {
            return;
        }

        var omittedCount = meaningfulLineCount - MaxPastedTodoLines;
        _ = window.Dispatcher.BeginInvoke(
            (Action)(() => PaperNoticeDialog.Show(
                window,
                Strings.Get("TodoPasteTruncatedTitle"),
                Strings.Format(
                    "TodoPasteTruncatedMessage",
                    MaxPastedTodoLines,
                    omittedCount))),
            DispatcherPriority.Background);
    }

    internal void PreserveTriggeredTodoReminderInHistory(
        string itemId,
        DateTimeOffset reminderAt)
    {
        PreserveTriggeredTodoReminderInHistory(_undoStack, itemId, reminderAt);
        PreserveTriggeredTodoReminderInHistory(_redoStack, itemId, reminderAt);
    }

    private static void PreserveTriggeredTodoReminderInHistory(
        IEnumerable<List<PaperItem>> history,
        string itemId,
        DateTimeOffset reminderAt)
    {
        foreach (var snapshot in history)
        {
            var item = snapshot.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
            if (item?.ReminderAt == reminderAt)
            {
                // Delivery is runtime truth for this exact scheduled reminder. Replaying an older
                // unrelated snapshot must not turn the same already-surfaced reminder pending again.
                item.ReminderTriggered = true;
            }
        }
    }
}

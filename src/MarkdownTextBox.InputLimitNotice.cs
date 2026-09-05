using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PaperTodo;

public sealed partial class MarkdownTextBox
{
    private bool _textLimitNoticeShown;

    // Observe the same routed input that the editor already validates, but ask the editor's
    // canonical CanApplyTextReplacement() authority whether the operation is allowed. This keeps
    // user feedback aligned with legacy oversized notes and the image-delimiter exception.
    private static readonly bool TextLimitNoticeHandlersRegistered =
        RegisterTextLimitNoticeHandlers();

    private static bool RegisterTextLimitNoticeHandlers()
    {
        EventManager.RegisterClassHandler(
            typeof(MarkdownTextBox),
            UIElement.PreviewTextInputEvent,
            new TextCompositionEventHandler(OnTextLimitPreviewTextInput),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(MarkdownTextBox),
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnTextLimitPreviewKeyDown),
            handledEventsToo: true);
        return true;
    }

    private static void OnTextLimitPreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (sender is not MarkdownTextBox editor ||
            editor.IsReadOnly ||
            editor.MaxLength <= 0 ||
            string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        // The normal editor path first removes an explicitly selected image reference and only
        // then validates the replacement. Do not predict against the pre-delete document here.
        if (editor.HasSelectedImageReference)
        {
            return;
        }

        editor.UpdateTextLimitNotice(
            editor.CanApplyTextReplacement(e.Text));
    }

    private static void OnTextLimitPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not MarkdownTextBox editor ||
            editor.IsReadOnly ||
            editor.MaxLength <= 0)
        {
            return;
        }

        string? replacement = null;
        if (e.Key == Key.Enter && editor._acceptsReturn)
        {
            replacement = editor.NewLineTextAtCaret();
        }
        else if (e.Key == Key.Tab && editor._acceptsTab)
        {
            replacement = "\t";
        }

        if (replacement == null)
        {
            return;
        }

        editor.UpdateTextLimitNotice(
            editor.CanApplyTextReplacement(replacement));
    }

    private void UpdateTextLimitNotice(bool inputAllowed)
    {
        if (inputAllowed)
        {
            _textLimitNoticeShown = false;
            return;
        }

        if (_textLimitNoticeShown)
        {
            return;
        }

        _textLimitNoticeShown = true;
        var maximumCharacters = MaxLength;
        _ = Dispatcher.BeginInvoke(
            (Action)(() =>
            {
                if (Window.GetWindow(this) is not PaperWindow owner)
                {
                    return;
                }

                PaperNoticeDialog.Show(
                    owner,
                    Strings.Get("NoteInputLimitTitle"),
                    Strings.Format(
                        "NoteInputLimitMessage",
                        maximumCharacters));
            }),
            DispatcherPriority.Background);
    }
}

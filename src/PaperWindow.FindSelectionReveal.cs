using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace PaperTodo;

public sealed partial class PaperWindow
{
    private MarkdownTextBox? _builtInFindRevealTrackedNoteBox;
    private EventHandler? _builtInFindRevealSelectionChangedHandler;
    private Popup? _builtInFindRevealTrackedPopup;
    private EventHandler? _builtInFindRevealPopupClosedHandler;
    private int _builtInFindRevealScrollGeneration;

    internal static void OnBuiltInFindTextBoxGotKeyboardFocusForSelectionReveal(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || Application.Current == null)
        {
            return;
        }

        foreach (Window candidate in Application.Current.Windows)
        {
            if (candidate is PaperWindow window &&
                ReferenceEquals(window._findInput, textBox))
            {
                window.EnableBuiltInFindSelectionRevealTracking();
                return;
            }
        }
    }

    private void EnableBuiltInFindSelectionRevealTracking()
    {
        if (_paper.Type != PaperTypes.Note || _noteBox == null)
        {
            DisableBuiltInFindSelectionRevealTracking();
            return;
        }

        if (!ReferenceEquals(_builtInFindRevealTrackedNoteBox, _noteBox))
        {
            if (_builtInFindRevealTrackedNoteBox != null &&
                _builtInFindRevealSelectionChangedHandler != null)
            {
                _builtInFindRevealTrackedNoteBox.TextArea.SelectionChanged -=
                    _builtInFindRevealSelectionChangedHandler;
            }

            _builtInFindRevealSelectionChangedHandler ??=
                (_, _) => RefreshBuiltInFindSelectionReveal();
            _builtInFindRevealTrackedNoteBox = _noteBox;
            _builtInFindRevealTrackedNoteBox.TextArea.SelectionChanged +=
                _builtInFindRevealSelectionChangedHandler;
        }

        if (!ReferenceEquals(_builtInFindRevealTrackedPopup, _findPopup))
        {
            if (_builtInFindRevealTrackedPopup != null &&
                _builtInFindRevealPopupClosedHandler != null)
            {
                _builtInFindRevealTrackedPopup.Closed -= _builtInFindRevealPopupClosedHandler;
            }

            _builtInFindRevealPopupClosedHandler ??=
                (_, _) => DisableBuiltInFindSelectionRevealTracking();
            _builtInFindRevealTrackedPopup = _findPopup;
            if (_builtInFindRevealTrackedPopup != null)
            {
                _builtInFindRevealTrackedPopup.Closed += _builtInFindRevealPopupClosedHandler;
            }
        }

        RefreshBuiltInFindSelectionReveal();
    }

    private void RefreshBuiltInFindSelectionReveal()
    {
        if (_builtInFindRevealTrackedNoteBox == null)
        {
            return;
        }

        if (!ReferenceEquals(_builtInFindRevealTrackedNoteBox, _noteBox))
        {
            EnableBuiltInFindSelectionRevealTracking();
            return;
        }

        var note = _builtInFindRevealTrackedNoteBox;
        if (!IsBuiltInFindOpen ||
            _findAppliedMatch is not PaperFindMatch match ||
            !match.IsNote ||
            note.SelectionStart != match.Offset ||
            note.SelectionLength != match.Length)
        {
            _markdownBodySession?.SetTransientFindReveal(null, 0);
            _builtInFindRevealScrollGeneration++;
            return;
        }

        _markdownBodySession?.SetTransientFindReveal(match.Offset, match.Length);
        QueueBuiltInFindMatchScroll(note, match);
    }

    private void QueueBuiltInFindMatchScroll(
        MarkdownTextBox note,
        PaperFindMatch match)
    {
        var generation = ++_builtInFindRevealScrollGeneration;
        _ = Dispatcher.BeginInvoke((Action)(() =>
        {
            if (generation != _builtInFindRevealScrollGeneration ||
                !IsBuiltInFindOpen ||
                !ReferenceEquals(_noteBox, note) ||
                _findAppliedMatch is not PaperFindMatch current ||
                current != match ||
                note.SelectionStart != match.Offset ||
                note.SelectionLength != match.Length)
            {
                return;
            }

            ScrollBuiltInFindOffsetIntoView(note, match.Offset);
        }), DispatcherPriority.Background);
    }

    internal static void ScrollBuiltInFindOffsetIntoView(
        MarkdownTextBox note,
        int absoluteOffset)
    {
        ArgumentNullException.ThrowIfNull(note);
        var document = note.Document;
        if (document == null)
        {
            return;
        }

        var offset = Math.Clamp(absoluteOffset, 0, document.TextLength);
        var location = document.GetLocation(offset);
        note.ScrollTo(location.Line, location.Column);
    }

    private void DisableBuiltInFindSelectionRevealTracking()
    {
        _builtInFindRevealScrollGeneration++;
        _markdownBodySession?.SetTransientFindReveal(null, 0);

        if (_builtInFindRevealTrackedNoteBox != null)
        {
            if (_builtInFindRevealSelectionChangedHandler != null)
            {
                _builtInFindRevealTrackedNoteBox.TextArea.SelectionChanged -=
                    _builtInFindRevealSelectionChangedHandler;
            }
            _builtInFindRevealTrackedNoteBox = null;
        }

        if (_builtInFindRevealTrackedPopup != null &&
            _builtInFindRevealPopupClosedHandler != null)
        {
            _builtInFindRevealTrackedPopup.Closed -= _builtInFindRevealPopupClosedHandler;
        }
        _builtInFindRevealTrackedPopup = null;
    }
}

internal static class BuiltInFindSelectionRevealRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(
                PaperWindow.OnBuiltInFindTextBoxGotKeyboardFocusForSelectionReveal),
            handledEventsToo: true);
    }
}

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PaperTodo;

public sealed partial class PaperWindow
{
    private MarkdownTextBox? _builtInFindRevealTrackedNoteBox;
    private EventHandler? _builtInFindRevealSelectionChangedHandler;
    private Popup? _builtInFindRevealTrackedPopup;
    private EventHandler? _builtInFindRevealPopupClosedHandler;

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
                _builtInFindRevealTrackedNoteBox.SetTransientSelectionRevealActive(false);
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

        _builtInFindRevealTrackedNoteBox.SetTransientSelectionRevealActive(IsBuiltInFindOpen);
    }

    private void DisableBuiltInFindSelectionRevealTracking()
    {
        if (_builtInFindRevealTrackedNoteBox != null)
        {
            if (_builtInFindRevealSelectionChangedHandler != null)
            {
                _builtInFindRevealTrackedNoteBox.TextArea.SelectionChanged -=
                    _builtInFindRevealSelectionChangedHandler;
            }
            _builtInFindRevealTrackedNoteBox.SetTransientSelectionRevealActive(false);
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

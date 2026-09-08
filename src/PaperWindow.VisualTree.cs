using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PaperTodo;

public sealed partial class PaperWindow
{
    private static bool IsDescendantOf(DependencyObject? current, DependencyObject target)
    {
        while (current != null)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
            current = GetSafeParent(current);
        }
        return false;
    }

    /// <summary>
    /// The built-in note mouse handler currently passes its MarkdownTextBox directly. Keep the
    /// rendered-task consumption guard on that narrow overload so the reusable scrollbar traversal
    /// below stays a pure visual-tree question.
    /// </summary>
    private static bool IsScrollBarInteractionSource(
        DependencyObject? current,
        MarkdownTextBox scope)
    {
        return scope.RenderedTaskCheckBoxMouseDownHandled ||
            IsScrollBarInteractionSource(current, (DependencyObject)scope);
    }

    private static bool IsScrollBarInteractionSource(
        DependencyObject? current,
        DependencyObject scope)
    {
        while (current != null)
        {
            if (current is ScrollBar or Thumb or Track or RepeatButton)
            {
                return true;
            }

            if (ReferenceEquals(current, scope))
            {
                return false;
            }

            current = GetSafeParent(current);
        }

        return false;
    }
}

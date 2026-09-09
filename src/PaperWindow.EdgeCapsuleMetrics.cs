using System.Windows;

namespace PaperTodo;

public sealed partial class PaperWindow
{
    /// <summary>
    /// Ordinary Todo/Note edge capsules share one icon slot. Their symbols are different glyphs
    /// (`✓` / `✎`) with different advances, but that must not make otherwise identical one-character
    /// titles produce different pill widths. Script capsules keep their own icon metrics.
    /// </summary>
    private double MeasureDeepCapsuleIconSlotWidth(double pixelsPerDip)
    {
        if (IsScriptCapsule())
        {
            return MeasureCapsuleIconWidth(pixelsPerDip);
        }

        var todoWidth = MeasureCapsuleTextWidth(
            "✓",
            CapsuleIconFontSize,
            FontWeights.SemiBold,
            AppTypography.SymbolFontFamily,
            pixelsPerDip);
        var noteWidth = MeasureCapsuleTextWidth(
            "✎",
            CapsuleIconFontSize,
            FontWeights.SemiBold,
            AppTypography.SymbolFontFamily,
            pixelsPerDip);
        return Math.Max(todoWidth, noteWidth);
    }
}

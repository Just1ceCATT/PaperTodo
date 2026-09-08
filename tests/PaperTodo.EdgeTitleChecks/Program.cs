using System.IO;
using System.Windows;
using PaperTodo;

internal static class Program
{
    private static int assertions;

    [STAThread]
    private static int Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            CycleAndUnicode();
            Console.WriteLine("PASS title-cycle-and-unicode");
            Persistence();
            Console.WriteLine("PASS legacy-and-zero-persistence");
            Geometry();
            Console.WriteLine("PASS title-presentation-and-transition-geometry");
            Console.WriteLine($"Edge title checks: 3/3 groups, {assertions} assertions passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Application.Current.Shutdown(); }
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void CycleAndUnicode()
    {
        var cycle = new[] { EdgeCapsuleTitleLimit.Unlimited, EdgeCapsuleTitleLimit.Hidden }
            .Concat(Enumerable.Range(1, PaperTitles.MaxConfigurableTitleLength)).ToArray();
        for (var i = 0; i < cycle.Length; i++)
        {
            Check(EdgeCapsuleTitleLimit.Step(cycle[i], true) == cycle[(i + 1) % cycle.Length], "Forward cycle");
            Check(EdgeCapsuleTitleLimit.Step(cycle[i], false) == cycle[(i + cycle.Length - 1) % cycle.Length], "Backward cycle");
        }
        const string text = "中👨‍👩‍👧‍👦e\u0301文";
        Check(EdgeCapsuleTitleLimit.TextForMeasure(text, 0) == text, "Legacy unlimited retains full title");
        Check(EdgeCapsuleTitleLimit.TextForMeasure(text, -1) == "", "Zero measures no title");
        Check(EdgeCapsuleTitleLimit.TextForMeasure(text, 2) == "中👨‍👩‍👧‍👦", "Do not split emoji clusters");
        Check(EdgeCapsuleTitleLimit.TextForMeasure(text, 3) == "中👨‍👩‍👧‍👦e\u0301", "Do not split combining characters");
        Check(EdgeCapsuleTitleLimit.TextForMeasure("", -1) == "", "Empty title");
        Check(EdgeCapsuleTitleLimit.Normalize(int.MinValue) == 0, "Invalid old negatives remain unlimited");
        Check(EdgeCapsuleTitleLimit.Normalize(int.MaxValue) == 20, "Upper bound");
    }

    private static void Persistence()
    {
        var dir = Path.Combine(Path.GetTempPath(), "papertodo-edge-title-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new StateStore(dir, DurableAtomicFileWriter.Shared);
            foreach (var value in new[] { 0, -1, 1, 20 })
            {
                File.WriteAllText(store.FilePath, $"{{\"papers\":[],\"deepCapsuleTitleMeasureCharacterLimit\":{value}}}");
                var state = store.Load();
                Check(state.DeepCapsuleTitleMeasureCharacterLimit == value, "Load/normalization retains semantics");
                File.WriteAllText(store.FilePath, store.SerializeState(state));
                Check(store.Load().DeepCapsuleTitleMeasureCharacterLimit == value, "Round-trip retains zero/unlimited");
            }
            File.WriteAllText(store.FilePath, "{\"papers\":[]}");
            Check(store.Load().DeepCapsuleTitleMeasureCharacterLimit == 0, "Missing legacy field remains unlimited");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static void Geometry()
    {
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        foreach (var edge in new[] { EdgeCapsuleEdge.Left, EdgeCapsuleEdge.Right })
        foreach (var hiddenClose in new[] { false, true })
        {
            var monitor = new MonitorGeometry("test", new DeviceScreenRect(-1920, -100, 0, 980), scale, scale);
            var layout = new EdgeCapsuleLayoutSnapshot(monitor, edge, 40, 10,
                40, 28, 40, 260, 180, hiddenClose, 1, null,
                ExpandedWidthDip: 150, HideRestingTitle: true);
            var model = EdgeCapsuleModel.Initial with
            {
                State = new EdgeCapsuleState(EdgeCapsuleSlotState.CollapsedDocked,
                    EdgeCapsuleVisualState.Resting, EdgeCapsuleGestureState.Idle, EdgeCapsuleOpenOrigin.Normal),
                Placement = new EdgeCapsulePlacement(0, 0, 1)
            };
            EdgeCapsuleTargetPresentation Plan(EdgeCapsuleModel m, EdgeCapsuleLayoutSnapshot l) =>
                EdgeCapsuleTargetPlanner.Calculate(m, l).Docked;
            var resting = Plan(model, layout);
            var hoverModel = model with { State = model.State with { Visual = EdgeCapsuleVisualState.Hovered } };
            var hovered = Plan(hoverModel, layout);
            Check(!resting.TitleVisible && hovered.TitleVisible, "Zero title appears only while expanded");
            Check(resting.HostBounds == hovered.HostBounds, "Reserve full title capacity before hover");
            var expected = EdgeCapsuleGeometry.Calculate(new(monitor, edge, 40, 150, 28, 40));
            Check(hovered.Bounds == expected.Bounds && hovered.InteractiveBounds == expected.InteractiveBounds,
                "Visible width and hit region follow full title on target DPI/edge");
            Check(hovered.CloseSegmentActsAsContent == hiddenClose, "Hidden close setting retained");
            var transition = new EdgeCapsuleTransition(resting.ToFrame(), hovered, 0, 100,
                EdgeCapsuleTransitionReason.Pointer);
            var previousWidth = resting.Bounds.Width;
            for (var tick = 0; tick <= 100; tick += 5)
            {
                var frame = EdgeCapsuleTransitionPolicy.Sample(transition, tick).Frame;
                Check(frame.IsUsable && frame.HostBounds == resting.HostBounds, "Every animation frame stays inside stable host");
                Check(frame.Bounds.Width >= previousWidth, "No width reversal during hover");
                previousWidth = frame.Bounds.Width;
            }
            var retract = new EdgeCapsuleTransition(hovered.ToFrame(), resting, 0, 100,
                EdgeCapsuleTransitionReason.Pointer);
            Check(!EdgeCapsuleTransitionPolicy.Sample(retract, 100).Frame.TitleVisible,
                "Mouse leave restores hidden title");
            Check(EdgeCapsuleTransitionPolicy.Create(resting.ToFrame(), hovered,
                EdgeCapsuleMotion.Snap(EdgeCapsuleTransitionReason.Pointer), false, 0, 1000) == null,
                "Animation disabled remains an immediate apply");
            var previewsEnabled = layout with { ExpandedWidthDip = layout.RestingWidthDip };
            var ordinaryHover = Plan(hoverModel, previewsEnabled);
            Check(!ordinaryHover.TitleVisible && ordinaryHover.BodyWindowWidthDevice == resting.BodyWindowWidthDevice,
                "Preview-enabled compact hover retains the configured title limit");
            var preview = Plan(hoverModel with { Preview = EdgeCapsulePreviewState.Open }, layout);
            Check(preview.Surface == EdgeCapsuleSurfaceKind.DockedPreview &&
                preview.Bounds.Width == (int)Math.Round(260 * scale), "Preview card width stays independent");
            var active = hoverModel with { State = hoverModel.State with { Visual = EdgeCapsuleVisualState.Active } };
            var handoff = Plan(active with { State = active.State with { Gesture = EdgeCapsuleGestureState.DockingHandoff } }, layout);
            var reveal = Plan(active with { State = active.State with { Gesture = EdgeCapsuleGestureState.DockingReveal } }, layout);
            Check(handoff.Bounds == reveal.Bounds && reveal.Bounds == Plan(active, layout).Bounds,
                "Docking handoff/reveal/active retain identical title geometry");
        }
    }
}

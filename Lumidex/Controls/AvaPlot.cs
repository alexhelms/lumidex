using Avalonia;
using Avalonia.Skia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using SkiaSharp;
using ScottPlot;
using ScottPlot.Interactivity;

namespace Lumidex.Controls;

public class AvaPlot : Avalonia.Controls.Control, IPlotControl
{
    public static readonly DirectProperty<AvaPlot, Plot> PlotProperty =
        AvaloniaProperty.RegisterDirect<AvaPlot, Plot>(
            nameof(Plot),
            o => o.Plot,
            (o, v) => o.Plot = v);

    public Plot Plot
    {
        get => field;
        set
        {
            if (field is not null)
            {
                field.PlotControl = null;
            }

            if (value is not null && SetAndRaise(PlotProperty, ref field!, value))
            {
                field.PlotControl = this;
            }
        }
    }

    public IPlotMenu? Menu { get; set; }
    public UserInputProcessor UserInputProcessor { get; }

    public GRContext? GRContext => null;

    public float DisplayScale { get; set; }

    public IMultiplot Multiplot { get; set; }

    public AvaPlot()
    {
        Plot = new() { PlotControl = this };
        Multiplot = new Multiplot(Plot);
        ClipToBounds = true;
        DisplayScale = DetectDisplayScale();
        
        UserInputProcessor = new(this);
        UserInputProcessor.UserActionResponses.Clear();
        UserInputProcessor.UserActionResponses.AddRange([
            new ScottPlot.Interactivity.UserActionResponses.SingleClickContextMenu(StandardMouseButtons.Right),
        ]);

        Menu = new AvaPlotMenu(this);
        Focusable = true; // Required for keyboard events
        Refresh();
    }

    private class CustomDrawOp : ICustomDrawOperation
    {
        private readonly Plot _plot;

        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;

        public CustomDrawOp(Rect bounds, Plot plot)
        {
            _plot = plot;
            Bounds = bounds;
        }

        public void Dispose()
        {
            // No-op
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null) return;

            using var lease = leaseFeature.Lease();
            ScottPlot.PixelRect rect = new(0, (float)Bounds.Width, (float)Bounds.Height, 0);

            try
            {
                _plot.Render(lease.SkCanvas, rect);
            }
            catch (NullReferenceException)
            {
                // Sometimes this throws, maybe a bug in ScottPlot?
            }
            catch (KeyNotFoundException)
            {
                // Sometimes this throws, maybe a bug in ScottPlot?
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        Rect controlBounds = new(Bounds.Size);
        CustomDrawOp customDrawOp = new(controlBounds, Plot);
        context.Custom(customDrawOp);
    }

    public void Reset()
    {
        Plot plot = new() { PlotControl = this };
        Reset(plot);
    }

    public void Reset(Plot plot)
    {
        Plot oldPlot = Plot;
        Plot = plot;
        oldPlot?.Dispose();
    }

    public void Refresh()
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    public void ShowContextMenu(Pixel position)
    {
        Menu?.ShowContextMenu(position);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        UserInputProcessor.ProcessMouseDown(pixel, kind);

        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        UserInputProcessor.ProcessMouseUp(pixel, kind);

        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        UserInputProcessor.ProcessMouseMove(pixel);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        float delta = (float)e.Delta.Y; // This is now the correct behavior even if shift is held, see https://github.com/AvaloniaUI/Avalonia/pull/8628

        if (delta != 0)
        {
            UserInputProcessor.ProcessMouseWheel(pixel, delta);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        UserInputProcessor.ProcessKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        UserInputProcessor.ProcessKeyUp(e);
    }

    public float DetectDisplayScale()
    {
        // TODO: improve support for DPI scale detection
        // https://github.com/ScottPlot/ScottPlot/issues/2760
        return 1.0f;
    }
}
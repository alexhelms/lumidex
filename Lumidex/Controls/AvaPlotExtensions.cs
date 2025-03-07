using Avalonia.Input;
using ScottPlot.Interactivity;
using ScottPlot;

using AvaKey = Avalonia.Input.Key;
using Key = ScottPlot.Interactivity.Key;
using UserActions = ScottPlot.Interactivity.UserActions;

namespace Lumidex.Controls;

internal static class AvaPlotExtensions
{
    internal static Pixel ToPixel(this PointerEventArgs e, Visual visual)
    {
        float x = (float)e.GetPosition(visual).X;
        float y = (float)e.GetPosition(visual).Y;
        return new Pixel(x, y);
    }

    internal static void ProcessMouseDown(this UserInputProcessor processor, Pixel pixel, PointerUpdateKind kind)
    {
        IUserAction action = kind switch
        {
            PointerUpdateKind.LeftButtonPressed => new UserActions.LeftMouseDown(pixel),
            PointerUpdateKind.MiddleButtonPressed => new UserActions.MiddleMouseDown(pixel),
            PointerUpdateKind.RightButtonPressed => new UserActions.RightMouseDown(pixel),
            _ => new UserActions.Unknown("mouse down", kind.ToString()),
        };

        processor.Process(action);
    }

    internal static void ProcessMouseUp(this UserInputProcessor processor, Pixel pixel, PointerUpdateKind kind)
    {
        IUserAction action = kind switch
        {
            PointerUpdateKind.LeftButtonReleased => new UserActions.LeftMouseUp(pixel),
            PointerUpdateKind.MiddleButtonReleased => new UserActions.MiddleMouseUp(pixel),
            PointerUpdateKind.RightButtonReleased => new UserActions.RightMouseUp(pixel),
            _ => new UserActions.Unknown("mouse up", kind.ToString()),
        };

        processor.Process(action);
    }

    internal static void ProcessMouseMove(this UserInputProcessor processor, Pixel pixel)
    {
        processor.Process(new UserActions.MouseMove(pixel));
    }

    internal static void ProcessMouseWheel(this UserInputProcessor processor, Pixel pixel, double delta)
    {
        IUserAction action = delta > 0
            ? new UserActions.MouseWheelUp(pixel)
            : new UserActions.MouseWheelDown(pixel);

        processor.Process(action);
    }

    internal static void ProcessKeyDown(this UserInputProcessor processor, KeyEventArgs e)
    {
        Key key = GetKey(e.Key);
        IUserAction action = new UserActions.KeyDown(key);
        processor.Process(action);
    }

    internal static void ProcessKeyUp(this UserInputProcessor processor, KeyEventArgs e)
    {
        Key key = GetKey(e.Key);
        IUserAction action = new UserActions.KeyUp(key);
        processor.Process(action);
    }

    public static Key GetKey(AvaKey avaKey)
    {
        return avaKey switch
        {
            AvaKey.LeftAlt => StandardKeys.Alt,
            AvaKey.RightAlt => StandardKeys.Alt,
            AvaKey.LeftShift => StandardKeys.Shift,
            AvaKey.RightShift => StandardKeys.Shift,
            AvaKey.LeftCtrl => StandardKeys.Control,
            AvaKey.RightCtrl => StandardKeys.Control,
            _ => new Key(avaKey.ToString()),
        };
    }
}

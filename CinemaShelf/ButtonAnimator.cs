using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace CinemaShelf;

public static class ButtonAnimator
{
    public static async Task Animate(object sender, uint duration = 140)
    {
        if (sender is View view)
        {
            await view.ScaleTo(0.92, duration, Easing.CubicOut);
            await view.ScaleTo(1.0, duration, Easing.CubicIn);
        }
    }

    public static async Task AnimateFast(object sender)
    {
        if (sender is View view)
        {
            await view.ScaleTo(0.92, 80, Easing.CubicOut);
            await view.ScaleTo(1.0, 80, Easing.CubicIn);
        }
    }
}


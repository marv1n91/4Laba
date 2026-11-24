using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SortingLab4.App.Helpers;

/// <summary>
/// Плавные анимации для WPF: перемещение по Canvas, смена цвета и появление панелей.
/// </summary>
public static class UiAnimations
{
    public static readonly IEasingFunction SmoothOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    public static readonly IEasingFunction SmoothInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

    public static void FadeIn(UIElement element, int durationMs = 150)
    {
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = SmoothOut });
    }

    public static void AnimateCanvasLeft(UIElement element, double toLeft, int durationMs)
    {
        double current = Canvas.GetLeft(element);
        if (double.IsNaN(current))
        {
            Canvas.SetLeft(element, toLeft);
            return;
        }
        if (Math.Abs(current - toLeft) < 0.5)
        {
            element.BeginAnimation(Canvas.LeftProperty, null);
            Canvas.SetLeft(element, toLeft);
            return;
        }
        var anim = new DoubleAnimation(current, toLeft, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = SmoothInOut,
            FillBehavior = FillBehavior.HoldEnd
        };
        element.BeginAnimation(Canvas.LeftProperty, anim);
    }

    public static void AnimateOffsetY(UIElement element, double toY, int durationMs)
    {
        var tt = EnsureTranslate(element);
        if (Math.Abs(tt.Y - toY) < 0.5) return;
        var anim = new DoubleAnimation(tt.Y, toY, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = SmoothOut
        };
        tt.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    public static void AnimateBrushColor(SolidColorBrush brush, Color toColor, int durationMs)
    {
        if (brush.IsFrozen) return;
        if (brush.Color == toColor) return;
        var anim = new ColorAnimation(brush.Color, toColor, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = SmoothOut
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    public static void AnimateOpacity(UIElement element, double toOpacity, int durationMs)
    {
        if (Math.Abs(element.Opacity - toOpacity) < 0.02) return;
        var anim = new DoubleAnimation(element.Opacity, toOpacity, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = SmoothOut
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static TranslateTransform EnsureTranslate(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform tt) return tt;
        var created = new TranslateTransform();
        element.RenderTransform = created;
        return created;
    }
}

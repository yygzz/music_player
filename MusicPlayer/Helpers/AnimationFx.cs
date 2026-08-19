using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicPlayer.Helpers;

/// <summary>复用的缓动/过渡工具，全部基于 RenderTransform（GPU 合成，不触发布局）。</summary>
public static class AnimationFx
{
    /// <summary>淡入 + 轻微上移（用于页面切换，带轻回弹）。</summary>
    public static void FadeInSlide(FrameworkElement e)
    {
        if (e.RenderTransform is not TranslateTransform tt)
        {
            tt = new TranslateTransform();
            e.RenderTransform = tt;
        }

        e.BeginAnimation(UIElement.OpacityProperty, null);
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        e.BeginAnimation(UIElement.OpacityProperty, fade);

        tt.Y = 14;
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }
        };
        tt.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
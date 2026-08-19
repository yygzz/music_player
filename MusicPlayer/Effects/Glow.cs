using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicPlayer.Effects;

/// <summary>
/// 为按钮在悬停时播放"流动的白色光晕"。
/// 使用方式：在 XAML 中对任意 Button 设置 effects:Glow.IsEnabled="True"。
/// 要求按钮模板的根元素是一个名为 "Root" 的 Border（本项目的按钮样式均满足）。
/// 实现：把 Border.BorderBrush 换成三段式 LinearGradientBrush，
/// 持续动画化白色 GradientStop 的 Offset，使光斑沿边框往复流动。
/// </summary>
public static class Glow
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(Glow),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    // 挂载按钮后找到的模板 Border 与其原始 BorderBrush
    private static readonly System.Collections.Generic.Dictionary<Border, Brush> Originals = new();
    // 每个 Border 正在播放的 Storyboard（用于停止）
    private static readonly System.Collections.Generic.Dictionary<Border, Storyboard> ActiveStoryboards = new();

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button) return;

        if ((bool)e.NewValue)
            button.Loaded += OnButtonLoaded;
        else
            button.Loaded -= OnButtonLoaded;
    }

    private static void OnButtonLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        button.ApplyTemplate();
        if (button.Template?.FindName("Root", button) is not Border border)
            return;

        border.MouseEnter += (_, _) => StartGlow(border);
        border.MouseLeave += (_, _) => StopGlow(border);
    }

    private static void StartGlow(Border border)
    {
        if (ActiveStoryboards.ContainsKey(border)) return;

        if (!Originals.ContainsKey(border))
            Originals[border] = border.BorderBrush;

        // 三段式渐变：两端透明、中间亮白，形成一条"光带"
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF), 0.5));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0));
        border.BorderBrush = brush;

        // 直接动画化中间白色 GradientStop 的 Offset（从 -1 流动到 2，循环）
        var whiteStop = brush.GradientStops[1];
        var animation = new DoubleAnimation
        {
            From = -1.0,
            To = 2.0,
            Duration = TimeSpan.FromMilliseconds(1600),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(animation, whiteStop);
        Storyboard.SetTargetProperty(animation, new PropertyPath(GradientStop.OffsetProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(border, true);

        ActiveStoryboards[border] = storyboard;
    }

    private static void StopGlow(Border border)
    {
        if (ActiveStoryboards.TryGetValue(border, out var storyboard))
        {
            storyboard.Stop(border);
            ActiveStoryboards.Remove(border);
        }

        if (Originals.TryGetValue(border, out var original))
        {
            border.BorderBrush = original;
            Originals.Remove(border);
        }
    }
}

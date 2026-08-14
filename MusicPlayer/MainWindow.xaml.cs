using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicPlayer.Helpers;
using MusicPlayer.Models;
using MusicPlayer.Shell;

namespace MusicPlayer;

public partial class MainWindow : Window
{
    public ObservableCollection<NavItem> NavItems { get; } = new();
    public ObservableCollection<Song> Songs { get; } = new();

    // 侧边栏拖拽排序状态
    private int _navDragIndex = -1;
    private bool _navIsDragging;
    private Point _navDragStart;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        PopulateNavItems();
        PopulateSongs();
        StartBackgroundAnimations();
    }

    #region 数据

    private void PopulateNavItems()
    {
        NavItems.Add(new NavItem("Home", "\uE80F"));
        NavItems.Add(new NavItem("Search", "\uE721"));
        NavItems.Add(new NavItem("Library", "\uE8F1"));
        NavItems.Add(new NavItem("Favorites", "\uE734"));
        NavItems.Add(new NavItem("History", "\uE81C"));
        NavList.SelectedIndex = 0;
    }

    private void PopulateSongs()
    {
        Songs.Add(new Song(1, "Midnight Bloom", "Lumen", "Neon City Nights", "3:24"));
        Songs.Add(new Song(2, "Static Waves", "Aria Nova", "Neon City Nights", "4:02"));
        Songs.Add(new Song(3, "Golden Hour", "Cinder & Co", "Loose Ends", "2:58"));
        Songs.Add(new Song(4, "Paper Moons", "Eli Rain", "Paper Moons", "3:41"));
        Songs.Add(new Song(5, "Low Light", "The Orbits", "Neon City Nights", "3:12"));
        Songs.Add(new Song(6, "Velvet", "Aria Nova", "Loose Ends", "3:55"));
        Songs.Add(new Song(7, "Driftlines", "Lumen", "Paper Moons", "4:18"));
        Songs.Add(new Song(8, "Afterimage", "Cinder & Co", "Loose Ends", "3:09"));
    }

    #endregion

    #region 亚克力窗口

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        AcrylicWindow.Enable(this);
        AcrylicWindow.TryRoundCorners(new WindowInteropHelper(this).Handle);
    }

    #endregion

    #region 背景光斑动画（Canvas + Ellipse + BlurEffect）

    private void StartBackgroundAnimations()
    {
        AnimateSpot(Blob1, -80, 380, 140, 480, TimeSpan.FromSeconds(13));
        AnimateSpot(Blob2, 360, 60, -60, 340, TimeSpan.FromSeconds(17));
        AnimateSpot(Blob3, 900, 1280, 80, 460, TimeSpan.FromSeconds(15));
        AnimateSpot(Blob4, 640, 1050, 520, 120, TimeSpan.FromSeconds(19));
    }

    private static void AnimateSpot(UIElement element,
        double fromLeft, double toLeft, double fromTop, double toTop, TimeSpan duration)
    {
        var left = new DoubleAnimation(fromLeft, toLeft, new Duration(duration))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(left, element);
        Storyboard.SetTargetProperty(left, new PropertyPath(Canvas.LeftProperty));

        var top = new DoubleAnimation(fromTop, toTop, new Duration(duration))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(top, element);
        Storyboard.SetTargetProperty(top, new PropertyPath(Canvas.TopProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(left);
        storyboard.Children.Add(top);
        storyboard.Begin((FrameworkElement)element, true);
    }

    #endregion

    #region 搜索框：聚焦时下划线从点击处展开 / 失焦收缩

    private void SearchTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SearchUnderline.RenderTransform is ScaleTransform scale)
        {
            scale.CenterX = e.GetPosition(SearchUnderline).X;
            AnimateScaleX(SearchUnderline, 1.0, TimeSpan.FromMilliseconds(420));
        }
    }

    private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AnimateScaleX(SearchUnderline, 0.0, TimeSpan.FromMilliseconds(260));
    }

    private static void AnimateScaleX(FrameworkElement target, double to, TimeSpan duration)
    {
        if (target.RenderTransform is not ScaleTransform scale) return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        var animation = new DoubleAnimation(to, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    #endregion

    #region 侧边栏拖拽排序

    private void NavList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = UiHelpers.FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is null || item.DataContext is not NavItem nav) return;

        _navDragIndex = NavItems.IndexOf(nav);
        _navDragStart = e.GetPosition(NavList);
        _navIsDragging = false;
    }

    private void NavList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_navDragIndex < 0 || e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(NavList);
        if (!_navIsDragging)
        {
            if ((position - _navDragStart).Length < 6) return;
            _navIsDragging = true;
        }

        var hitItem = UiHelpers.FindVisualParent<ListBoxItem>(NavList.InputHitTest(position) as DependencyObject);
        if (hitItem?.DataContext is NavItem target && target != NavItems[_navDragIndex])
        {
            var targetIndex = NavItems.IndexOf(target);
            if (targetIndex >= 0 && targetIndex != _navDragIndex)
            {
                NavItems.Move(_navDragIndex, targetIndex);
                _navDragIndex = targetIndex;
            }
        }

        // 拖拽中的条目变淡
        if (NavList.ItemContainerGenerator.ContainerFromIndex(_navDragIndex) is ListBoxItem dragging)
            dragging.Opacity = 0.55;
    }

    private void NavList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        FinishNavDrag();
    }

    private void NavList_MouseLeave(object sender, MouseEventArgs e)
    {
        FinishNavDrag();
    }

    private void NavList_LostMouseCapture(object sender, MouseEventArgs e)
    {
        FinishNavDrag();
    }

    private void FinishNavDrag()
    {
        if (_navDragIndex >= 0 &&
            NavList.ItemContainerGenerator.ContainerFromIndex(_navDragIndex) is ListBoxItem item)
        {
            item.Opacity = 1.0;
            NavList.SelectedIndex = _navDragIndex;
        }

        _navDragIndex = -1;
        _navIsDragging = false;
        NavList.ReleaseMouseCapture();
    }

    #endregion

    #region 窗口操作

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 鼠标未按下等边界情况，忽略
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}

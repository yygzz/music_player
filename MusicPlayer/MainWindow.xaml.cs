using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using MusicPlayer.Helpers;
using MusicPlayer.Models;
using MusicPlayer.Services;
using MusicPlayer.Shell;
using MusicPlayer.Views;

namespace MusicPlayer;

public partial class MainWindow : Window
{
    // ---------- 绑定集合 ----------
    public ObservableCollection<NavItem> NavItems { get; } = new();
    public ObservableCollection<Song> Songs { get; } = new();
    public ObservableCollection<AlbumTile> Albums { get; } = new();

    // ---------- 播放状态 ----------
    private readonly MediaPlayer _player = new();
    private DispatcherTimer _timer = null!;
    private int _currentIndex = -1;
    private Song? _currentSong;
    private bool _isPaused;
    private bool _isSeeking;
    private bool _syncingVolume;

    // ---------- 曲库状态 ----------
    private List<Song> _allSongs = new();
    private string _filterQuery = "";
    private string _folderPath = "";
    private MetadataStore _metadataStore = null!;

    private static readonly string[] SupportedExts =
        { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".wma", ".aac" };

    // 侧边栏拖拽排序状态
    private int _navDragIndex = -1;
    private bool _navIsDragging;
    private Point _navDragStart;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _metadataStore = new MetadataStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicPlayer", "metadata.json"));

        PopulateNav();
        SetupPlayer();
        StartBackgroundAnimations();
        SetWelcome();

        NavList.SelectedIndex = 0; // 触发 SelectionChanged -> 显示首页
    }

    #region 侧边栏导航 / 页面切换

    private void PopulateNav()
    {
        NavItems.Add(new NavItem("首页", "\uE80F", "home"));
        NavItems.Add(new NavItem("歌库", "\uE8F1", "library"));
        NavItems.Add(new NavItem("播放列表", "\uE7C0", "playlist"));
        NavItems.Add(new NavItem("设置", "\uE713", "settings"));
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navIsDragging) return;            // 仅拖拽重排期间不切换页面
        if (NavList.SelectedItem is NavItem nav)
            ShowView(nav.ViewHint);
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        var index = NavItems.ToList().FindIndex(n => n.ViewHint == "settings");
        if (index >= 0) NavList.SelectedIndex = index;
    }

    private void ShowView(string hint)
    {
        HomePanel.Visibility = hint == "home" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPanel.Visibility = hint == "library" ? Visibility.Visible : Visibility.Collapsed;
        PlaylistPanel.Visibility = hint == "playlist" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = hint == "settings" ? Visibility.Visible : Visibility.Collapsed;

        PageTitleText.Text = hint switch
        {
            "home" => "首页",
            "library" => "歌库",
            "playlist" => "播放队列",
            "settings" => "设置",
            _ => ""
        };
        PageSubtitleText.Text = hint switch
        {
            "home" => Songs.Count > 0 ? "点击专辑或播放全部开始聆听" : "导入音乐文件夹，建立你的本地曲库",
            "library" => _filterQuery.Length > 0 ? $"搜索结果 · 匹配 {Songs.Count} 首" : $"共 {_allSongs.Count} 首歌曲",
            "playlist" => "双击歌曲即可播放",
            "settings" => "管理音乐文件夹与播放偏好",
            _ => ""
        };
        RefreshStats();

        var target = hint switch
        {
            "home" => (FrameworkElement)HomePanel,
            "library" => (FrameworkElement)LibraryPanel,
            "playlist" => (FrameworkElement)PlaylistPanel,
            "settings" => (FrameworkElement)SettingsPanel,
            _ => null
        };
        if (target is not null) AnimationFx.FadeInSlide(target);
    }

    private void SetWelcome()
    {
        var hour = DateTime.Now.Hour;
        WelcomeText.Text = hour switch
        {
            >= 5 and < 12 => "早上好",
            >= 12 and < 18 => "下午好",
            _ => "晚上好"
        };
    }

    #endregion

    #region 亚克力窗口

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        AcrylicWindow.Enable(this);
        AcrylicWindow.TryRoundCorners(new WindowInteropHelper(this).Handle);
    }

    #endregion

    #region 背景光斑动画

    private void StartBackgroundAnimations()
    {
        AnimateSpot(Blob1, 0, 480, 0, 380, TimeSpan.FromSeconds(12));
        AnimateSpot(Blob2, 0, -340, 0, 420, TimeSpan.FromSeconds(16));
        AnimateSpot(Blob3, 0, 400, -20, 400, TimeSpan.FromSeconds(14));
        AnimateSpot(Blob4, 0, 440, -420, 420, TimeSpan.FromSeconds(18));
    }

    private static void AnimateSpot(UIElement element,
        double fromX, double toX, double fromY, double toY, TimeSpan duration)
    {
        if (element.RenderTransform is not TranslateTransform t) return;

        var easing = new SineEase { EasingMode = EasingMode.EaseInOut };
        t.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, toX, duration)
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = easing
        });
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, toY, duration)
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = easing
        });
    }

    #endregion

    #region 搜索框：下划线展开 / 实时过滤

    private void SearchTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SearchUnderline.RenderTransform is ScaleTransform scale)
        {
            scale.CenterX = e.GetPosition(SearchUnderline).X;
            AnimateScaleX(SearchUnderline, 1.0, TimeSpan.FromMilliseconds(420));
        }
    }

    private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => AnimateScaleX(SearchUnderline, 0.0, TimeSpan.FromMilliseconds(260));

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filterQuery = SearchTextBox.Text.Trim();
        SearchPlaceholder.Visibility = _filterQuery.Length == 0 ? Visibility.Visible : Visibility.Hidden;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Songs.Clear();
        var q = _filterQuery;
        var number = 1;
        foreach (var s in _allSongs)
        {
            if (q.Length == 0 ||
                s.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Album.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                s.Number = number++;
                Songs.Add(s);
            }
        }
        PageSubtitleText.Text = q.Length > 0 ? $"搜索结果 · 匹配 {Songs.Count} 首" : $"共 {_allSongs.Count} 首歌曲";
        RefreshStats();
    }

    private static void AnimateScaleX(FrameworkElement target, double to, TimeSpan duration)
    {
        if (target.RenderTransform is not ScaleTransform scale) return;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        var ease = to > 0.0
            ? (IEasingFunction)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            : new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(to, duration) { EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    #endregion

    #region 曲库加载

    private void ImportFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择音乐文件夹" };
        if (dlg.ShowDialog() == true)
            LoadFolder(dlg.FolderName);
    }

    private void RefreshFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_folderPath.Length == 0)
            ImportFolderButton_Click(sender, e);
        else
            LoadFolder(_folderPath);
    }

    private void LoadFolder(string path)
    {
        if (!Directory.Exists(path)) return;
        _folderPath = path;

        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            MessageBox.Show(this, "无法读取该文件夹，可能没有访问权限。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _allSongs = files.Select((f, i) => ParseFile(f, i + 1)).ToList();
        _filterQuery = "";
        SearchTextBox.Text = "";
        ApplyFilter();
        RebuildAlbums();
        RefreshStats();

        FolderPathText.Text = _folderPath;
        LibraryCountText.Text = $"共 {_allSongs.Count} 首歌曲";
        QueueCountText.Text = $"共 {Songs.Count} 首";

        if (_allSongs.Count == 0)
            MessageBox.Show(this, "该文件夹里没有找到支持的音频文件。\n支持：MP3 / WAV / M4A / FLAC / OGG / WMA / AAC", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        else
            ShowView(HintOfCurrent());
    }

    private Song ParseFile(string file, int number)
    {
        var meta = MetadataService.Read(file, number);
        meta = _metadataStore.Apply(file, meta);
        return new Song(number, meta.Title, meta.Artist, meta.Album, file);
    }

    private string HintOfCurrent()
    {
        if (LibraryPanel.Visibility == Visibility.Visible) return "library";
        if (PlaylistPanel.Visibility == Visibility.Visible) return "playlist";
        if (SettingsPanel.Visibility == Visibility.Visible) return "settings";
        return "home";
    }

    private void RebuildAlbums()
    {
        Albums.Clear();
        var colorIdx = 0;
        var distinct = new List<string>();
        foreach (var s in _allSongs)
            if (!distinct.Contains(s.Album))
                distinct.Add(s.Album);

        foreach (var album in distinct.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
        {
            Albums.Add(new AlbumTile
            {
                Album = album,
                Count = _allSongs.Count(s => s.Album == album),
                Brush = Palette.At(colorIdx++),
                Badge = album.Length > 0 ? album[..1].ToUpperInvariant() : "♪"
            });
        }

        HomeAlbumCount.Text = Albums.Count.ToString();
        HomeSongCount.Text = _allSongs.Count.ToString();
        AlbumsSectionTitle.Text = Albums.Count > 0 ? "我的专辑" : "我的专辑（导入后显示）";
    }

    private void RefreshStats()
    {
        HomeSongCount.Text = _allSongs.Count.ToString();
        HomeAlbumCount.Text = Albums.Count.ToString();
        LibraryCountText.Text = $"共 {Songs.Count} 首歌曲";
        QueueCountText.Text = $"共 {Songs.Count} 首";
    }

    #endregion

    #region 播放控制

    private void SetupPlayer()
    {
        _player.MediaOpened += (_, _) => OnMediaOpened();
        _player.MediaEnded += (_, _) => PlaySong(NextIndex(_currentIndex));
        _player.Volume = VolumeSlider.Value / 100.0;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => OnTimerTick();
        // 定时器按需启停：仅在播放时运行，避免空闲空转
    }

    private void EnsureTimerRunning()
    {
        if (!_timer.IsEnabled) _timer.Start();
    }

    private void OnMediaOpened()
    {
        if (!_player.NaturalDuration.HasTimeSpan || _currentSong is null) return;
        var total = _player.NaturalDuration.TimeSpan.TotalSeconds;
        SeekSlider.Maximum = total;
        SeekTotalText.Text = Fmt(total);
        _currentSong.DurationText = Fmt(total);
    }

    private void OnTimerTick()
    {
        if (_currentSong is null) return;
        if (!_isSeeking)
            SeekSlider.Value = _player.Position.TotalSeconds;
        SeekTimeText.Text = Fmt(_player.Position.TotalSeconds);
    }

    private int NextIndex(int i) => i < 0 || Songs.Count == 0 ? -1 : (i + 1) % Songs.Count;
    private int PrevIndex(int i) => i < 0 || Songs.Count == 0 ? -1 : (i - 1 + Songs.Count) % Songs.Count;

    private void PlaySong(int index)
    {
        if (index < 0 || index >= Songs.Count) return;

        _currentIndex = index;
        _currentSong = Songs[index];

        foreach (var s in Songs) s.IsPlaying = false;
        _currentSong.IsPlaying = true;

        try
        {
            _player.Stop();
            _player.Open(new Uri(_currentSong.FilePath));
            _player.Play();
            _isPaused = false;
            PlayPauseIcon.Text = "\uE769"; // 暂停图标
            EnsureTimerRunning();

            SeekSlider.Value = 0;
            SeekTimeText.Text = "0:00";
            SeekTotalText.Text = _currentSong.DurationText;

            NowTitleText.Text = _currentSong.Title;
            NowArtistText.Text = _currentSong.ArtistAlbum;

            ScrollListsToCurrent();
        }
        catch (Exception)
        {
            MessageBox.Show(this, "无法播放该文件。", "播放错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ScrollListsToCurrent()
    {
        if (_currentSong is null) return;
        LibraryList.ScrollIntoView(_currentSong);
        QueueList.ScrollIntoView(_currentSong);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSong is null)
        {
            PlayAllButton_Click(sender, e);
            return;
        }
        if (_isPaused)
        {
            _player.Play();
            _isPaused = false;
            PlayPauseIcon.Text = "\uE769";
            EnsureTimerRunning();
        }
        else
        {
            _player.Pause();
            _isPaused = true;
            PlayPauseIcon.Text = "\uE768";
            _timer.Stop();
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
        => PlaySong(PrevIndex(_currentIndex < 0 ? 0 : _currentIndex));

    private void NextButton_Click(object sender, RoutedEventArgs e)
        => PlaySong(NextIndex(_currentIndex < 0 ? -1 : _currentIndex));

    private void PlayAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (Songs.Count > 0) PlaySong(0);
    }

    private void AlbumTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string album }) return;
        for (var i = 0; i < Songs.Count; i++)
        {
            if (Songs[i].Album == album)
            {
                PlaySong(i);
                return;
            }
        }
    }

    private void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryList.SelectedItem is Song s)
            PlaySong(Songs.IndexOf(s));
    }

    private void LibraryList_EditSong(object sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not Song s) return;

        var dlg = new EditSongDialog(s.FilePath, s.Title, s.Artist, s.Album)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (dlg.ShowDialog() != true) return;

        s.Title = dlg.TitleValue;
        s.Artist = dlg.ArtistValue;
        s.Album = dlg.AlbumValue;

        _metadataStore.Set(s.FilePath, s.Title, s.Artist, s.Album);
        if (dlg.WriteTag)
        {
            try { MetadataService.SaveTag(s.FilePath, new SongMetadata(s.Title, s.Artist, s.Album, s.FilePath)); }
            catch { /* 写标签失败不致命 */ }
        }

        RebuildAlbums();
        RefreshStats();
    }

    private void QueueList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QueueList.SelectedItem is Song s)
            PlaySong(Songs.IndexOf(s));
    }

    private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _isSeeking = true;
    private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e) => _isSeeking = false;

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isSeeking || _currentSong is null) return;
        if (_player.NaturalDuration.HasTimeSpan)
        {
            _player.Position = TimeSpan.FromSeconds(e.NewValue);
            SeekTimeText.Text = Fmt(e.NewValue);
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _player.Volume = e.NewValue / 100.0;
        if (_syncingVolume) return;

        // 设置页与底部播放栏各有一个音量滑块，双向同步避免二者显示不一致
        _syncingVolume = true;
        try
        {
            var other = ReferenceEquals(sender, VolumeSlider) ? BottomVolumeSlider : VolumeSlider;
            if (other != null && Math.Abs(other.Value - e.NewValue) > 0.01)
                other.Value = e.NewValue;
        }
        finally
        {
            _syncingVolume = false;
        }
    }

    private static string Fmt(double seconds)
    {
        var s = (int)Math.Round(Math.Max(0, seconds));
        return $"{s / 60}:{s % 60:00}";
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

        if (NavList.ItemContainerGenerator.ContainerFromIndex(_navDragIndex) is ListBoxItem dragging)
            dragging.Opacity = 0.55;
    }

    private void NavList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => FinishNavDrag();
    private void NavList_MouseLeave(object sender, MouseEventArgs e) => FinishNavDrag();
    private void NavList_LostMouseCapture(object sender, MouseEventArgs e) => FinishNavDrag();

    private void FinishNavDrag()
    {
        // 重入守卫：ReleaseMouseCapture() 会同步触发 LostMouseCapture → 再次进入本方法，
        // 以及 MouseLeave 也会触发；无拖拽状态时直接返回，避免重复调用 ShowView。
        if (_navDragIndex < 0 && !_navIsDragging) return;

        var wasDragging = _navIsDragging;   // 先记录：是不是真的拖拽重排过

        if (_navDragIndex >= 0 &&
            NavList.ItemContainerGenerator.ContainerFromIndex(_navDragIndex) is ListBoxItem item)
        {
            item.Opacity = 1.0;
        }

        _navDragIndex = -1;
        _navIsDragging = false;
        NavList.ReleaseMouseCapture();

        // 普通点击已由 SelectionChanged 切页；这里只在拖拽重排后补一次，
        // 避免重复触发淡入动画导致闪烁。
        if (wasDragging && NavList.SelectedItem is NavItem nav)
            ShowView(nav.ViewHint);
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
            try { DragMove(); }
            catch (InvalidOperationException) { /* 忽略边界情况 */ }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_Closed(object? sender, EventArgs e)
    {
        _timer?.Stop();
        _player?.Stop();
        _player?.Close();
    }

    #endregion
}
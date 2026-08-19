using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace MusicPlayer.Models;

/// <summary>属性通知基类</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>侧边栏导航项</summary>
public class NavItem : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string Glyph { get; set; } = "\uE80F";
    public string ViewHint { get; set; } = string.Empty;

    public NavItem() { }
    public NavItem(string title, string glyph, string viewHint)
    {
        Title = title;
        Glyph = glyph;
        ViewHint = viewHint;
    }
}

/// <summary>歌曲条目（对应磁盘上的一个真实文件）</summary>
public class Song : ObservableObject
{
    public int Number { get; set; }
    public string Title { get; set; } = "未知歌曲";
    public string Artist { get; set; } = "未知艺术家";
    public string Album { get; set; } = "未知专辑";
    public string FilePath { get; set; } = string.Empty;

    private string _durationText = "—";
    public string DurationText
    {
        get => _durationText;
        set { _durationText = value; OnPropertyChanged(); }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    public string ArtistAlbum => $"{Artist} · {Album}";

    public Song() { }
    public Song(int number, string title, string artist, string album, string filePath)
    {
        Number = number;
        Title = title;
        Artist = artist;
        Album = album;
        FilePath = filePath;
    }
}

/// <summary>首页展示的专辑卡片</summary>
public class AlbumTile : ObservableObject
{
    public string Album { get; set; } = "未知专辑";
    public int Count { get; set; }
    public Brush Brush { get; set; } = Brushes.SlateGray;
    public string Badge { get; set; } = "?";
}
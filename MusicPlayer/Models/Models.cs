namespace MusicPlayer.Models;

/// <summary>侧边栏导航项</summary>
public class NavItem
{
    public string Title { get; set; } = string.Empty;
    public string Glyph { get; set; } = "\uE80F";

    public NavItem() { }
    public NavItem(string title, string glyph)
    {
        Title = title;
        Glyph = glyph;
    }
}

/// <summary>歌曲条目</summary>
public class Song
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Duration { get; set; } = "0:00";

    public Song() { }
    public Song(int number, string title, string artist, string album, string duration)
    {
        Number = number;
        Title = title;
        Artist = artist;
        Album = album;
        Duration = duration;
    }
}

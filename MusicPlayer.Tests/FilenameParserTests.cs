using Xunit;
using MusicPlayer.Services;

namespace MusicPlayer.Tests;

public class FilenameParserTests
{
    [Fact]
    public void Parses_ArtistDashTitle()
    {
        var m = FilenameParser.Parse(@"C:\Music\周杰伦 - 晴天.mp3", 1);
        Assert.Equal("晴天", m.Title);
        Assert.Equal("周杰伦", m.Artist);
    }

    [Fact]
    public void Parses_AsciiSeparator()
    {
        var m = FilenameParser.Parse(@"C:\Music\Coldplay - Yellow.mp3", 1);
        Assert.Equal("Yellow", m.Title);
        Assert.Equal("Coldplay", m.Artist);
    }

    [Fact]
    public void FallsBackToWholeName_NoSeparator()
    {
        var m = FilenameParser.Parse(@"C:\Music\my song.mp3", 1);
        Assert.Equal("my song", m.Title);
        Assert.Equal("未知艺术家", m.Artist);
    }

    [Fact]
    public void UsesParentDirAsAlbum()
    {
        var m = FilenameParser.Parse(@"C:\Music\AlbumA\track.mp3", 1);
        Assert.Equal("AlbumA", m.Album);
    }
}
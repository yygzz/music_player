using System.IO;
using Xunit;
using MusicPlayer.Services;

namespace MusicPlayer.Tests;

public class MetadataStoreTests
{
    [Fact]
    public void Apply_UsesOverride_WhenPresent()
    {
        var f = Path.GetTempFileName();
        try
        {
            var store = new MetadataStore(f);
            store.Set(@"C:\x\a.mp3", "新标题", "新歌手", "新专辑");
            var m = store.Apply(@"C:\x\a.mp3", new SongMetadata("旧", "旧歌手", "旧专辑", @"C:\x\a.mp3"));
            Assert.Equal("新标题", m.Title);
            Assert.Equal("新歌手", m.Artist);
            Assert.Equal("新专辑", m.Album);
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Apply_FallsBack_WhenNoOverride()
    {
        var f = Path.GetTempFileName();
        try
        {
            var store = new MetadataStore(f);
            var m = store.Apply(@"C:\x\b.mp3", new SongMetadata("标题", "歌手", "专辑", @"C:\x\b.mp3"));
            Assert.Equal("标题", m.Title);
        }
        finally { File.Delete(f); }
    }
}
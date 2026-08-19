using System;
using TagLib;

namespace MusicPlayer.Services;

/// <summary>读取/写回音频文件内嵌标签（ID3 / Vorbis / MP4 / ASF）。</summary>
public static class MetadataService
{
    public static SongMetadata Read(string filePath, int number)
    {
        var fallback = FilenameParser.Parse(filePath, number);
        try
        {
            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;
            var artist = FirstNonEmpty(tag.JoinedPerformers, fallback.Artist);
            artist = FirstNonEmpty(artist, tag.FirstPerformer);
            artist = FirstNonEmpty(artist, fallback.Artist);
            return new SongMetadata(
                FirstNonEmpty(tag.Title, fallback.Title),
                artist,
                FirstNonEmpty(tag.Album, fallback.Album),
                filePath);
        }
        catch
        {
            return fallback; // 损坏 / 无标签文件
        }
    }

    public static void SaveTag(string filePath, SongMetadata meta)
    {
        using var file = TagLib.File.Create(filePath);
        file.Tag.Title = meta.Title;
        file.Tag.Performers = new[] { meta.Artist };
        file.Tag.Album = meta.Album;
        file.Save();
    }

    private static string FirstNonEmpty(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
}
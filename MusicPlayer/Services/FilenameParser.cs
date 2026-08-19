using System;
using System.IO;

namespace MusicPlayer.Services;

/// <summary>无标签文件时的文件名回退解析（纯逻辑，可单测）。</summary>
public static class FilenameParser
{
    public static SongMetadata Parse(string filePath, int number)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var title = fileName;
        var artist = "未知艺术家";

        // 兼容中英文连字符、长破折号等多种分隔变体
        string[] separators = { " - ", " – ", " -", "- ", "‐" };
        foreach (var sep in separators)
        {
            var idx = fileName.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                var a = fileName[..idx].Trim();
                var t = fileName[(idx + sep.Length)..].Trim();
                if (t.Length > 0) { artist = a.Length > 0 ? a : artist; title = t; }
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(title)) title = string.IsNullOrWhiteSpace(fileName) ? "未知歌曲" : fileName;
        if (string.IsNullOrWhiteSpace(artist)) artist = "未知艺术家";

        var dir = Path.GetDirectoryName(filePath);
        var album = !string.IsNullOrEmpty(dir) && dir != Path.GetPathRoot(filePath)
            ? new DirectoryInfo(dir).Name
            : "未知专辑";

        return new SongMetadata(title.Trim(), artist.Trim(), album, filePath);
    }
}

public readonly record struct SongMetadata(string Title, string Artist, string Album, string FilePath);
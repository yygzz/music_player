using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MusicPlayer.Services;

/// <summary>以绝对路径为键，持久化用户手动编辑的标题/艺术家/专辑覆盖。</summary>
public sealed class MetadataStore
{
    private readonly string _path;
    private readonly Dictionary<string, Override> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Override
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
    }

    public MetadataStore(string path)
    {
        _path = path;
        Load();
    }

    public SongMetadata Apply(string absolutePath, SongMetadata baseMeta)
    {
        if (!_overrides.TryGetValue(absolutePath, out var o)) return baseMeta;
        return baseMeta with
        {
            Title = string.IsNullOrWhiteSpace(o.Title) ? baseMeta.Title : o.Title,
            Artist = string.IsNullOrWhiteSpace(o.Artist) ? baseMeta.Artist : o.Artist,
            Album = string.IsNullOrWhiteSpace(o.Album) ? baseMeta.Album : o.Album
        };
    }

    public void Set(string absolutePath, string title, string artist, string album)
    {
        _overrides[absolutePath] = new Override { Title = title, Artist = artist, Album = album };
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, Override>>(File.ReadAllText(_path));
            if (data is null) return;
            _overrides.Clear();
            foreach (var (k, v) in data) _overrides[k] = v;
        }
        catch { /* 损坏缓存忽略 */ }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_overrides, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 写入失败静默 */ }
    }
}
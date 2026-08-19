using System.Windows.Media;

namespace MusicPlayer.Helpers;

/// <summary>预冻结的专辑配色画刷，避免在专辑重建时反复解析/创建。</summary>
public static class Palette
{
    private static readonly string[] Hex =
    {
        "#FF5C8A", "#8B5CF6", "#59C2FF", "#FFB35C", "#2DD4BF",
        "#F472B6", "#3B82F6", "#10B981", "#EC4899", "#F59E0B"
    };

    private static readonly SolidColorBrush[] Brushes = CreateBrushes();

    private static SolidColorBrush[] CreateBrushes()
    {
        var list = new SolidColorBrush[Hex.Length];
        for (var i = 0; i < Hex.Length; i++)
        {
            list[i] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Hex[i]));
            list[i].Freeze();
        }
        return list;
    }

    public static SolidColorBrush At(int index)
        => Brushes[(index % Brushes.Length + Brushes.Length) % Brushes.Length];
}
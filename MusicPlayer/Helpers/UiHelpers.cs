using System.Windows;
using System.Windows.Media;

namespace MusicPlayer.Helpers;

/// <summary>视觉树辅助方法</summary>
public static class UiHelpers
{
    /// <summary>向上查找指定类型的可视父级</summary>
    public static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T target) return target;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MusicPlayer.Shell;

/// <summary>
/// 为 WPF 窗口开启 Windows 10/11 的"亚克力"（Acrylic）背景。
/// 经典做法：WindowStyle=None + AllowsTransparency=False + Background=Transparent，
/// 再用 DwmExtendFrameIntoClientArea 扩展 DWM 框架，并用 SetWindowCompositionAttribute
/// 开启 ACCENT_ENABLE_ACRYLICBLURBEHIND 系统级模糊。
/// </summary>
public static class AcrylicWindow
{
    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;   // AABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int leftWidth;
        public int rightWidth;
        public int topHeight;
        public int bottomHeight;
    }

    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
    private const int WCA_ACCENT_POLICY = 19;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    #endregion

    /// <summary>
    /// 在窗口句柄可用后调用（通常在 SourceInitialized 中）。
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="tintAABBGGRR">亚克力着色，默认 0xCC141414（深色半透明）</param>
    public static void Enable(Window window, uint tintAABBGGRR = 0xCC141414)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            // 句柄尚未创建：延迟到 SourceInitialized 后再应用
            window.SourceInitialized += (_, _) => Apply(new WindowInteropHelper(window).Handle, tintAABBGGRR);
            return;
        }

        Apply(hwnd, tintAABBGGRR);
    }

    private static void Apply(IntPtr hwnd, uint tintAABBGGRR)
    {
        if (hwnd == IntPtr.Zero) return;

        // 1) 扩展 DWM 框架，让系统亚克力模糊渲染在客户端区域背后
        var margins = new Margins { leftWidth = -1, rightWidth = -1, topHeight = -1, bottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // 2) 开启系统级亚克力模糊（Windows 10 1803+ / Windows 11）
        var accent = new AccentPolicy
        {
            AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2, // 让模糊覆盖整个客户区
            GradientColor = tintAABBGGRR,
            AnimationId = 0
        };

        var data = new WindowCompositionAttributeData
        {
            Attribute = WCA_ACCENT_POLICY,
            SizeOfData = Marshal.SizeOf<AccentPolicy>(),
            Data = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>())
        };
        try
        {
            Marshal.StructureToPtr(accent, data.Data, false);
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(data.Data);
        }
    }

    /// <summary>
    /// 让无边框窗口使用系统圆角（Windows 11 生效，Windows 10 无副作用）。
    /// </summary>
    public static void TryRoundCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }
}

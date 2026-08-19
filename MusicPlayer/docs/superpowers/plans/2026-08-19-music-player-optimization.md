# 音乐播放器「换页 / 性能 / 元数据 / 内存 / 液体玻璃 UI / 动效」实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 一次性解决用户反馈的六个问题：换页闪烁、约 10 FPS 卡顿、歌名乱码需可自定义、内存占用过高、UI 缺乏液体/磨砂感、动效卡顿且粗糙，并构建通过、推送到 GitHub 分支。

**Architecture:** 这是一个 .NET 8 WPF 桌面程序（`net8.0-windows10.0.17763.0` + `EnableWindowsTargeting`，可在非 Windows 沙盒还原/编译，运行时需 Windows）。亚克力窗口由 `Shell/AcrylicWindow.cs` 通过 DWM API 实现；界面采用半透明「液体玻璃」卡片与 GPU 加速的 `RenderTransform` 动画（背景光斑为 `TranslateTransform`，避免 `BlurEffect`）。本轮聚焦**尚未落地**的四个真实缺陷：①`FinishNavDrag` 因 `ReleaseMouseCapture` 触发 `LostMouseCapture` 而级联重入、并对普通点击重复调用 `ShowView` 造成换页闪烁；②`LibraryList`/`QueueList` 被外层 `ScrollViewer` 包裹导致 `ListBox` 虚拟化失效（上万首歌会实例化全部行 → 卡顿 + 高内存）；③残留的 1px 硬分割线与不统一的圆角破坏液体感；④圆形图标按钮悬停用「替换 `ScaleTransform`」实现缩放（瞬时跳变），动效粗糙。另需补元数据纯逻辑单测并提交已完成的改动。

**Tech Stack:** C# / WPF / .NET 8（`net8.0-windows10.0.17763.0`）、TagLibSharp 2.3.0、System.Text.Json、xUnit 2.8.1。

**结构总览（本计划涉及的文件）：**
- 修改 `MusicPlayer/MainWindow.xaml.cs` — 修复 `FinishNavDrag` 重入与重复 `ShowView`。
- 修改 `MusicPlayer/MainWindow.xaml` — 移除外层 `ScrollViewer`（恢复虚拟化）、删除硬分割线、统一圆角。
- 修改 `MusicPlayer/App.xaml` — 圆形按钮悬停改为平滑动画缩放。
- 新增 `MusicPlayer.Tests/MusicPlayer.Tests.csproj` — xUnit 测试工程。
- 新增 `MusicPlayer.Tests/FilenameParserTests.cs` — 文件名解析回退单测。
- 新增 `MusicPlayer.Tests/MetadataStoreTests.cs` — 用户自定义覆盖持久化单测。

> **已完成（勿重复实现）：** `BlurEffect`/`DropShadowEffect` 已移除、光斑已改 `TranslateTransform`；`TagLibSharp` 标签读取 + `FilenameParser` 回退 + `EditSongDialog` 编辑 + `MetadataStore` JSON 持久化已就位；`Palette` 画刷已冻结；定时器已按需启停；窗口关闭已释放 `MediaPlayer`。这些改动目前**尚未提交**，将在 Task 6 一并提交。
>
> 提交粒度：每个 Task 结束单独 `git commit`。远端 `origin https://github.com/yygzz/music_player`，工作分支 `trae/agent-t9vGPT`（无上游，需 `-u`）。

---

## Task 1: 修复换页闪烁（`FinishNavDrag` 重入 + 重复 `ShowView`）

**Files:**
- Modify: `MusicPlayer/MainWindow.xaml.cs`（`FinishNavDrag`）

**根因：** `FinishNavDrag` 同时挂在 `PreviewMouseLeftButtonUp`、`MouseLeave`、`LostMouseCapture` 三个事件上，且内部调用 `NavList.ReleaseMouseCapture()` 会**同步触发** `LostMouseCapture` 再次进入 `FinishNavDrag`。又因为方法末尾无条件执行 `ShowView(nav.ViewHint)`，每次普通点击都会在 `SelectionChanged`（鼠标按下已切一次）+ `MouseUp` + `LostMouseCapture` 之间重复触发多次淡入，动画被中断重放，表现为「换页闪烁 / 不跟手 / 看起来像没换页」。

- [ ] **Step 1: 改写 `FinishNavDrag`，加入重入守卫并仅在真正拖拽后补一次切页**

将 [MainWindow.xaml.cs](file:///workspace/MusicPlayer/MainWindow.xaml.cs#L584-L598) 的 `FinishNavDrag` 替换为：

```csharp
private void FinishNavDrag()
{
    // 重入守卫：ReleaseMouseCapture() 会同步触发 LostMouseCapture → 再次进入本方法，
    // 以及 MouseLeave 也会触发；无拖拽状态时直接返回，避免重复调用 ShowView。
    if (_navDragIndex < 0 && !_navIsDragging) return;

    var wasDragging = _navIsDragging;   // 先记录：是不是真的拖拽重排过

    if (_navDragIndex >= 0 &&
        NavList.ItemContainerGenerator.ContainerFromIndex(_navDragIndex) is ListBoxItem item)
    {
        item.Opacity = 1.0;
    }

    _navDragIndex = -1;
    _navIsDragging = false;
    NavList.ReleaseMouseCapture();

    // 普通点击已由 SelectionChanged 切页；这里只在拖拽重排后补一次，
    // 避免重复触发淡入动画导致闪烁。
    if (wasDragging && NavList.SelectedItem is NavItem nav)
        ShowView(nav.ViewHint);
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build MusicPlayer/MusicPlayer.csproj -c Debug
```

Expected：`BUILD SUCCEEDED`，无错误。

- [ ] **Step 3: 手工验证**

运行程序：依次点击「首页 / 歌库 / 播放列表 / 设置」，确认每个页面只淡入一次、无闪烁；拖拽侧边栏重排后页面仍与当前选中项一致。

- [ ] **Step 4: Commit**

```bash
git -C /workspace/MusicPlayer add MusicPlayer/MainWindow.xaml.cs
git -C /workspace/MusicPlayer commit -m "fix: 修复 FinishNavDrag 重入导致的换页闪烁"
```

---

## Task 2: 恢复列表虚拟化（性能 + 内存）

**Files:**
- Modify: `MusicPlayer/MainWindow.xaml`（`LibraryList`、`QueueList`）

**根因：** 两个歌曲列表都被单独包在外层 `<ScrollViewer>` 里。`ListBox` 默认的 UI 虚拟化依赖它**自带**的 `ScrollViewer` 提供有限视口；一旦外面再套一个 `ScrollViewer`，`ListBox` 的高度变为无限，虚拟化失效，所有行都会实例化。上千首歌时，(a) 首屏与滚动严重掉帧，(b) 每个 `ListBoxItem` 都是完整可视化树，内存随歌曲数线性飙升——这正是「卡顿 ~10 FPS」与「内存比浏览器还高」的共同主因。

- [ ] **Step 1: 移除歌库列表的外层 `ScrollViewer`，显式开启虚拟化**

将 [MainWindow.xaml](file:///workspace/MusicPlayer/MainWindow.xaml#L375-L380) 中 `LibraryList` 的外层包裹由：

```xml
<Border Grid.Row="1" Style="{StaticResource GlassCardStyle}">
    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
        <ListBox x:Name="LibraryList" Background="Transparent" BorderThickness="0"
                 ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                 MouseDoubleClick="LibraryList_MouseDoubleClick"
                 ItemsSource="{Binding Songs}">
```

改为：

```xml
<Border Grid.Row="1" Style="{StaticResource GlassCardStyle}" Padding="4">
    <ListBox x:Name="LibraryList" Background="Transparent" BorderThickness="0"
             ScrollViewer.HorizontalScrollBarVisibility="Disabled"
             ScrollViewer.VerticalScrollBarVisibility="Auto"
             VirtualizingPanel.IsVirtualizing="True"
             VirtualizingPanel.VirtualizationMode="Recycling"
             MouseDoubleClick="LibraryList_MouseDoubleClick"
             ItemsSource="{Binding Songs}">
```

同时删除该 `ListBox` 结束标签后多余的 `</ScrollViewer>`（原外层 ScrollViewer 的闭合），保持 `</Border>` 只闭合一次。

- [ ] **Step 2: 对播放列表 `QueueList` 做同样处理**

将 [MainWindow.xaml](file:///workspace/MusicPlayer/MainWindow.xaml#L475-L480) 中 `QueueList` 的外层包裹由：

```xml
<Border Grid.Row="1" Style="{StaticResource GlassCardStyle}">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <ListBox x:Name="QueueList" Background="Transparent" BorderThickness="0"
                 ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                 MouseDoubleClick="QueueList_MouseDoubleClick"
                 ItemsSource="{Binding Songs}">
```

改为：

```xml
<Border Grid.Row="1" Style="{StaticResource GlassCardStyle}" Padding="4">
    <ListBox x:Name="QueueList" Background="Transparent" BorderThickness="0"
             ScrollViewer.HorizontalScrollBarVisibility="Disabled"
             ScrollViewer.VerticalScrollBarVisibility="Auto"
             VirtualizingPanel.IsVirtualizing="True"
             VirtualizingPanel.VirtualizationMode="Recycling"
             MouseDoubleClick="QueueList_MouseDoubleClick"
             ItemsSource="{Binding Songs}">
```

同样删除其结束标签后多余的 `</ScrollViewer>`。

- [ ] **Step 3: 编译验证**

```bash
dotnet build MusicPlayer/MusicPlayer.csproj -c Debug
```

Expected：`BUILD SUCCEEDED`。

- [ ] **Step 4: 手工验证**

导入一个含 **1000+ 首歌曲** 的目录，滚动歌库列表，观察内存（任务管理器）不再随曲目数线性增长，滚动流畅接近 60 FPS。

- [ ] **Step 5: Commit**

```bash
git -C /workspace/MusicPlayer add MusicPlayer/MainWindow.xaml
git -C /workspace/MusicPlayer commit -m "perf: 移除外层 ScrollViewer 恢复 ListBox 虚拟化，降低卡顿与内存"
```

---

## Task 3: 液体玻璃 UI 打磨（去硬线、统一圆角）

**Files:**
- Modify: `MusicPlayer/MainWindow.xaml`

**根因：** 残留两条 1px 硬分割线（设置页、底部播放栏）属于「线条审美」，破坏液体磨砂的整体感；同时侧边栏项、曲库行、专辑卡仍用 `CornerRadius="6/8"` 的偏小圆角，与 `GlassCardStyle` 的 16px 不统一。

- [ ] **Step 1: 删除设置页硬分割线并改用间距**

将 [MainWindow.xaml](file:///workspace/MusicPlayer/MainWindow.xaml#L556-L560) 中：

```xml
<Rectangle Height="1" Fill="#1AFFFFFF" Margin="0,22,0,20"/>
<TextBlock Text="音量" FontSize="13" Foreground="{StaticResource TextSecondaryBrush}"/>
<Slider x:Name="VolumeSlider" Style="{StaticResource ProgressSliderStyle}" Minimum="0"
        Maximum="100" Value="70" Margin="0,10,0,0"
        ValueChanged="VolumeSlider_ValueChanged"/>
```

改为（删除分隔线，`TextBlock` 顶部改用 `Margin` 留白）：

```xml
<TextBlock Text="音量" FontSize="13" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,24,0,0"/>
<Slider x:Name="VolumeSlider" Style="{StaticResource ProgressSliderStyle}" Minimum="0"
        Maximum="100" Value="70" Margin="0,10,0,0"
        ValueChanged="VolumeSlider_ValueChanged"/>
```

- [ ] **Step 2: 删除底部播放栏的硬分割线**

删除 [MainWindow.xaml](file:///workspace/MusicPlayer/MainWindow.xaml#L635) 中的整行：

```xml
<Rectangle Width="1" Height="26" Fill="#1AFFFFFF" Margin="20,0"/>
```

（该行隔开音量图标与音量滑块，删除后由图标自身的 `Margin` 保持间距即可。）

- [ ] **Step 3: 统一圆角**

在 `MainWindow.xaml` 中做如下替换：

| 位置 | 现值 | 新值 |
|------|------|------|
| 侧边栏导航项 `ItemBorder`（`CornerRadius="6"`） | 6 | 10 |
| 专辑卡片 `Border`（`CornerRadius="8"`） | 8 | 12 |
| 歌库行 `Row`（`CornerRadius="6"`） | 6 | 10 |
| 播放列表行 `Row`（`CornerRadius="6"`） | 6 | 10 |

例如专辑卡片 [MainWindow.xaml](file:///workspace/MusicPlayer/MainWindow.xaml#L331) 的 `CornerRadius="8"` → `CornerRadius="12"`，导航项 [MainWindow.xaml](file:///workspace/MusicPlayer/MainWindow.xaml#L129) 的 `CornerRadius="6"` → `CornerRadius="10"`，其余同理。

- [ ] **Step 4: 编译验证**

```bash
dotnet build MusicPlayer/MusicPlayer.csproj -c Debug
```

Expected：`BUILD SUCCEEDED`。

- [ ] **Step 5: Commit**

```bash
git -C /workspace/MusicPlayer add MusicPlayer/MainWindow.xaml
git -C /workspace/MusicPlayer commit -m "style: 去除硬分割线并统一圆角，强化液体玻璃质感"
```

---

## Task 4: 动效打磨（圆形按钮悬停平滑缩放）

**Files:**
- Modify: `MusicPlayer/App.xaml`（`RoundIconButtonStyle`、`RoundPlayButtonStyle`）

**根因：** `RoundIconButtonStyle` / `RoundPlayButtonStyle` 的悬停触发用 `<Setter ... Property="RenderTransform"><ScaleTransform ScaleX="1.06"/></Setter>` **整体替换** `RenderTransform`，是瞬时的「跳变」而非动画；与 `GlowButtonStyle` 平滑的 `CubicEase` 缩放风格不一致，显得粗糙。

- [ ] **Step 1: 将 `RoundIconButtonStyle` 悬停改为 Storyboard 平滑缩放**

把 [App.xaml](file:///workspace/MusicPlayer/App.xaml#L106-L113) 中该样式的 `IsMouseOver` 触发块：

```xml
<Trigger Property="IsMouseOver" Value="True">
    <Setter TargetName="Root" Property="Background" Value="{StaticResource HoverBrush}" />
    <Setter TargetName="Root" Property="RenderTransform">
        <Setter.Value>
            <ScaleTransform ScaleX="1.06" ScaleY="1.06"/>
        </Setter.Value>
    </Setter>
</Trigger>
```

替换为：

```xml
<Trigger Property="IsMouseOver" Value="True">
    <Setter TargetName="Root" Property="Background" Value="{StaticResource HoverBrush}" />
    <Trigger.EnterActions>
        <BeginStoryboard>
            <Storyboard>
                <DoubleAnimation Storyboard.TargetName="Root"
                                 Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                 To="1.06" Duration="0:0:0.12">
                    <DoubleAnimation.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </DoubleAnimation.EasingFunction>
                </DoubleAnimation>
                <DoubleAnimation Storyboard.TargetName="Root"
                                 Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                                 To="1.06" Duration="0:0:0.12">
                    <DoubleAnimation.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </DoubleAnimation.EasingFunction>
                </DoubleAnimation>
            </Storyboard>
        </BeginStoryboard>
    </Trigger.EnterActions>
    <Trigger.ExitActions>
        <BeginStoryboard>
            <Storyboard>
                <DoubleAnimation Storyboard.TargetName="Root"
                                 Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                 To="1.0" Duration="0:0:0.12"/>
                <DoubleAnimation Storyboard.TargetName="Root"
                                 Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                                 To="1.0" Duration="0:0:0.12"/>
            </Storyboard>
        </BeginStoryboard>
    </Trigger.ExitActions>
</Trigger>
```

- [ ] **Step 2: 对 `RoundPlayButtonStyle` 做同样处理（缩放值 1.08）**

把 [App.xaml](file:///workspace/MusicPlayer/App.xaml#L142-L149) 中该样式的 `IsMouseOver` 触发块：

```xml
<Trigger Property="IsMouseOver" Value="True">
    <Setter TargetName="Root" Property="RenderTransform">
        <Setter.Value>
            <ScaleTransform ScaleX="1.08" ScaleY="1.08"/>
        </Setter.Value>
    </Setter>
</Trigger>
```

替换为与 Step 1 结构相同、但 `To="1.08"` 的 `EnterActions`/`ExitActions` Storyboard。

- [ ] **Step 3: 编译验证**

```bash
dotnet build MusicPlayer/MusicPlayer.csproj -c Debug
```

Expected：`BUILD SUCCEEDED`。

- [ ] **Step 4: 手工验证**

悬停「上一首 / 下一首 / 播放」按钮，确认缩放平滑跟手、移出后平滑回落。

- [ ] **Step 5: Commit**

```bash
git -C /workspace/MusicPlayer add MusicPlayer/App.xaml
git -C /workspace/MusicPlayer commit -m "style: 圆形按钮悬停改为平滑缩放动画"
```

---

## Task 5: 补全元数据纯逻辑单测（乱码 / 自定义回归）

**Files:**
- Create: `MusicPlayer.Tests/MusicPlayer.Tests.csproj`
- Create: `MusicPlayer.Tests/FilenameParserTests.cs`
- Create: `MusicPlayer.Tests/MetadataStoreTests.cs`

**背景：** `Services/FilenameParser.cs` 与 `Services/MetadataStore.cs` 是纯逻辑（不依赖 WPF 视觉树），可单测。目前元数据链路（TagLib 读标签 → 文件名回退 → 用户编辑覆盖持久化）已实现但缺少测试工程，补齐后把「乱码文件名回退解析」与「用户自定义覆盖持久化」固化为回归。

- [ ] **Step 1: 创建测试工程**

新建 `MusicPlayer.Tests/MusicPlayer.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.17763.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../MusicPlayer/MusicPlayer.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 写 `FilenameParser` 测试**

新建 `MusicPlayer.Tests/FilenameParserTests.cs`：

```csharp
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
```

- [ ] **Step 3: 写 `MetadataStore` 测试**

新建 `MusicPlayer.Tests/MetadataStoreTests.cs`：

```csharp
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
```

- [ ] **Step 4: 运行测试**

```bash
dotnet test MusicPlayer.Tests/MusicPlayer.Tests.csproj
```

Expected：6 passed（需在装有 .NET SDK 的 Windows 环境执行；本远程沙盒为 Linux 且无 `dotnet`，无法在此运行）。

- [ ] **Step 5: Commit**

```bash
git -C /workspace/MusicPlayer add MusicPlayer.Tests
git -C /workspace/MusicPlayer commit -m "test: 补充文件名解析与覆盖持久化的纯逻辑单测"
```

---

## Task 6: 构建验证并提交 / 推送到 GitHub

**Files:** 无代码改动。需 Windows + .NET 8 SDK。

- [ ] **Step 1: 还原与构建**

```bash
dotnet build MusicPlayer/MusicPlayer.csproj -c Debug
```

Expected：`BUILD SUCCEEDED`，无错误、无警告。

- [ ] **Step 2: 运行并逐条核对六个问题**

1. **换页**：点击侧边栏四项，面板与 `PageTitleText` 正确切换且只淡入一次、无闪烁。
2. **帧率**：光斑平滑移动、大曲库滚动流畅，任务管理器 GPU/CPU 占用显著下降。
3. **乱码 / 自定义**：导入含中文 / 非 ASCII 文件名目录，歌名正确；右键歌曲 →「编辑歌曲信息」→ 修改后列表即时刷新，重新扫描后仍保留。
4. **内存**：空闲不空转，播放时内存无明显增长，滚动数千首歌不随曲量线性膨胀，关闭后释放。
5. **液体感**：无 1px 硬描边 / 分割线，圆角圆润统一，卡片渐变磨砂，按钮悬停轻微平滑放大。
6. **动效**：页面切换淡入流畅、搜索下划线回弹展开、圆形按钮缩放平滑跟手。

- [ ] **Step 3: 确认工作树状态并提交全部改动**

```bash
git -C /workspace/MusicPlayer status
git -C /workspace/MusicPlayer add MusicPlayer MusicPlayer.Tests
git -C /workspace/MusicPlayer commit -m "feat: 完成换页/性能/元数据/内存/液体玻璃UI/动效优化"
```

（若 Task 1–5 已各自提交，此步仅提交顺序上尚未纳入的零散改动，例如 `docs/` 计划文件可一并加入。）

- [ ] **Step 4: 推送到远端分支**

```bash
git -C /workspace/MusicPlayer push -u origin trae/agent-t9vGPT
```

- [ ] **Step 5: （可选）发起 Pull Request 到 main**

用 GitHub 连接器对比 `trae/agent-t9vGPT` → `main` 并发起 PR，标题示例：`fix: 修复换页闪烁、提升帧率并支持歌曲信息自定义`。

---

## 完成校验清单（全部 Task 之后）

- [ ] `dotnet test MusicPlayer.Tests/MusicPlayer.Tests.csproj` 全绿（6 项）。
- [ ] `dotnet build MusicPlayer/MusicPlayer.csproj -c Debug` 无错误。
- [ ] 六个问题在 Windows 上逐一核对通过。
- [ ] 改动已推送 `origin/trae/agent-t9vGPT`（可选：已开 PR）。

> **环境限制说明：** 本远程沙盒为 Linux 且未安装 .NET SDK（`dotnet not found`），`dotnet build` / `dotnet test` / WPF 运行均无法在此执行，只能在装有 .NET 8 SDK 的 Windows 环境验证。各 Task 的编译 / 测试步骤需在 Windows 侧运行；纯代码编辑与 `git` 提交 / 推送可在当前沙盒完成。
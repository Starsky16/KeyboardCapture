# Starsky16.KeyboardCapture.Abstractions

[KeyboardCapture](https://github.com/Starsky16/KeyboardCapture) 插件的**共享接口包**：只包含接口与数据模型（无任何钩子实现、无第三方依赖），供其他 [ClassIsland](https://classisland.tech) 插件引用后订阅全局键盘按键事件。

- 包内容：`IKeyboardCaptureService`、`KeyboardKey` / `KeyboardKeys`、`KeyboardKeyEventArgs`、`KeyModifiers`
- 目标框架：`net8.0`
- 配套插件：`Starsky16.KeyboardCapture`（实现方，需在 ClassIsland 中安装）

## 这个包解决什么问题

ClassIsland 插件之间是独立的程序集，插件 A 想使用插件 B 提供的能力，不能直接引用 B 的 dll（会导致类型标识不一致、加载冲突）。本包把「键盘捕捉服务」的**契约**单独抽出来发布到 NuGet：实现方（KeyboardCapture 插件）与消费方（你的插件）都引用同一个包，于是双方看到的是同一个 `IKeyboardCaptureService` 类型，可以在 ClassIsland 的 IoC 容器中正常注入与订阅。

## 安装

```powershell
dotnet add package Starsky16.KeyboardCapture.Abstractions
```

## 使用前提

1. 使用者的 ClassIsland 中已安装 `Starsky16.KeyboardCapture` 插件（它负责把 `IKeyboardCaptureService` 注册到容器并启动全局钩子）。
2. 在你的插件清单 `manifest.yml` 中声明依赖：

```yaml
dependencies:
  - id: Starsky16.KeyboardCapture
```

若希望「没装 KeyboardCapture 时插件也能正常工作」，把它声明为**可选依赖**：

```yaml
dependencies:
  - id: Starsky16.KeyboardCapture
    isRequired: false
```

## 用法

### 必选依赖：构造注入

```csharp
using KeyboardCapture.Abstractions;

public class YourService
{
    private readonly IKeyboardCaptureService _capture;

    public YourService(IKeyboardCaptureService capture) => _capture = capture;

    public void Subscribe()
    {
        _capture.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyboardKeyEventArgs e)
    {
        // 精确匹配：键名 + 修饰键（修饰键需完全一致，避免 Ctrl+F5 误触发 Ctrl+Shift+F5）
        if (e.Key.Name == KeyboardKeys.F5 && e.Modifiers == KeyModifiers.Ctrl)
        {
            // Ctrl+F5 被按下
        }
    }
}
```

获取服务即可订阅，主插件会以托管服务的形式随 ClassIsland 自动启动钩子；只有在你显式调用过 `Stop()` 后才需要自己 `Start()`。

### 可选依赖：从应用主机取服务

声明为可选依赖时不能使用构造注入（服务可能不存在），改用 `IAppHost.TryGetService<T>()`：

```csharp
using ClassIsland.Shared;
using KeyboardCapture.Abstractions;

private IKeyboardCaptureService? _capture;

public void TryAttach()
{
    _capture ??= IAppHost.TryGetService<IKeyboardCaptureService>();
    if (_capture is null)
    {
        // KeyboardCapture 未安装或尚未加载，稍后重试（例如在宿主服务里每几秒重试一次）
        return;
    }

    _capture.KeyDown += OnKeyDown;
}
```

> 插件加载顺序不保证，`TryGetService` 首次可能返回 `null`。若在插件初始化阶段取不到，建议在后台服务中定时重试，而不是直接判定不可用。

## API 一览

| 类型 | 说明 |
| --- | --- |
| `IKeyboardCaptureService` | 全局键盘捕捉服务（`IsCapturing` / `Start()` / `Stop()` / `KeyDown` / `KeyUp`） |
| `KeyboardKey` | 单个按键：`Code`（对齐 UIOHook 键码，跨平台统一）+ `Name`（人类可读键名，如 `"A"`、`"F5"`、`"LeftCtrl"`） |
| `KeyboardKeys` | 常用键名常量（`A`–`Z`、`Digit0`–`Digit9`、`F1`–`F24`、`LeftCtrl`、`ArrowUp = "Up"`、`Space`、`Escape` 等），用于与 `KeyboardKey.Name` 比较 |
| `KeyboardKeyEventArgs` | `Key`、`Modifiers`、`IsKeyDown`、`IsAutoRepeat`（按住键的系统自动重复）、`TimestampMs`（单调递增毫秒） |
| `KeyModifiers` | `[Flags]`：`None` / `Ctrl` / `Alt` / `Shift` / `Meta`（Win / Cmd / Super） |

## 注意事项

- **后台线程**：事件在钩子线程触发，更新 UI 请自行封送到 Avalonia 的 `Dispatcher.UIThread`；耗时操作请转到线程池，避免阻塞按键分发。
- **自动重复**：按住按键会持续产生 `IsAutoRepeat == true` 的按下事件（由插件根据按键是否仍处于按下状态推断，而非系统原生标志），做快捷键触发时通常应忽略它们。
- **非独占钩子**：仅观察、不拦截按键，不会抢占系统热键（`RegisterHotKey`）或被按下的组合，也不影响前台程序接收按键。
- **异常隔离**：单个订阅者的处理程序抛出的异常不会影响其他订阅者，也不会中断钩子分发。
- **资源释放**：`IKeyboardCaptureService` 实现 `IDisposable`，其生命周期由 ClassIsland 容器管理，消费方不要自行释放。

## 更多信息

- 项目主页与完整说明：<https://github.com/Starsky16/KeyboardCapture>
- 最小消费者示例：仓库中的 `demo/KeyboardCapture.DemoPlugin/`
- 问题反馈：<https://github.com/Starsky16/KeyboardCapture/issues>

## 许可

[GPL-3.0-only](https://github.com/Starsky16/KeyboardCapture/blob/main/LICENSE.txt)，与 ClassIsland 生态保持一致。

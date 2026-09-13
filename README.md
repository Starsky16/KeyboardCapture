# KeyboardCapture（键盘捕捉）

一个用于 [ClassIsland](https://classisland.tech) 的**全局键盘按键捕捉**插件。

本插件通过跨平台全局键盘钩子（[SharpHook](https://github.com/TolikPylypchuk/SharpHook) / libuiohook）捕捉系统级键盘按键事件，并以依赖注入服务的形式暴露给其他 ClassIsland 插件，使它们可以订阅全局按键（KeyDown / KeyUp）、读取修饰键状态，从而实现快捷键触发、按键联动等场景（例如教室多媒体大屏上的物理按键 / 遥控触发）。

## 主要功能

- 全局系统级键盘按键捕捉：任意应用中的按键按下（KeyDown）与释放（KeyUp）事件。
- 标准化按键表示：键名（如 `A`、`F5`、`LeftCtrl`、`Space`）+ 跨平台键码。
- 修饰键状态：Ctrl / Alt / Shift / Meta（Win / Cmd / Super）按位组合。
- 作为 IoC 单例服务注册，随 ClassIsland 主机自动启动、停止全局钩子。
- 单个订阅者的异常被隔离，不影响钩子分发与其他订阅者。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `src/KeyboardCapture.Abstractions/` | 共享接口包（NuGet 包名 `Starsky16.KeyboardCapture.Abstractions`），供其他插件引用，不依赖任何钩子实现 |
| `KeyboardCapture.csproj` | 插件本体：基于 SharpHook 的全局钩子实现 |
| `demo/KeyboardCapture.DemoPlugin/` | 最小消费者示例插件，演示跨插件依赖与订阅 |
| `tools/` | 本地回归测试 / 发布 / 图标生成脚本 |

## 面向插件开发者

### 1. 声明依赖

在您的插件清单 `manifest.yml` 中加入：

```yaml
dependencies:
  - id: Starsky16.KeyboardCapture
```

### 2. 引用共享接口

NuGet 引用 `Starsky16.KeyboardCapture.Abstractions`（开发期也可以直接以项目引用指向本仓库的 `src/KeyboardCapture.Abstractions`）。

### 3. 注入并订阅

```csharp
public class YourService
{
    private readonly IKeyboardCaptureService _capture;

    public YourService(IKeyboardCaptureService capture) => _capture = capture;

    public void Start()
    {
        _capture.KeyDown += OnKeyDown;
        _capture.KeyUp += OnKeyUp;
    }

    private void OnKeyDown(object? sender, KeyboardKeyEventArgs e)
    {
        if (e.Key.Name == KeyboardKeys.F5 && e.Modifiers.HasFlag(KeyModifiers.Ctrl))
        {
            // Ctrl+F5 被按下
        }
    }
}
```

> 注意：
> - 按键事件在**后台线程**触发，如需更新 UI，请封送到 Avalonia 的 `Dispatcher.UIThread`。
> - 主插件以托管服务形式随 ClassIsland 自动启动钩子，订阅方无需手动调用 `Start()`（除非曾显式 `Stop()`）。
> - 若将本插件声明为**可选**依赖（`isRequired: false`），请改用 `IAppHost.TryGetService<IKeyboardCaptureService>()` 获取服务，并妥善处理服务缺失的情形。

## 与 SystemTools 等综合插件的关系

ClassIsland 生态中，[SystemTools](https://github.com/Programmer-MrWang/SystemTools) 等综合插件也提供键盘相关能力（“按下自定义热键时”触发器，基于 Win32 `RegisterHotKey`）。二者**定位不同、可以共存、互为补充**：

| 维度 | KeyboardCapture | SystemTools 等综合插件 |
| --- | --- | --- |
| 面向对象 | **插件开发者**：通过 `IKeyboardCaptureService` 在代码中订阅 | **最终用户**：在图形界面配置触发器 / 行动 |
| 按键范围 | 完整键流：任意键的 KeyDown / KeyUp + 原生键码 + 修饰键状态 | 仅已注册的离散热键组合（按下时广播一次） |
| 实现方式 | 持续全局低级键盘钩子（SharpHook / libuiohook），**只观察、不拦截** | `RegisterHotKey` 系统热键注册，会**独占**该组合，前台程序不再收到该按键 |
| 冲突与上限 | 不占用系统热键表，无数量上限、无注册失败问题 | 受系统热键表限制，被其他程序占用时注册失败 |
| 复用方式 | 发布 `Starsky16.KeyboardCapture.Abstractions`，其他插件引用后直接订阅 | 服务为插件内部实现，不对外暴露 |

结论：

- 若只需要「按某个快捷键执行 ClassIsland 自动化」，使用综合插件的触发器即可，无需本插件。
- 若是**插件开发者**，需要拿到完整、非侵入的全局按键流（自定义快捷键系统、按键序列与长按检测、遥控器 / 物理键盘映射、按键宏录制回放、外部输入统计等），请依赖本插件。
- 两者可以同时安装运行：本插件使用非独占钩子，不会阻止其他程序（包括 SystemTools）注册热键或接收按键。

## 开发与测试

前置条件：.NET 8 SDK，以及本机可运行的 ClassIsland Debug 构建（`tools/run-local-test.ps1` 默认查找 `d:\code\ClassIsland\...\net8.0-windows10.0.19041.0`）。

```powershell
# 构建解决方案
dotnet build KeyboardCapture.sln -c Debug

# 本地回归测试：启动本地 ClassIsland，加载主插件与示例插件，
# 模拟按键并验证跨插件事件链路（F13 / A / Ctrl+F14）
pwsh tools/run-local-test.ps1

# 打包发布插件（输出 .cipx 到 ./cipx）
pwsh tools/publish.ps1

# （可选）重新生成 icon.png
pwsh tools/generate-icon.ps1
```

Git 分支约定：`main` 为发布主线，`dev` 为开发分支；功能经过本地测试通过后合入主线。

## 许可

[GPL-3.0](LICENSE.txt) 开源，与 ClassIsland 生态保持一致。

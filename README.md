# KeyboardCapture（键盘捕捉）

一个用于 [ClassIsland](https://classisland.tech) 的**全局键盘按键捕捉**插件。

本插件通过跨平台全局键盘钩子（[SharpHook](https://github.com/TolikPylypchuk/SharpHook)）捕捉系统级按键事件，并将捕捉能力作为服务暴露给其他有需求的 ClassIsland 插件，使其可以订阅按键事件、注册组合热键等。

## 主要功能

- 全局系统级捕捉键盘按键（KeyDown / KeyUp）。
- 标准化按键标识（键码 + 显示名）与修饰键（Ctrl / Alt / Shift / Win）状态。
- 通过依赖注入向其他插件暴露 `IKeyboardCaptureService`，实现跨插件联动。

## 面向插件开发者

如果您的插件需要读取全局键盘按键：

1. 在您的插件清单 `manifest.yml` 中声明依赖本插件：

   ```yaml
   dependencies:
     - id: Starsky16.KeyboardCapture
   ```

2. 通过 `IAppHost.TryGetService<IKeyboardCaptureService>()`（或构造函数注入）获取服务，订阅 `KeyDown` / `KeyUp` 事件。

> 注意：按键事件在后台线程触发，若需更新 UI，请使用 Avalonia 的 `Dispatcher.UIThread` 封送到 UI 线程。

## 开发状态

- 开发中，功能代码位于 `dev` 分支；发布版本见 `main` 分支。
- 接口定义与实现细节将在后续版本中逐步补充。

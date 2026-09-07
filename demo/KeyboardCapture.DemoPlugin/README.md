# KeyboardCapture.DemoPlugin（键盘捕捉示例插件）

演示如何依赖 [KeyboardCapture](../..) 主插件并订阅全局键盘事件。

- 在 `manifest.yml` 中声明 `dependencies`，指向主插件 `Starsky16.KeyboardCapture`。
- 通过构造函数注入 `IKeyboardCaptureService`，订阅 `KeyDown` / `KeyUp`。

当环境变量 `KEYBOARD_CAPTURE_DEMO=1` 时，本插件会把收到的按键记录到
插件配置目录下的 `keylog.txt`，供本地联动测试与自动化验证使用。

```powershell
# 在仓库根目录运行本地回归测试（会同时部署并联动验证本插件）
pwsh tools/run-local-test.ps1
```

MIT License

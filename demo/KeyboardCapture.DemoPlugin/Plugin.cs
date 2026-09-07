using System;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using KeyboardCapture.Abstractions;
using KeyboardCapture.DemoPlugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KeyboardCapture.DemoPlugin;

/// <summary>
/// 键盘捕捉示例插件入口。
/// </summary>
/// <remarks>
/// 本插件演示如何依赖 <c>Starsky16.KeyboardCapture</c> 插件并通过依赖注入
/// 获取 <see cref="IKeyboardCaptureService"/> 订阅全局键盘事件。
/// 当环境变量 <c>KEYBOARD_CAPTURE_DEMO</c> 为 <c>1</c> 时，会将按键事件记录到
/// 插件配置目录下的 <c>keylog.txt</c>（供本地联动测试与自动化验证使用）。
/// </remarks>
[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        if (Environment.GetEnvironmentVariable("KEYBOARD_CAPTURE_DEMO") != "1")
        {
            return;
        }

        // 从 IoC 容器注入主插件注册的 IKeyboardCaptureService，订阅其事件。
        services.AddSingleton(sp =>
            new KeyLogService(
                PluginConfigFolder,
                sp.GetRequiredService<IKeyboardCaptureService>()));
        services.AddHostedService(sp => sp.GetRequiredService<KeyLogService>());
    }
}

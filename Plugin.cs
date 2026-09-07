using System;
using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using KeyboardCapture.Abstractions;
using KeyboardCapture.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KeyboardCapture;

/// <summary>
/// 键盘捕捉插件入口。
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册全局键盘捕捉服务：单例服务 + 托管服务（随主机自动启停全局钩子）。
        services.AddSingleton<KeyboardCaptureService>();
        services.AddSingleton<IKeyboardCaptureService>(sp =>
            sp.GetRequiredService<KeyboardCaptureService>());
        services.AddHostedService(sp => sp.GetRequiredService<KeyboardCaptureService>());

        // 集成测试标记：仅当显式开启环境变量时写入，
        // 供自动化验证在启动 ClassIsland 后确认插件已成功加载。
        if (Environment.GetEnvironmentVariable("KEYBOARD_CAPTURE_CI_MARKER") == "1")
        {
            try
            {
                Directory.CreateDirectory(PluginConfigFolder);
                File.WriteAllText(
                    Path.Combine(PluginConfigFolder, "loaded.marker"),
                    $"loaded at {DateTime.UtcNow:O}");
            }
            catch
            {
                // 标记写入失败不应影响插件正常初始化
            }
        }
    }
}
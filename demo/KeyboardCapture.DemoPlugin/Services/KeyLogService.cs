using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KeyboardCapture.Abstractions;
using Microsoft.Extensions.Hosting;

namespace KeyboardCapture.DemoPlugin.Services;

/// <summary>
/// 演示用按键日志服务：订阅 <see cref="IKeyboardCaptureService"/> 的键盘事件，
/// 将按键记录到插件配置目录下的 <c>keylog.txt</c>。
/// </summary>
public class KeyLogService : IHostedService
{
    private readonly IKeyboardCaptureService _capture;
    private readonly string _logFile;

    /// <summary>
    /// 初始化 <see cref="KeyLogService"/>。
    /// </summary>
    public KeyLogService(string configFolder, IKeyboardCaptureService capture)
    {
        _capture = capture;
        _logFile = Path.Combine(configFolder, "keylog.txt");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _capture.KeyDown += OnKey;
        _capture.KeyUp += OnKey;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _capture.KeyDown -= OnKey;
        _capture.KeyUp -= OnKey;
        return Task.CompletedTask;
    }

    private void OnKey(object? sender, KeyboardKeyEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            File.AppendAllText(
                _logFile,
                $"{DateTime.Now:O} {(e.IsKeyDown ? "DOWN" : "UP")} " +
                $"{e.Key.Name} [{e.Modifiers}]{Environment.NewLine}");
        }
        catch
        {
            // 日志写入失败不应影响事件订阅
        }
    }
}

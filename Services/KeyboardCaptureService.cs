using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using KeyboardCapture.Abstractions;
using Microsoft.Extensions.Hosting;
using SharpHook;
using SharpHook.Data;

namespace KeyboardCapture.Services;

/// <summary>
/// 基于 SharpHook（libuiohook）的全局键盘捕捉服务实现。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>同时实现 <see cref="IHostedService"/>，由插件注册为托管服务，
/// 在 ClassIsland 主机启动时自动开始全局捕捉，停止时自动释放。</description></item>
/// <item><description>按键事件在 SharpHook 的任务池线程触发（不阻塞系统钩子），
/// 单个订阅者抛出的异常会被隔离，不影响其他订阅者。</description></item>
/// </list>
/// </remarks>
public class KeyboardCaptureService : IKeyboardCaptureService, IHostedService
{
    private readonly TaskPoolGlobalHook _hook;
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _isCapturing;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="KeyboardCaptureService"/>，并订阅 SharpHook 的键盘事件。
    /// </summary>
    public KeyboardCaptureService()
    {
        _hook = new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    /// <inheritdoc />
    public bool IsCapturing
    {
        get
        {
            lock (_gate)
            {
                return _isCapturing;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<KeyboardKeyEventArgs>? KeyDown;

    /// <inheritdoc />
    public event EventHandler<KeyboardKeyEventArgs>? KeyUp;

    /// <inheritdoc />
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isCapturing)
            {
                return;
            }

            _isCapturing = true;
        }

        if (!_hook.IsRunning)
        {
            // RunAsync 在后台线程运行钩子，返回的 Task 在钩子停止时完成；
            // 启动失败等异常仅记录，不应影响宿主。
            _ = _hook.RunAsync().ContinueWith(
                t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_gate)
        {
            if (!_isCapturing)
            {
                return;
            }

            _isCapturing = false;
        }

        if (_hook.IsRunning)
        {
            _hook.Stop();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isCapturing = false;
        }

        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;
        if (_hook.IsRunning)
        {
            _hook.Stop();
        }

        _hook.Dispose();
    }

    /// <summary>
    /// 主机启动时调用：自动开始全局捕捉。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Start();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 主机停止时调用：停止全局捕捉。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Stop();
        return Task.CompletedTask;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcUndefined)
        {
            return;
        }

        Raise(KeyDown, this, CreateEventArgs(e, isKeyDown: true));
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcUndefined)
        {
            return;
        }

        Raise(KeyUp, this, CreateEventArgs(e, isKeyDown: false));
    }

    private KeyboardKeyEventArgs CreateEventArgs(KeyboardHookEventArgs e, bool isKeyDown)
    {
        var keyCode = e.Data.KeyCode;
        var mask = e.RawEvent.Mask;
        return new KeyboardKeyEventArgs(
            new KeyboardKey((int)keyCode, GetKeyName(keyCode)),
            ToModifiers(mask),
            isKeyDown,
            isAutoRepeat: false,
            _clock.ElapsedMilliseconds);
    }

    /// <summary>
    /// 逐个调用订阅者，并隔离单个订阅者的异常，避免影响其他订阅者与钩子分发。
    /// </summary>
    private static void Raise(
        EventHandler<KeyboardKeyEventArgs>? handler,
        object? sender,
        KeyboardKeyEventArgs e)
    {
        if (handler is null)
        {
            return;
        }

        foreach (var d in handler.GetInvocationList())
        {
            if (d is not EventHandler<KeyboardKeyEventArgs> h)
            {
                continue;
            }

            try
            {
                h(sender, e);
            }
            catch
            {
                // 忽略单个订阅者异常
            }
        }
    }

    private static KeyModifiers ToModifiers(EventMask mask)
    {
        var result = KeyModifiers.None;
        if (mask.HasCtrl())
        {
            result |= KeyModifiers.Ctrl;
        }

        if (mask.HasAlt())
        {
            result |= KeyModifiers.Alt;
        }

        if (mask.HasShift())
        {
            result |= KeyModifiers.Shift;
        }

        if (mask.HasMeta())
        {
            result |= KeyModifiers.Meta;
        }

        return result;
    }

    private static string GetKeyName(KeyCode code)
    {
        // 修饰键名与 Abstractions 中的常量保持一致（如 LeftCtrl 而非 LeftControl）。
        switch (code)
        {
            case KeyCode.VcLeftControl:
                return KeyboardKeys.LeftCtrl;
            case KeyCode.VcRightControl:
                return KeyboardKeys.RightCtrl;
            case KeyCode.VcLeftAlt:
                return KeyboardKeys.LeftAlt;
            case KeyCode.VcRightAlt:
                return KeyboardKeys.RightAlt;
            case KeyCode.VcLeftShift:
                return KeyboardKeys.LeftShift;
            case KeyCode.VcRightShift:
                return KeyboardKeys.RightShift;
            case KeyCode.VcLeftMeta:
                return KeyboardKeys.LeftMeta;
            case KeyCode.VcRightMeta:
                return KeyboardKeys.RightMeta;
        }

        // 其余键名直接使用枚举名去掉 "Vc" 前缀的形式，如 VcA -> "A"、VcF5 -> "F5"。
        var name = code.ToString();
        return name.StartsWith("Vc", StringComparison.Ordinal) ? name[2..] : name;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using KeyboardCapture.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    /// <summary>
    /// 等待全局钩子进入运行状态的最长时间；超时视为启动失败并回滚捕捉状态。
    /// </summary>
    private static readonly TimeSpan HookStartTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 等待钩子运行任务结束的最长时间，超时会重试一次停止。
    /// </summary>
    private static readonly TimeSpan HookStopTimeout = TimeSpan.FromMilliseconds(500);

    private const int PollIntervalMilliseconds = 10;

    private readonly IGlobalHook _hook;
    private readonly ILogger<KeyboardCaptureService>? _logger;
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<KeyCode> _pressedKeys = new();
    private Task? _runTask;
    private bool _isCapturing;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="KeyboardCaptureService"/>，使用默认的 SharpHook 全局钩子。
    /// </summary>
    /// <param name="logger">可选的日志记录器，用于记录启动失败等异常情况。</param>
    public KeyboardCaptureService(ILogger<KeyboardCaptureService>? logger = null)
        : this(new TaskPoolGlobalHook(), logger)
    {
    }

    /// <summary>
    /// 初始化 <see cref="KeyboardCaptureService"/>，并订阅传入全局钩子的键盘事件。
    /// </summary>
    /// <param name="hook">全局钩子实现。生产环境使用 SharpHook 的 <see cref="TaskPoolGlobalHook"/>，
    /// 测试可注入替代实现。</param>
    /// <param name="logger">可选的日志记录器。</param>
    internal KeyboardCaptureService(IGlobalHook hook, ILogger<KeyboardCaptureService>? logger = null)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _logger = logger;
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
    /// <remarks>
    /// 方法返回时全局钩子已处于运行状态；若钩子启动失败或超时，
    /// 捕捉状态会回滚为未捕捉，调用方可以稍后重试。
    /// </remarks>
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isCapturing)
            {
                return;
            }

            // 先占位，避免并发调用重复启动全局钩子。
            _isCapturing = true;
        }

        Task? runTask = null;
        try
        {
            // RunAsync 在后台线程运行钩子，返回的 Task 在钩子停止时完成。
            runTask = _hook.RunAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "全局键盘钩子启动失败。");
        }

        // RunAsync 返回后钩子并非立即就绪，需等待其真正进入运行状态，
        // 否则紧随其后的 Stop 会因为钩子尚未运行而被漏掉。
        var started = runTask is not null && WaitForHookRunning(runTask, HookStartTimeout);

        bool keepRunning;
        lock (_gate)
        {
            _runTask = runTask;
            keepRunning = started && _isCapturing;
            if (!keepRunning)
            {
                _isCapturing = false;
                _pressedKeys.Clear();
            }
        }

        if (keepRunning)
        {
            return;
        }

        LogStartFailure(runTask, started);

        if (runTask is not null)
        {
            // 启动失败、超时，或等待期间已被请求停止：确保钩子不会继续运行。
            StopHook(runTask);
            lock (_gate)
            {
                _runTask = null;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 无论钩子是否仍报告运行中都会调用其停止逻辑，避免启动竞态下漏停。
    /// </remarks>
    public void Stop()
    {
        Task? runTask;
        lock (_gate)
        {
            if (!_isCapturing)
            {
                return;
            }

            _isCapturing = false;
            _pressedKeys.Clear();
            runTask = _runTask;
            _runTask = null;
        }

        if (runTask is not null)
        {
            StopHook(runTask);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Task? runTask;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isCapturing = false;
            _pressedKeys.Clear();
            runTask = _runTask;
            _runTask = null;
        }

        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;

        if (runTask is not null)
        {
            StopHook(runTask);
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

    /// <summary>
    /// 轮询等待钩子进入运行状态；若运行任务提前结束（通常是启动失败）则立即返回。
    /// </summary>
    private bool WaitForHookRunning(Task runTask, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (_hook.IsRunning)
            {
                return true;
            }

            if (runTask.IsCompleted)
            {
                return _hook.IsRunning;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }

        return _hook.IsRunning;
    }

    /// <summary>
    /// 停止钩子并等待其运行任务结束，超时后再尝试一次停止。
    /// </summary>
    private void StopHook(Task runTask)
    {
        _hook.Stop();

        if (WaitForTaskCompletion(runTask, HookStopTimeout))
        {
            return;
        }

        _logger?.LogWarning(
            "全局键盘钩子未在 {Timeout} 毫秒内停止，正在重试。",
            HookStopTimeout.TotalMilliseconds);

        _hook.Stop();

        if (!WaitForTaskCompletion(runTask, HookStopTimeout))
        {
            _logger?.LogWarning("全局键盘钩子仍未停止，可能存在残留的系统钩子。");
        }
    }

    private static bool WaitForTaskCompletion(Task task, TimeSpan timeout)
    {
        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            // 任务以异常结束同样视为已结束。
            return true;
        }
    }

    private void LogStartFailure(Task? runTask, bool started)
    {
        if (started)
        {
            // 启动成功但等待期间已被请求停止，不属于异常。
            return;
        }

        // 显式读取异常，确保启动失败不会被静默吞掉。
        var exception = runTask?.Exception;
        if (exception is not null)
        {
            _logger?.LogError(exception, "全局键盘钩子启动失败，已回滚捕捉状态。");
            return;
        }

        _logger?.LogError(
            "全局键盘钩子在 {Timeout} 毫秒内未进入运行状态，已回滚捕捉状态。",
            HookStartTimeout.TotalMilliseconds);
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var keyCode = e.Data.KeyCode;
        if (keyCode == KeyCode.VcUndefined)
        {
            return;
        }

        // 键码已在按下集合中说明这是系统的自动重复，而不是新的按下。
        bool isAutoRepeat;
        lock (_gate)
        {
            isAutoRepeat = !_pressedKeys.Add(keyCode);
        }

        Raise(KeyDown, this, CreateEventArgs(e, isKeyDown: true, isAutoRepeat));
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        var keyCode = e.Data.KeyCode;
        if (keyCode == KeyCode.VcUndefined)
        {
            return;
        }

        lock (_gate)
        {
            _pressedKeys.Remove(keyCode);
        }

        Raise(KeyUp, this, CreateEventArgs(e, isKeyDown: false, isAutoRepeat: false));
    }

    private KeyboardKeyEventArgs CreateEventArgs(
        KeyboardHookEventArgs e,
        bool isKeyDown,
        bool isAutoRepeat)
    {
        var keyCode = e.Data.KeyCode;
        var mask = e.RawEvent.Mask;
        return new KeyboardKeyEventArgs(
            new KeyboardKey((int)keyCode, GetKeyName(keyCode)),
            ToModifiers(mask),
            isKeyDown,
            isAutoRepeat,
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

    internal static KeyModifiers ToModifiers(EventMask mask)
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

    internal static string GetKeyName(KeyCode code)
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
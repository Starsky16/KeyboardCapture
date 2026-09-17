using System;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace KeyboardCapture.Tests;

/// <summary>
/// 可编程控制的 <see cref="IGlobalHook"/> 测试替身：不安装真实系统钩子，
/// 通过 <see cref="StartDelay"/>、<see cref="FailOnStart"/> 精确复现启动慢与启动失败，
/// 并支持手动抛出键盘事件驱动服务的事件分发路径。
/// </summary>
internal sealed class FakeGlobalHook : IGlobalHook
{
    private EventHandler<KeyboardHookEventArgs>? _keyPressed;
    private EventHandler<KeyboardHookEventArgs>? _keyReleased;
    private volatile ManualResetEventSlim _stopSignal = new(false);
    private volatile bool _isRunning;

    /// <summary>
    /// 为 <c>true</c> 时 <see cref="RunAsync"/> 返回已失败的 <see cref="Task"/>，
    /// 且 <see cref="IsRunning"/> 保持为 <c>false</c>，用于模拟钩子启动失败。
    /// </summary>
    public bool FailOnStart { get; set; }

    /// <summary>
    /// 模拟钩子启动耗时：运行线程在该延迟之后才把 <see cref="IsRunning"/> 置为 <c>true</c>。
    /// </summary>
    public TimeSpan StartDelay { get; set; }

    /// <summary>
    /// <see cref="RunAsync"/> 被调用的次数，用于验证重复 Start 不会重复启动钩子。
    /// </summary>
    public int RunCallCount { get; private set; }

    /// <summary>
    /// <see cref="Stop"/> 被调用的次数。
    /// </summary>
    public int StopCallCount { get; private set; }

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    // 本测试替身不涉及鼠标与钩子启停事件，显式空实现以避免未使用事件告警。
    public event EventHandler<HookEventArgs>? HookEnabled
    {
        add { }
        remove { }
    }

    public event EventHandler<HookEventArgs>? HookDisabled
    {
        add { }
        remove { }
    }

    public event EventHandler<KeyboardHookEventArgs>? KeyTyped
    {
        add { }
        remove { }
    }

    public event EventHandler<KeyboardHookEventArgs>? KeyPressed
    {
        add => _keyPressed += value;
        remove => _keyPressed -= value;
    }

    public event EventHandler<KeyboardHookEventArgs>? KeyReleased
    {
        add => _keyReleased += value;
        remove => _keyReleased -= value;
    }

    public event EventHandler<MouseHookEventArgs>? MouseClicked
    {
        add { }
        remove { }
    }

    public event EventHandler<MouseHookEventArgs>? MousePressed
    {
        add { }
        remove { }
    }

    public event EventHandler<MouseHookEventArgs>? MouseReleased
    {
        add { }
        remove { }
    }

    public event EventHandler<MouseHookEventArgs>? MouseMoved
    {
        add { }
        remove { }
    }

    public event EventHandler<MouseHookEventArgs>? MouseDragged
    {
        add { }
        remove { }
    }

    public event EventHandler<MouseWheelHookEventArgs>? MouseWheel
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public void Run() => RunAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task RunAsync()
    {
        RunCallCount++;

        if (FailOnStart)
        {
            _isRunning = false;
            return Task.FromException(new InvalidOperationException("模拟：全局钩子启动失败"));
        }

        // 每次运行使用独立的停止信号，保证 Stop 之后仍可重新 Start。
        var stopSignal = new ManualResetEventSlim(false);
        _stopSignal = stopSignal;

        return Task.Run(() =>
        {
            if (StartDelay > TimeSpan.Zero)
            {
                Thread.Sleep(StartDelay);
            }

            _isRunning = true;
            stopSignal.Wait();
            _isRunning = false;
        });
    }

    /// <inheritdoc />
    public void Stop()
    {
        StopCallCount++;
        _stopSignal.Set();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
        Stop();
    }

    /// <summary>
    /// 模拟一次按键按下事件。
    /// </summary>
    public void RaiseKeyPressed(KeyCode code, EventMask mask = EventMask.None)
        => Raise(_keyPressed, EventType.KeyPressed, code, mask);

    /// <summary>
    /// 模拟一次按键抬起事件。
    /// </summary>
    public void RaiseKeyReleased(KeyCode code, EventMask mask = EventMask.None)
        => Raise(_keyReleased, EventType.KeyReleased, code, mask);

    private void Raise(
        EventHandler<KeyboardHookEventArgs>? handler,
        EventType type,
        KeyCode code,
        EventMask mask)
    {
        if (handler is null)
        {
            return;
        }

        var raw = new UioHookEvent
        {
            Type = type,
            Mask = mask,
            Keyboard =
            {
                KeyCode = code,
                RawCode = (ushort)code,
            },
        };

        handler(this, new KeyboardHookEventArgs(raw));
    }
}
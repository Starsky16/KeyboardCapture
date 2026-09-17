using System;
using System.Diagnostics;
using System.Threading;
using KeyboardCapture.Abstractions;
using KeyboardCapture.Services;
using SharpHook.Data;
using Xunit;

namespace KeyboardCapture.Tests;

/// <summary>
/// <see cref="KeyboardCaptureService"/> 生命周期与事件分发的行为测试。
/// 通过 <see cref="FakeGlobalHook"/> 注入可控钩子，不依赖真实系统钩子。
/// </summary>
public class KeyboardCaptureServiceTests
{
    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }

    [Fact]
    public void Start_钩子启动失败_IsCapturing应为false且允许重试()
    {
        var hook = new FakeGlobalHook { FailOnStart = true };
        using var service = new KeyboardCaptureService(hook);

        service.Start();

        Assert.False(service.IsCapturing);
        Assert.False(hook.IsRunning);

        // 修复掉失败状态后应能再次启动，而不是永久卡在"捕捉中"。
        hook.FailOnStart = false;
        service.Start();

        Assert.True(WaitUntil(() => service.IsCapturing && hook.IsRunning));
        Assert.Equal(2, hook.RunCallCount);

        service.Stop();
    }

    [Fact]
    public void Start后立即Stop_钩子最终应停止()
    {
        var hook = new FakeGlobalHook { StartDelay = TimeSpan.FromMilliseconds(150) };
        using var service = new KeyboardCaptureService(hook);

        try
        {
            service.Start();
            service.Stop();

            Assert.True(WaitUntil(() => !hook.IsRunning), "Stop 之后钩子不应继续处于运行状态");
            Assert.True(hook.StopCallCount >= 1, "Stop 必须真正调用钩子的 Stop，未就绪时也不例外");
            Assert.False(service.IsCapturing);
        }
        finally
        {
            // 兜底清理：断言失败时也要保证替身钩子被停下。
            hook.Stop();
        }
    }

    [Fact]
    public void 重复Start_只启动一次钩子()
    {
        var hook = new FakeGlobalHook();
        using var service = new KeyboardCaptureService(hook);

        service.Start();
        service.Start();

        Assert.True(WaitUntil(() => service.IsCapturing && hook.IsRunning));
        Assert.Equal(1, hook.RunCallCount);

        service.Stop();
    }

    [Fact]
    public void 未启动时Stop_不应调用钩子的Stop()
    {
        var hook = new FakeGlobalHook();
        using var service = new KeyboardCaptureService(hook);

        service.Stop();

        Assert.Equal(0, hook.StopCallCount);
        Assert.False(service.IsCapturing);
    }

    [Fact]
    public void Stop后可再次Start()
    {
        var hook = new FakeGlobalHook();
        using var service = new KeyboardCaptureService(hook);

        service.Start();
        Assert.True(WaitUntil(() => service.IsCapturing && hook.IsRunning));

        service.Stop();
        Assert.True(WaitUntil(() => !hook.IsRunning));

        service.Start();
        Assert.True(WaitUntil(() => service.IsCapturing && hook.IsRunning));
        Assert.Equal(2, hook.RunCallCount);

        service.Stop();
    }

    [Fact]
    public void 订阅者抛异常_不影响其他订阅者与钩子()
    {
        var hook = new FakeGlobalHook();
        using var service = new KeyboardCaptureService(hook);

        var received = 0;
        service.KeyDown += (_, _) => throw new InvalidOperationException("订阅者故障");
        service.KeyDown += (_, _) => Interlocked.Increment(ref received);

        service.Start();
        Assert.True(WaitUntil(() => hook.IsRunning));

        hook.RaiseKeyPressed(KeyCode.VcA);

        Assert.Equal(1, received);

        service.Stop();
    }

    [Fact]
    public void 未定义键码_不应触发事件()
    {
        var hook = new FakeGlobalHook();
        using var service = new KeyboardCaptureService(hook);

        var received = 0;
        service.KeyDown += (_, _) => Interlocked.Increment(ref received);

        service.Start();
        Assert.True(WaitUntil(() => hook.IsRunning));

        hook.RaiseKeyPressed(KeyCode.VcUndefined);

        Assert.Equal(0, received);

        service.Stop();
    }

    [Fact]
    public void 按住不放_第二次KeyDown的IsAutoRepeat应为true()
    {
        var hook = new FakeGlobalHook();
        using var service = new KeyboardCaptureService(hook);

        var repeats = new System.Collections.Generic.List<bool>();
        service.KeyDown += (_, e) => repeats.Add(e.IsAutoRepeat);

        service.Start();
        Assert.True(WaitUntil(() => hook.IsRunning));

        hook.RaiseKeyPressed(KeyCode.VcA);
        hook.RaiseKeyPressed(KeyCode.VcA);
        hook.RaiseKeyReleased(KeyCode.VcA);
        hook.RaiseKeyPressed(KeyCode.VcA);

        Assert.Equal(new[] { false, true, false }, repeats);

        service.Stop();
    }

    [Theory]
    [InlineData(KeyCode.VcLeftControl, KeyboardKeys.LeftCtrl)]
    [InlineData(KeyCode.VcRightControl, KeyboardKeys.RightCtrl)]
    [InlineData(KeyCode.VcLeftAlt, KeyboardKeys.LeftAlt)]
    [InlineData(KeyCode.VcRightShift, KeyboardKeys.RightShift)]
    [InlineData(KeyCode.VcLeftMeta, KeyboardKeys.LeftMeta)]
    [InlineData(KeyCode.VcA, "A")]
    [InlineData(KeyCode.VcF13, "F13")]
    [InlineData(KeyCode.VcSpace, "Space")]
    [InlineData(KeyCode.VcUp, "Up")]
    public void GetKeyName_返回Abstractions约定名(KeyCode code, string expected)
    {
        Assert.Equal(expected, KeyboardCaptureService.GetKeyName(code));
    }

    [Theory]
    [InlineData(EventMask.None, KeyModifiers.None)]
    [InlineData(EventMask.LeftCtrl, KeyModifiers.Ctrl)]
    [InlineData(EventMask.Ctrl, KeyModifiers.Ctrl)]
    [InlineData(EventMask.LeftAlt, KeyModifiers.Alt)]
    [InlineData(EventMask.Shift | EventMask.Ctrl, KeyModifiers.Shift | KeyModifiers.Ctrl)]
    [InlineData(EventMask.LeftMeta, KeyModifiers.Meta)]
    public void ToModifiers_由掩码映射修饰键(EventMask mask, KeyModifiers expected)
    {
        Assert.Equal(expected, KeyboardCaptureService.ToModifiers(mask));
    }
}
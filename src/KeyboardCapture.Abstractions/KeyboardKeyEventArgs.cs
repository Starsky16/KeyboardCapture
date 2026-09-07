namespace KeyboardCapture.Abstractions;

/// <summary>
/// 键盘事件参数，供 <see cref="IKeyboardCaptureService.KeyDown"/> 与
/// <see cref="IKeyboardCaptureService.KeyUp"/> 事件使用。
/// </summary>
public sealed class KeyboardKeyEventArgs : EventArgs
{
    /// <summary>
    /// 初始化一个新的 <see cref="KeyboardKeyEventArgs"/>。
    /// </summary>
    /// <param name="key">被按下/释放的按键。</param>
    /// <param name="modifiers">事件发生时修饰键的状态。</param>
    /// <param name="isKeyDown">事件是否为按键按下（<see langword="true"/>）或按键释放（<see langword="false"/>）。</param>
    /// <param name="isAutoRepeat">是否为按住键产生的系统自动重复事件。仅按下事件可能为 <see langword="true"/>。</param>
    /// <param name="timestampMs">事件发生的时间戳（毫秒，单调递增，无时钟跳变语义）。</param>
    public KeyboardKeyEventArgs(
        KeyboardKey key,
        KeyModifiers modifiers,
        bool isKeyDown,
        bool isAutoRepeat,
        long timestampMs)
    {
        Key = key;
        Modifiers = modifiers;
        IsKeyDown = isKeyDown;
        IsAutoRepeat = isAutoRepeat;
        TimestampMs = timestampMs;
    }

    /// <summary>
    /// 被按下/释放的按键。
    /// </summary>
    public KeyboardKey Key { get; }

    /// <summary>
    /// 事件发生时修饰键（Ctrl / Alt / Shift / Meta）的状态，按位组合。
    /// </summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>
    /// 事件是否为按键按下（<see langword="true"/>）或按键释放（<see langword="false"/>）。
    /// </summary>
    public bool IsKeyDown { get; }

    /// <summary>
    /// 是否为按住键产生的系统自动重复事件。仅按下事件可能为 <see langword="true"/>。
    /// </summary>
    public bool IsAutoRepeat { get; }

    /// <summary>
    /// 事件发生的时间戳（毫秒，单调递增，无时钟跳变语义）。
    /// </summary>
    public long TimestampMs { get; }
}

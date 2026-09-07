namespace KeyboardCapture.Abstractions;

/// <summary>
/// 修饰键（Ctrl / Alt / Shift / Meta）的当前状态，按位组合。
/// </summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>无修饰键。</summary>
    None = 0,

    /// <summary>Ctrl 键（左或右）。</summary>
    Ctrl = 1 << 0,

    /// <summary>Alt 键（左或右）。</summary>
    Alt = 1 << 1,

    /// <summary>Shift 键（左或右）。</summary>
    Shift = 1 << 2,

    /// <summary>
    /// 系统/徽标键。Windows 上为 Win 键，macOS 上为 Command 键，Linux 上为 Super 键（左或右）。
    /// </summary>
    Meta = 1 << 3,
}

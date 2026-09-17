namespace KeyboardCapture.Abstractions;

/// <summary>
/// 一个键盘按键。
/// </summary>
/// <remarks>
/// <see cref="Code"/> 的取值语义对齐 UIOHook 键码（<c>uiohook</c> 使用的跨平台键码），
/// 与具体操作系统无关；<see cref="Name"/> 为该键的人类可读名称。
/// </remarks>
public sealed class KeyboardKey
{
    /// <summary>
    /// 初始化一个新的 <see cref="KeyboardKey"/>。
    /// </summary>
    /// <param name="code">UIOHook 语义的键码。</param>
    /// <param name="name">人类可读键名，如 <c>"A"</c>、<c>"F5"</c>、<c>"LeftCtrl"</c>。</param>
    public KeyboardKey(int code, string name)
    {
        Code = code;
        Name = name;
    }

    /// <summary>
    /// 键码。取值语义对齐 UIOHook 键码（跨平台统一、与操作系统无关）。
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// 人类可读键名，如 <c>"A"</c>、<c>"F5"</c>、<c>"LeftCtrl"</c>、<c>"Space"</c>。
    /// 如需比较按键，推荐使用 <see cref="KeyboardKeys"/> 中定义的常量。
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// 常用键名的字符串常量，便于订阅方与 <see cref="KeyboardKey.Name"/> 比较，
/// 例如：<c>e.Key.Name == KeyboardKeys.F5</c>。
/// </summary>
public static class KeyboardKeys
{
    public const string Escape = "Escape";
    public const string Enter = "Enter";
    public const string Space = "Space";
    public const string Tab = "Tab";
    public const string Backspace = "Backspace";
    public const string Delete = "Delete";
    public const string Insert = "Insert";
    public const string CapsLock = "CapsLock";
    public const string PageUp = "PageUp";
    public const string PageDown = "PageDown";
    public const string Home = "Home";
    public const string End = "End";
    public const string ArrowUp = "Up";
    public const string ArrowDown = "Down";
    public const string ArrowLeft = "Left";
    public const string ArrowRight = "Right";

    public const string LeftCtrl = "LeftCtrl";
    public const string RightCtrl = "RightCtrl";
    public const string LeftAlt = "LeftAlt";
    public const string RightAlt = "RightAlt";
    public const string LeftShift = "LeftShift";
    public const string RightShift = "RightShift";
    public const string LeftMeta = "LeftMeta";
    public const string RightMeta = "RightMeta";

    public const string NumLock = "NumLock";
    public const string ScrollLock = "ScrollLock";
    public const string PrintScreen = "PrintScreen";
    public const string Pause = "Pause";

    public const string F1 = "F1";
    public const string F2 = "F2";
    public const string F3 = "F3";
    public const string F4 = "F4";
    public const string F5 = "F5";
    public const string F6 = "F6";
    public const string F7 = "F7";
    public const string F8 = "F8";
    public const string F9 = "F9";
    public const string F10 = "F10";
    public const string F11 = "F11";
    public const string F12 = "F12";
    public const string F13 = "F13";
    public const string F14 = "F14";
    public const string F15 = "F15";
    public const string F16 = "F16";
    public const string F17 = "F17";
    public const string F18 = "F18";
    public const string F19 = "F19";
    public const string F20 = "F20";
    public const string F21 = "F21";
    public const string F22 = "F22";
    public const string F23 = "F23";
    public const string F24 = "F24";

    // 字母键：键名即大写字母本身。
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string E = "E";
    public const string F = "F";
    public const string G = "G";
    public const string H = "H";
    public const string I = "I";
    public const string J = "J";
    public const string K = "K";
    public const string L = "L";
    public const string M = "M";
    public const string N = "N";
    public const string O = "O";
    public const string P = "P";
    public const string Q = "Q";
    public const string R = "R";
    public const string S = "S";
    public const string T = "T";
    public const string U = "U";
    public const string V = "V";
    public const string W = "W";
    public const string X = "X";
    public const string Y = "Y";
    public const string Z = "Z";

    // 主键盘数字键：键名为数字字符本身（常量名加 Digit 前缀以便书写）。
    public const string Digit0 = "0";
    public const string Digit1 = "1";
    public const string Digit2 = "2";
    public const string Digit3 = "3";
    public const string Digit4 = "4";
    public const string Digit5 = "5";
    public const string Digit6 = "6";
    public const string Digit7 = "7";
    public const string Digit8 = "8";
    public const string Digit9 = "9";
}

namespace KeyboardCapture.Abstractions;

/// <summary>
/// 全局键盘捕捉服务。由 KeyboardCapture 插件注册到 ClassIsland 的 IoC 容器中，
/// 供其他依赖本插件的 ClassIsland 插件订阅全局键盘按键事件。
/// </summary>
/// <remarks>
/// 实现说明：
/// <list type="bullet">
/// <item><description>事件在<strong>后台线程</strong>触发。若订阅方需要更新 UI，
/// 请将操作封送到 Avalonia 的 UI 线程（如 <c>Avalonia.Threading.Dispatcher.UIThread</c>）。</description></item>
/// <item><description>单个事件处理程序抛出的异常不会影响其他订阅者，也不会中断钩子分发。</description></item>
/// </list>
/// </remarks>
public interface IKeyboardCaptureService : IDisposable
{
    /// <summary>
    /// 当前是否正在全局捕捉按键。
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// 开始全局捕捉按键。重复调用为幂等操作：已在捕捉中时直接返回。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止全局捕捉按键。重复调用为幂等操作：未在捕捉中时直接返回。
    /// </summary>
    void Stop();

    /// <summary>
    /// 任意键被按下时触发。
    /// </summary>
    event EventHandler<KeyboardKeyEventArgs>? KeyDown;

    /// <summary>
    /// 任意键被释放时触发。
    /// </summary>
    event EventHandler<KeyboardKeyEventArgs>? KeyUp;
}

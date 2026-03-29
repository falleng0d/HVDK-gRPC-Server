namespace GRPCRemote.Drivers;

public interface IInputTransport : IDisposable
{
    Task SendKeyboardAsync(KeyboardReport report, CancellationToken cancellationToken);

    Task SendRelativeMouseAsync(RelativeMouseReport report, CancellationToken cancellationToken);

    Task SendAbsoluteMouseAsync(AbsoluteMouseReport report, CancellationToken cancellationToken);
}

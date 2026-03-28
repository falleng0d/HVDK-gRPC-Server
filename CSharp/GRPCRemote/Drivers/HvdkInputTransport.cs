using System.Runtime.InteropServices;
using Drivers;
using GRPCRemote.Configuration;
using HIDCtrl;

namespace GRPCRemote.Drivers;

public sealed class HvdkInputTransport : IInputTransport
{
    private readonly object _sync = new();
    private readonly ILogger<HvdkInputTransport> _logger;
    private HidController? _keyboardController;
    private HidController? _relativeMouseController;
    private HidController? _absoluteMouseController;
    private bool _disposed;

    public HvdkInputTransport(RemoteHostOptions options, ILogger<HvdkInputTransport> logger)
    {
        _logger = logger;
    }

    public Task SendKeyboardAsync(KeyboardReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var controller = GetOrCreateController(ref _keyboardController, DriversConst.TtcProductidKeyboard);
        var keyboardReport = new SetFeatureKeyboard
        {
            ReportID = 1,
            CommandCode = 2,
            Timeout = report.TimeoutMilliseconds / 5,
            Modifier = report.Modifier,
            Padding = 0,
            Key0 = GetKey(report.Keys, 0),
            Key1 = GetKey(report.Keys, 1),
            Key2 = GetKey(report.Keys, 2),
            Key3 = GetKey(report.Keys, 3),
            Key4 = GetKey(report.Keys, 4),
            Key5 = GetKey(report.Keys, 5),
        };

        Send(controller, keyboardReport);
        return Task.CompletedTask;
    }

    public Task SendRelativeMouseAsync(RelativeMouseReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var controller = GetOrCreateController(ref _relativeMouseController, DriversConst.TtcProductidMouserel);
        var mouseReport = new SetFeatureMouseRel
        {
            ReportID = 1,
            CommandCode = 2,
            Buttons = report.Buttons,
            X = report.X,
            Y = report.Y,
        };

        Send(controller, mouseReport);
        return Task.CompletedTask;
    }

    public Task SendAbsoluteMouseAsync(AbsoluteMouseReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var controller = GetOrCreateController(ref _absoluteMouseController, DriversConst.TtcProductidMouseabs);
        var mouseReport = new SetFeatureMouseAbs
        {
            ReportID = 1,
            CommandCode = 2,
            Buttons = report.Buttons,
            X = report.X,
            Y = report.Y,
        };

        Send(controller, mouseReport);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _keyboardController?.Disconnect();
            _relativeMouseController?.Disconnect();
            _absoluteMouseController?.Disconnect();
            _disposed = true;
        }
    }

    private HidController GetOrCreateController(ref HidController? controller, DriversConst productId)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (controller is { Connected: true })
            {
                return controller;
            }

            controller ??= new HidController
            {
                VendorId = (ushort)DriversConst.TtcVendorid,
                ProductId = (ushort)productId,
            };

            controller.Connect();
            if (!controller.Connected)
            {
                throw new InvalidOperationException($"Unable to connect to HVDK device {(ushort)productId:x4}.");
            }

            _logger.LogInformation("Connected to HVDK device {ProductId}", (ushort)productId);
            return controller;
        }
    }

    private static void Send<TReport>(HidController controller, TReport report)
        where TReport : struct
    {
        var size = Marshal.SizeOf<TReport>();
        var buffer = new byte[size];
        var memory = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(report, memory, false);
            Marshal.Copy(memory, buffer, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }

        if (!controller.SendData(buffer, (uint)size))
        {
            throw new InvalidOperationException("HVDK driver rejected the report.");
        }
    }

    private static byte GetKey(IReadOnlyList<byte> keys, int index)
    {
        return index < keys.Count ? keys[index] : (byte)0;
    }
}

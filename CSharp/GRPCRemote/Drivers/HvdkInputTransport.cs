using System.Runtime.InteropServices;
using Drivers;
using GRPCRemote.Configuration;
using HIDCtrl;

namespace GRPCRemote.Drivers;

public sealed class HvdkInputTransport : IInputTransport
{
    private readonly object _sync = new();
    private readonly ILogger<HvdkInputTransport> _logger;
    private readonly int _keyboardReportTimeoutMs;
    private HidController? _keyboardController;
    private HidController? _relativeMouseController;
    private HidController? _absoluteMouseController;
    private bool _disposed;

    public HvdkInputTransport(RemoteHostOptions options, ILogger<HvdkInputTransport> logger)
    {
        _logger = logger;
        _keyboardReportTimeoutMs = options.KeyboardReportTimeoutMs;
        
        logger.LogInformation("HVDK input transport created. Keyboard report timeout: {KeyboardReportTimeoutMs}ms", _keyboardReportTimeoutMs);
    }

    public Task SendKeyboardAsync(KeyboardReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("SendKeyboardAsync: Modifier={Modifier}, Keys=[{Keys}], Timeout={Timeout}",
            report.Modifier, string.Join(",", report.Keys), report.TimeoutMilliseconds);

        var controller = GetOrCreateController(ref _keyboardController, DriversConst.TtcProductidKeyboard);
        var timeoutMilliseconds = report.TimeoutMilliseconds == 0
            ? (uint)_keyboardReportTimeoutMs
            : report.TimeoutMilliseconds;
        var keyboardReport = new SetFeatureKeyboard
        {
            ReportID = 1,
            CommandCode = 2,
            Timeout = timeoutMilliseconds / 5,
            Modifier = report.Modifier,
            Padding = 0,
            Key0 = GetKey(report.Keys, 0),
            Key1 = GetKey(report.Keys, 1),
            Key2 = GetKey(report.Keys, 2),
            Key3 = GetKey(report.Keys, 3),
            Key4 = GetKey(report.Keys, 4),
            Key5 = GetKey(report.Keys, 5),
        };

        _logger.LogDebug("Sending keyboard report: ReportID={ReportID}, CommandCode={CommandCode}, Modifier={Modifier}, Keys=[{Keys}]",
            keyboardReport.ReportID, keyboardReport.CommandCode, keyboardReport.Modifier,
            string.Join(",", new[] { keyboardReport.Key0, keyboardReport.Key1, keyboardReport.Key2, keyboardReport.Key3, keyboardReport.Key4, keyboardReport.Key5 }));

        var success = Send(controller, keyboardReport);
        if (!success)
        {
            _logger.LogError("HVDK driver rejected keyboard report. Modifier={Modifier}, Keys=[{Keys}]",
                report.Modifier, string.Join(",", report.Keys));
        }
        else
        {
            _logger.LogDebug("Keyboard report sent successfully.");
        }

        return Task.CompletedTask;
    }

    public Task SendRelativeMouseAsync(RelativeMouseReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("SendRelativeMouseAsync: Buttons={Buttons}, X={X}, Y={Y}",
            report.Buttons, report.X, report.Y);

        var controller = GetOrCreateController(ref _relativeMouseController, DriversConst.TtcProductidMouserel);
        var mouseReport = new SetFeatureMouseRel
        {
            ReportID = 1,
            CommandCode = 2,
            Buttons = report.Buttons,
            X = report.X,
            Y = report.Y,
        };

        _logger.LogDebug("Sending relative mouse report: ReportID={ReportID}, CommandCode={CommandCode}, Buttons={Buttons}, X={X}, Y={Y}",
            mouseReport.ReportID, mouseReport.CommandCode, mouseReport.Buttons, mouseReport.X, mouseReport.Y);

        var success = Send(controller, mouseReport);
        if (!success)
        {
            _logger.LogError("HVDK driver rejected relative mouse report. Buttons={Buttons}, X={X}, Y={Y}",
                report.Buttons, report.X, report.Y);
        }
        else
        {
            _logger.LogDebug("Relative mouse report sent successfully.");
        }

        return Task.CompletedTask;
    }

    public Task SendAbsoluteMouseAsync(AbsoluteMouseReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("SendAbsoluteMouseAsync: Buttons={Buttons}, X={X}, Y={Y}",
            report.Buttons, report.X, report.Y);

        var controller = GetOrCreateController(ref _absoluteMouseController, DriversConst.TtcProductidMouseabs);
        var mouseReport = new SetFeatureMouseAbs
        {
            ReportID = 1,
            CommandCode = 2,
            Buttons = report.Buttons,
            X = report.X,
            Y = report.Y,
        };

        _logger.LogDebug("Sending absolute mouse report: ReportID={ReportID}, CommandCode={CommandCode}, Buttons={Buttons}, X={X}, Y={Y}",
            mouseReport.ReportID, mouseReport.CommandCode, mouseReport.Buttons, mouseReport.X, mouseReport.Y);

        var success = Send(controller, mouseReport);
        if (!success)
        {
            _logger.LogError("HVDK driver rejected absolute mouse report. Buttons={Buttons}, X={X}, Y={Y}",
                report.Buttons, report.X, report.Y);
        }
        else
        {
            _logger.LogDebug("Absolute mouse report sent successfully.");
        }

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

            _logger.LogInformation("Disposing HVDK transport, disconnecting controllers...");

            _keyboardController?.Disconnect();
            _relativeMouseController?.Disconnect();
            _absoluteMouseController?.Disconnect();

            _disposed = true;

            _logger.LogInformation("HVDK transport disposed.");
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

            var productIdValue = (ushort)productId;
            _logger.LogInformation("Attempting to connect to HVDK device {ProductId} (VendorId={VendorId})",
                productIdValue, (ushort)DriversConst.TtcVendorid);

            controller ??= new HidController
            {
                VendorId = (ushort)DriversConst.TtcVendorid,
                ProductId = productIdValue,
            };

            controller.OnLog += (_, args) => _logger.LogDebug("HID: {Message}", args.Msg);

            controller.Connect();
            if (!controller.Connected)
            {
                _logger.LogError("Failed to connect to HVDK device {ProductId}. Device not found or inaccessible.", productIdValue);
                throw new InvalidOperationException($"Unable to connect to HVDK device {productIdValue:x4}.");
            }

            _logger.LogInformation("Successfully connected to HVDK device {ProductId}", productIdValue);
            return controller;
        }
    }

    private static bool Send<TReport>(HidController controller, TReport report)
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

        return controller.SendData(buffer, (uint)size);
    }

    private static byte GetKey(IReadOnlyList<byte> keys, int index)
    {
        return index < keys.Count ? keys[index] : (byte)0;
    }
}

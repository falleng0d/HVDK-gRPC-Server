using Grpc.Core;
using GRPCRemote.Configuration;
using GRPCRemote.Input;

namespace GRPCRemote.Services;

public sealed class InputMethodsGrpcService(
    ConfigService configService,
    InputCoordinator inputCoordinator,
    ILogger<InputMethodsGrpcService> logger)
    : InputMethods.InputMethodsBase
{
    public override async Task<Response> PressKey(Key request, ServerCallContext context)
    {
        try
        {
            logger.LogDebug("Pressing key {Key} with type {Type}", request.Id, request.Type);
            
            await inputCoordinator.PressKeyAsync(
                RemoteKeyMap.FromId(request.Id),
                (RemoteActionType)request.Type,
                KeyRequestOptions.FromProto(request.Options),
                context.CancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }

    public override async Task<Response> PressHotkey(Hotkey request, ServerCallContext context)
    {
        try
        {
            await inputCoordinator.PressHotkeyAsync(
                request.Hotkey_,
                (RemoteActionType)request.Type,
                HotkeyRequestOptions.FromProto(request.Options),
                context.CancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }

    public override async Task<Response> PressMouseKey(MouseKey request, ServerCallContext context)
    {
        try
        {
            await inputCoordinator.PressMouseKeyAsync(
                RemoteKeyMap.MouseButtonFromId(request.Id),
                (RemoteActionType)request.Type,
                context.CancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }

    public override async Task<Response> MoveMouse(MouseMove request, ServerCallContext context)
    {
        try
        {
            await inputCoordinator.MoveMouseAsync(request.X, request.Y, true, context.CancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }

    public override Task<Response> Ping(Empty request, ServerCallContext context)
    {
        return Task.FromResult(Ok());
    }

    public override Task<Config> GetConfig(Empty request, ServerCallContext context)
    {
        var snapshot = configService.Snapshot;
        return Task.FromResult(ToProto(snapshot));
    }

    public override Task<Config> SetConfig(Config request, ServerCallContext context)
    {
        try
        {
            var snapshot = configService.Update(
                request.HasCursorSpeed ? request.CursorSpeed : null,
                request.HasCursorAcceleration ? request.CursorAcceleration : null);

            return Task.FromResult(ToProto(snapshot));
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }

    private static Response Ok()
    {
        return new Response { Message = "Ok" };
    }

    private static Config ToProto(RuntimeConfig runtimeConfig)
    {
        return new Config
        {
            CursorSpeed = runtimeConfig.CursorSpeed,
            CursorAcceleration = runtimeConfig.CursorAcceleration,
        };
    }

    private RpcException MapException(Exception exception)
    {
        logger.LogError(exception, "gRPC request failed");

        return exception switch
        {
            ArgumentOutOfRangeException or ArgumentException or FormatException or KeyNotFoundException =>
                new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)),
            NotSupportedException => new RpcException(new Status(StatusCode.Unimplemented, exception.Message)),
            InvalidOperationException => new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message)),
            _ => new RpcException(new Status(StatusCode.Unknown, exception.Message)),
        };
    }
}

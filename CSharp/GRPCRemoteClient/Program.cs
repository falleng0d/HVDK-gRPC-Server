using System.Globalization;
using Grpc.Core;
using Grpc.Net.Client;
using GRPCRemote;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

return await GrpcRemoteClientProgram.RunAsync(args);

internal static class GrpcRemoteClientProgram
{
    private const string DefaultUrl = "http://127.0.0.1:5039";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        if (args.Any(IsHelpToken))
        {
            PrintHelp();
            return 0;
        }

        ClientOptions options;

        try
        {
            options = ParseArguments(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            PrintHelp();
            return 1;
        }

        using var cancellationContext = new ConsoleCancellationContext();
        Console.CancelKeyPress += cancellationContext.Handler;

        try
        {
            using var channel = GrpcChannel.ForAddress(options.Url);
            var client = new InputMethods.InputMethodsClient(channel);

            foreach (var command in options.Commands)
            {
                Console.WriteLine($"> {command.DisplayText}");
                var result = await command.ExecuteAsync(client, cancellationContext.Token);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    Console.WriteLine(result);
                }
            }

            return 0;
        }
        catch (RpcException ex)
        {
            Console.Error.WriteLine($"gRPC error: {ex.StatusCode} - {ex.Status.Detail}");
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancellationContext.Handler;
        }
    }

    private static ClientOptions ParseArguments(IReadOnlyList<string> args)
    {
        var url = DefaultUrl;
        var commands = new List<IClientCommand>();
        PendingMouseMove? pendingMouseMove = null;

        void FlushPendingMouseMove()
        {
            if (pendingMouseMove is null)
            {
                return;
            }

            commands.Add(new MouseMoveCommand(
                pendingMouseMove.X,
                pendingMouseMove.Y,
                pendingMouseMove.Relative,
                pendingMouseMove.Speed,
                pendingMouseMove.Acceleration));
            pendingMouseMove = null;
        }

        void AppendConfigCommand(float? speed, float? acceleration)
        {
            if (commands.LastOrDefault() is ConfigCommand existing)
            {
                commands[^1] = new ConfigCommand(speed ?? existing.Speed, acceleration ?? existing.Acceleration);
                return;
            }

            commands.Add(new ConfigCommand(speed, acceleration));
        }

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];

            switch (token)
            {
                case "--url":
                    url = ReadRequiredValue(args, ref index, token);
                    break;

                case "--ping":
                    FlushPendingMouseMove();
                    commands.Add(new PingCommand());
                    break;

                case "--type":
                case "--hotkey":
                    FlushPendingMouseMove();
                    commands.Add(new TypeCommand(ReadRequiredValue(args, ref index, token)));
                    break;

                case "--wait":
                    FlushPendingMouseMove();
                    commands.Add(new WaitCommand(ParseNonNegativeInt(ReadRequiredValue(args, ref index, token), token)));
                    break;

                case "--mouse-move":
                    FlushPendingMouseMove();
                    pendingMouseMove = PendingMouseMove.Create(ReadRequiredValue(args, ref index, token), relative: true);
                    break;

                case "--mouse-move-abs":
                    FlushPendingMouseMove();
                    pendingMouseMove = PendingMouseMove.Create(ReadRequiredValue(args, ref index, token), relative: false);
                    break;

                case "--speed":
                {
                    var speed = ParseFloat(ReadRequiredValue(args, ref index, token), token);
                    if (pendingMouseMove is not null && pendingMouseMove.Speed is null)
                    {
                        pendingMouseMove = pendingMouseMove with { Speed = speed };
                    }
                    else
                    {
                        FlushPendingMouseMove();
                        AppendConfigCommand(speed, acceleration: null);
                    }

                    break;
                }

                case "--acceleration":
                {
                    var acceleration = ParseFloat(ReadRequiredValue(args, ref index, token), token);
                    if (pendingMouseMove is not null && pendingMouseMove.Acceleration is null)
                    {
                        pendingMouseMove = pendingMouseMove with { Acceleration = acceleration };
                    }
                    else
                    {
                        FlushPendingMouseMove();
                        AppendConfigCommand(speed: null, acceleration);
                    }

                    break;
                }

                case "--get-config":
                    FlushPendingMouseMove();
                    commands.Add(new GetConfigCommand());
                    break;

                case "--mouse-click":
                    FlushPendingMouseMove();
                    commands.Add(new MouseButtonCommand(ParseMouseButton(ReadRequiredValue(args, ref index, token)), MouseKey.Types.KeyActionType.Press));
                    break;

                case "--mouse-down":
                    FlushPendingMouseMove();
                    commands.Add(new MouseButtonCommand(ParseMouseButton(ReadRequiredValue(args, ref index, token)), MouseKey.Types.KeyActionType.Down));
                    break;

                case "--mouse-up":
                    FlushPendingMouseMove();
                    commands.Add(new MouseButtonCommand(ParseMouseButton(ReadRequiredValue(args, ref index, token)), MouseKey.Types.KeyActionType.Up));
                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{token}'.");
            }
        }

        FlushPendingMouseMove();

        if (commands.Count == 0)
        {
            throw new ArgumentException("No commands specified.");
        }

        return new ClientOptions(url, commands);
    }

    private static string ReadRequiredValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }

    private static float ParseFloat(string value, string optionName)
    {
        if (!float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Invalid numeric value '{value}' for {optionName}.");
        }

        return parsed;
    }

    private static int ParseNonNegativeInt(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            throw new ArgumentException($"Invalid non-negative integer '{value}' for {optionName}.");
        }

        return parsed;
    }

    private static int ParseMouseButton(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var buttonId))
        {
            if (buttonId < 0 || buttonId > 4)
            {
                throw new ArgumentException("Mouse button id must be between 0 and 4.");
            }

            return buttonId;
        }

        return value.ToLowerInvariant() switch
        {
            "left" => 0,
            "right" => 1,
            "middle" => 2,
            "forward" => 3,
            "back" => 4,
            _ => throw new ArgumentException($"Unknown mouse button '{value}'. Use 0-4 or left/right/middle/forward/back."),
        };
    }

    private static bool IsHelpToken(string token)
    {
        return token is "--help" or "-h" or "/?";
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GRPCRemoteClient");
        Console.WriteLine("Usage:");
        Console.WriteLine("  GRPCRemoteClient [--url http://127.0.0.1:9036] <commands>");
        Console.WriteLine();
        Console.WriteLine("Commands run left-to-right.");
        Console.WriteLine("  --ping");
        Console.WriteLine("  --type <sequence>");
        Console.WriteLine("  --hotkey <sequence>");
        Console.WriteLine("  --wait <milliseconds>");
        Console.WriteLine("  --mouse-move <x,y>");
        Console.WriteLine("  --mouse-move-abs <x,y>");
        Console.WriteLine("  --mouse-click <button>");
        Console.WriteLine("  --mouse-down <button>");
        Console.WriteLine("  --mouse-up <button>");
        Console.WriteLine("  --speed <value>");
        Console.WriteLine("  --acceleration <value>");
        Console.WriteLine("  --get-config");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Buttons: 0=left, 1=right, 2=middle, 3=forward, 4=back.");
        Console.WriteLine("  If --speed or --acceleration appears immediately after --mouse-move, it applies before that move.");
        Console.WriteLine("  Otherwise, --speed and --acceleration update the server config directly.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  GRPCRemoteClient --ping");
        Console.WriteLine("  GRPCRemoteClient --type \"abc{Shift Down}aaa{Shift Up}\"");
        Console.WriteLine("  GRPCRemoteClient --mouse-move 10,-5 --speed 1.0");
        Console.WriteLine("  GRPCRemoteClient --mouse-click 0");
        Console.WriteLine("  GRPCRemoteClient --speed 1.25 --acceleration 0.5 --get-config");
    }

    private sealed record ClientOptions(string Url, IReadOnlyList<IClientCommand> Commands);

    private sealed record PendingMouseMove(float X, float Y, bool Relative, float? Speed, float? Acceleration)
    {
        public static PendingMouseMove Create(string value, bool relative)
        {
            var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                throw new ArgumentException($"Mouse move must be in 'x,y' form. Received '{value}'.");
            }

            return new PendingMouseMove(
                ParseFloat(parts[0], "--mouse-move"),
                ParseFloat(parts[1], "--mouse-move"),
                relative,
                null,
                null);
        }
    }

    private sealed class ConsoleCancellationContext : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public ConsoleCancelEventHandler Handler => OnCancelKeyPress;

        public CancellationToken Token => _cts.Token;

        public void Dispose()
        {
            _cts.Dispose();
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            _ = sender;
            eventArgs.Cancel = true;
            _cts.Cancel();
        }
    }

    private interface IClientCommand
    {
        string DisplayText { get; }

        Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken);
    }

    private sealed class PingCommand : IClientCommand
    {
        public string DisplayText => "ping";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            var response = await client.PingAsync(new Empty(), cancellationToken: cancellationToken);
            return response.Message;
        }
    }

    private sealed class TypeCommand(string text) : IClientCommand
    {
        public string DisplayText => $"type {text}";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            var response = await client.PressHotkeyAsync(new Hotkey
            {
                Hotkey_ = text,
                Type = KeyActionType.Down,
            }, cancellationToken: cancellationToken);

            return response.Message;
        }
    }

    private sealed class MouseButtonCommand(int buttonId, MouseKey.Types.KeyActionType action) : IClientCommand
    {
        public string DisplayText => $"mouse {action.ToString().ToLowerInvariant()} {buttonId}";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            var response = await client.PressMouseKeyAsync(new MouseKey
            {
                Id = buttonId,
                Type = action,
            }, cancellationToken: cancellationToken);

            return response.Message;
        }
    }

    private sealed class MouseMoveCommand(float x, float y, bool relative, float? speed, float? acceleration) : IClientCommand
    {
        public string DisplayText => relative
            ? $"mouse-move {x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}"
            : $"mouse-move-abs {x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            if (speed.HasValue || acceleration.HasValue)
            {
                await ApplyConfigAsync(client, speed, acceleration, cancellationToken);
            }

            var response = await client.MoveMouseAsync(new MouseMove
            {
                X = x,
                Y = y,
                Relative = relative,
            }, cancellationToken: cancellationToken);

            return response.Message;
        }
    }

    private sealed class ConfigCommand(float? speed, float? acceleration) : IClientCommand
    {
        public float? Speed { get; } = speed;

        public float? Acceleration { get; } = acceleration;

        public string DisplayText => $"set-config speed={FormatOptional(Speed)} acceleration={FormatOptional(Acceleration)}";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            var config = await ApplyConfigAsync(client, Speed, Acceleration, cancellationToken);
            return FormatConfig(config);
        }
    }

    private sealed class GetConfigCommand : IClientCommand
    {
        public string DisplayText => "get-config";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            var config = await client.GetConfigAsync(new Empty(), cancellationToken: cancellationToken);
            return FormatConfig(config);
        }
    }

    private sealed class WaitCommand(int milliseconds) : IClientCommand
    {
        public string DisplayText => $"wait {milliseconds}ms";

        public async Task<string?> ExecuteAsync(InputMethods.InputMethodsClient client, CancellationToken cancellationToken)
        {
            _ = client;
            await Task.Delay(milliseconds, cancellationToken);
            return $"Waited {milliseconds}ms";
        }
    }

    private static async Task<Config> ApplyConfigAsync(
        InputMethods.InputMethodsClient client,
        float? speed,
        float? acceleration,
        CancellationToken cancellationToken)
    {
        var request = new Config();

        if (speed.HasValue)
        {
            request.CursorSpeed = speed.Value;
        }

        if (acceleration.HasValue)
        {
            request.CursorAcceleration = acceleration.Value;
        }

        return await client.SetConfigAsync(request, cancellationToken: cancellationToken);
    }

    private static string FormatConfig(Config config)
    {
        return $"CursorSpeed={config.CursorSpeed.ToString(CultureInfo.InvariantCulture)}, CursorAcceleration={config.CursorAcceleration.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatOptional(float? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unchanged";
    }
}

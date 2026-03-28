using System.Globalization;

namespace GRPCRemote.Input;

public sealed record HotkeyStep(RemoteKey Key, RemoteActionType Action, int? WaitMilliseconds = null);

public static class HotkeyParser
{
    public static IReadOnlyList<HotkeyStep> Parse(string hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);

        var steps = new List<HotkeyStep>();
        var index = 0;

        while (index < hotkey.Length)
        {
            if (hotkey[index] == '{')
            {
                var endBrace = hotkey.IndexOf('}', index);
                if (endBrace < 0)
                {
                    AppendCharacterSteps(steps, hotkey[index].ToString());
                    index++;
                    continue;
                }

                var command = hotkey[(index + 1)..endBrace];
                AppendCommandSteps(steps, command);
                index = endBrace + 1;
                continue;
            }

            AppendCharacterSteps(steps, hotkey[index].ToString());
            index++;
        }

        return steps;
    }

    private static void AppendCharacterSteps(ICollection<HotkeyStep> steps, string token)
    {
        var key = RemoteKeyMap.ParseKey(token);
        steps.Add(new HotkeyStep(key, RemoteActionType.Down));
        steps.Add(new HotkeyStep(key, RemoteActionType.Up));
    }

    private static void AppendCommandSteps(ICollection<HotkeyStep> steps, string command)
    {
        var parts = command.Split(':', 2, StringSplitOptions.TrimEntries);
        var tokens = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            throw new FormatException("Hotkey command is empty.");
        }

        var key = RemoteKeyMap.ParseKey(tokens[0]);
        var action = tokens.Length > 1 ? RemoteKeyMap.ParseAction(tokens[1]) : RemoteActionType.Press;
        int? wait = parts.Length > 1
            ? int.Parse(parts[1], CultureInfo.InvariantCulture)
            : null;

        if (action == RemoteActionType.Press)
        {
            steps.Add(new HotkeyStep(key, RemoteActionType.Down));
            steps.Add(new HotkeyStep(key, RemoteActionType.Up, wait));
            return;
        }

        steps.Add(new HotkeyStep(key, action, wait));
    }
}

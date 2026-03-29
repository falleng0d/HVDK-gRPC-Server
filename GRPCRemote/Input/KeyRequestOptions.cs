namespace GRPCRemote.Input;

public sealed record KeyRequestOptions(bool NoRepeat, bool DisableUnwantedModifiers, IReadOnlyList<RemoteKey> Modifiers)
{
    public static KeyRequestOptions Empty { get; } = new(false, false, []);

    public static KeyRequestOptions FromProto(KeyOptions? options)
    {
        if (options is null)
        {
            return Empty;
        }

        return new KeyRequestOptions(
            options.NoRepeat,
            options.NoModifiers,
            options.Modifiers.Select(RemoteKeyMap.FromId).ToArray());
    }
}

public sealed record HotkeyRequestOptions(int? Speed, bool DisableUnwantedModifiers)
{
    public static HotkeyRequestOptions Empty { get; } = new(null, false);

    public static HotkeyRequestOptions FromProto(HotkeyOptions? options)
    {
        if (options is null)
        {
            return Empty;
        }

        return new HotkeyRequestOptions(options.Speed, options.NoModifiers);
    }
}

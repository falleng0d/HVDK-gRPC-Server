namespace GRPCRemote.Input;

public enum RemoteActionType
{
    Up = 0,
    Down = 1,
    Press = 3,
}

public enum RemoteKey
{
    Key0 = 0,
    Key1 = 1,
    Key2 = 2,
    Key3 = 3,
    Key4 = 4,
    Key5 = 5,
    Key6 = 6,
    Key7 = 7,
    Key8 = 8,
    Key9 = 9,
    KeyA = 10,
    KeyB = 11,
    KeyC = 12,
    KeyD = 13,
    KeyE = 14,
    KeyF = 15,
    KeyG = 16,
    KeyH = 17,
    KeyI = 18,
    KeyJ = 19,
    KeyK = 20,
    KeyL = 21,
    KeyM = 22,
    KeyN = 23,
    KeyO = 24,
    KeyP = 25,
    KeyQ = 26,
    KeyR = 27,
    KeyS = 28,
    KeyT = 29,
    KeyU = 30,
    KeyV = 31,
    KeyW = 32,
    KeyX = 33,
    KeyY = 34,
    KeyZ = 35,
    KeyF1 = 36,
    KeyF2 = 37,
    KeyF3 = 38,
    KeyF4 = 39,
    KeyF5 = 40,
    KeyF6 = 41,
    KeyF7 = 42,
    KeyF8 = 43,
    KeyF9 = 44,
    KeyF10 = 45,
    KeyF11 = 46,
    KeyF12 = 47,
    KeyNumLock = 48,
    KeyScroll = 49,
    KeyBack = 50,
    KeyTab = 51,
    KeyReturn = 52,
    KeyLShift = 53,
    KeyRShift = 54,
    KeyLControl = 55,
    KeyRControl = 56,
    KeyLMenu = 57,
    KeyRMenu = 58,
    KeyCapital = 59,
    KeyEscape = 60,
    KeyConvert = 61,
    KeyNonConvert = 62,
    KeyAccept = 63,
    KeyModeChange = 64,
    KeySpace = 65,
    KeyPrior = 66,
    KeyNext = 67,
    KeyEnd = 68,
    KeyHome = 69,
    KeyLeft = 70,
    KeyUp = 71,
    KeyRight = 72,
    KeyDown = 73,
    KeySelect = 74,
    KeyPrint = 75,
    KeyExecute = 76,
    KeySnapshot = 77,
    KeyInsert = 78,
    KeyDelete = 79,
    KeyHelp = 80,
    KeyLSuper = 81,
    KeyRSuper = 82,
    KeyApps = 83,
    KeySleep = 84,
    KeyNumpad0 = 85,
    KeyNumpad1 = 86,
    KeyNumpad2 = 87,
    KeyNumpad3 = 88,
    KeyNumpad4 = 89,
    KeyNumpad5 = 90,
    KeyNumpad6 = 91,
    KeyNumpad7 = 92,
    KeyNumpad8 = 93,
    KeyNumpad9 = 94,
    KeyMultiply = 95,
    KeyAdd = 96,
    KeySeparator = 97,
    KeySubtract = 98,
    KeyDecimal = 99,
    KeyDivide = 100,
    KeyOemPlus = 101,
    KeyOemComma = 102,
    KeyOemMinus = 103,
    KeyOemPeriod = 104,
    KeyOemSemicolon = 105,
    KeyOemForwardSlash = 106,
    KeyOemBacktick = 107,
    KeyOemBracketOpen = 108,
    KeyOemBackslash = 109,
    KeyOemBracketClose = 110,
    KeyOemQuote = 111,
    KeyMediaPlayPause = 112,
    KeyMediaPrevTrack = 113,
    KeyMediaNextTrack = 114,
    KeyVolumeMute = 115,
    KeyVolumeUp = 116,
    KeyVolumeDown = 117,
    KeyMediaStop = 118,
    KeyBrowserBack = 119,
    KeyBrowserForward = 120,
    KeyBrowserRefresh = 121,
}

public enum RemoteMouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
    Forward = 3,
    Back = 4,
}

public static class RemoteKeyMap
{
    private static readonly IReadOnlyDictionary<RemoteKey, byte> HidUsages = new Dictionary<RemoteKey, byte>
    {
        [RemoteKey.KeyA] = 0x04,
        [RemoteKey.KeyB] = 0x05,
        [RemoteKey.KeyC] = 0x06,
        [RemoteKey.KeyD] = 0x07,
        [RemoteKey.KeyE] = 0x08,
        [RemoteKey.KeyF] = 0x09,
        [RemoteKey.KeyG] = 0x0A,
        [RemoteKey.KeyH] = 0x0B,
        [RemoteKey.KeyI] = 0x0C,
        [RemoteKey.KeyJ] = 0x0D,
        [RemoteKey.KeyK] = 0x0E,
        [RemoteKey.KeyL] = 0x0F,
        [RemoteKey.KeyM] = 0x10,
        [RemoteKey.KeyN] = 0x11,
        [RemoteKey.KeyO] = 0x12,
        [RemoteKey.KeyP] = 0x13,
        [RemoteKey.KeyQ] = 0x14,
        [RemoteKey.KeyR] = 0x15,
        [RemoteKey.KeyS] = 0x16,
        [RemoteKey.KeyT] = 0x17,
        [RemoteKey.KeyU] = 0x18,
        [RemoteKey.KeyV] = 0x19,
        [RemoteKey.KeyW] = 0x1A,
        [RemoteKey.KeyX] = 0x1B,
        [RemoteKey.KeyY] = 0x1C,
        [RemoteKey.KeyZ] = 0x1D,
        [RemoteKey.Key1] = 0x1E,
        [RemoteKey.Key2] = 0x1F,
        [RemoteKey.Key3] = 0x20,
        [RemoteKey.Key4] = 0x21,
        [RemoteKey.Key5] = 0x22,
        [RemoteKey.Key6] = 0x23,
        [RemoteKey.Key7] = 0x24,
        [RemoteKey.Key8] = 0x25,
        [RemoteKey.Key9] = 0x26,
        [RemoteKey.Key0] = 0x27,
        [RemoteKey.KeyReturn] = 0x28,
        [RemoteKey.KeyEscape] = 0x29,
        [RemoteKey.KeyBack] = 0x2A,
        [RemoteKey.KeyTab] = 0x2B,
        [RemoteKey.KeySpace] = 0x2C,
        [RemoteKey.KeyOemMinus] = 0x2D,
        [RemoteKey.KeyOemPlus] = 0x2E,
        [RemoteKey.KeyOemBracketOpen] = 0x2F,
        [RemoteKey.KeyOemBracketClose] = 0x30,
        [RemoteKey.KeyOemBackslash] = 0x31,
        [RemoteKey.KeyOemSemicolon] = 0x33,
        [RemoteKey.KeyOemQuote] = 0x34,
        [RemoteKey.KeyOemBacktick] = 0x35,
        [RemoteKey.KeyOemComma] = 0x36,
        [RemoteKey.KeyOemPeriod] = 0x37,
        [RemoteKey.KeyOemForwardSlash] = 0x38,
        [RemoteKey.KeyCapital] = 0x39,
        [RemoteKey.KeyF1] = 0x3A,
        [RemoteKey.KeyF2] = 0x3B,
        [RemoteKey.KeyF3] = 0x3C,
        [RemoteKey.KeyF4] = 0x3D,
        [RemoteKey.KeyF5] = 0x3E,
        [RemoteKey.KeyF6] = 0x3F,
        [RemoteKey.KeyF7] = 0x40,
        [RemoteKey.KeyF8] = 0x41,
        [RemoteKey.KeyF9] = 0x42,
        [RemoteKey.KeyF10] = 0x43,
        [RemoteKey.KeyF11] = 0x44,
        [RemoteKey.KeyF12] = 0x45,
        [RemoteKey.KeyPrint] = 0x46,
        [RemoteKey.KeyScroll] = 0x47,
        [RemoteKey.KeySnapshot] = 0x46,
        [RemoteKey.KeyInsert] = 0x49,
        [RemoteKey.KeyHome] = 0x4A,
        [RemoteKey.KeyPrior] = 0x4B,
        [RemoteKey.KeyDelete] = 0x4C,
        [RemoteKey.KeyEnd] = 0x4D,
        [RemoteKey.KeyNext] = 0x4E,
        [RemoteKey.KeyRight] = 0x4F,
        [RemoteKey.KeyLeft] = 0x50,
        [RemoteKey.KeyDown] = 0x51,
        [RemoteKey.KeyUp] = 0x52,
        [RemoteKey.KeyNumLock] = 0x53,
        [RemoteKey.KeyDivide] = 0x54,
        [RemoteKey.KeyMultiply] = 0x55,
        [RemoteKey.KeySubtract] = 0x56,
        [RemoteKey.KeyAdd] = 0x57,
        [RemoteKey.KeyNumpad1] = 0x59,
        [RemoteKey.KeyNumpad2] = 0x5A,
        [RemoteKey.KeyNumpad3] = 0x5B,
        [RemoteKey.KeyNumpad4] = 0x5C,
        [RemoteKey.KeyNumpad5] = 0x5D,
        [RemoteKey.KeyNumpad6] = 0x5E,
        [RemoteKey.KeyNumpad7] = 0x5F,
        [RemoteKey.KeyNumpad8] = 0x60,
        [RemoteKey.KeyNumpad9] = 0x61,
        [RemoteKey.KeyNumpad0] = 0x62,
        [RemoteKey.KeyDecimal] = 0x63,
        [RemoteKey.KeyMediaPlayPause] = 0xE8,
        [RemoteKey.KeyMediaPrevTrack] = 0xE6,
        [RemoteKey.KeyMediaNextTrack] = 0xE5,
        [RemoteKey.KeyVolumeMute] = 0xE2,
        [RemoteKey.KeyVolumeUp] = 0xEA,
        [RemoteKey.KeyVolumeDown] = 0xE9,
        [RemoteKey.KeyMediaStop] = 0xE7,
        [RemoteKey.KeyBrowserBack] = 0xE4,
        [RemoteKey.KeyBrowserForward] = 0xE3,
        [RemoteKey.KeyBrowserRefresh] = 0xE1,
    };

    private static readonly IReadOnlyDictionary<RemoteKey, byte> ModifierMasks = new Dictionary<RemoteKey, byte>
    {
        [RemoteKey.KeyLControl] = 0x01,
        [RemoteKey.KeyLShift] = 0x02,
        [RemoteKey.KeyLMenu] = 0x04,
        [RemoteKey.KeyLSuper] = 0x08,
        [RemoteKey.KeyRControl] = 0x10,
        [RemoteKey.KeyRShift] = 0x20,
        [RemoteKey.KeyRMenu] = 0x40,
        [RemoteKey.KeyRSuper] = 0x80,
    };

    private static readonly IReadOnlyDictionary<string, RemoteKey> StringToKey = BuildStringToKey();

    public static RemoteKey FromId(int id)
    {
        if (!Enum.IsDefined(typeof(RemoteKey), id))
        {
            throw new ArgumentOutOfRangeException(nameof(id), $"Unsupported key id: {id}");
        }

        return (RemoteKey)id;
    }

    public static RemoteMouseButton MouseButtonFromId(int id)
    {
        if (!Enum.IsDefined(typeof(RemoteMouseButton), id))
        {
            throw new ArgumentOutOfRangeException(nameof(id), $"Unsupported mouse button id: {id}");
        }

        return (RemoteMouseButton)id;
    }

    public static RemoteKey ParseKey(string token)
    {
        var normalized = token.ToUpperInvariant();
        if (StringToKey.TryGetValue(normalized, out var key))
        {
            return key;
        }

        throw new KeyNotFoundException($"Key '{token}' not found in mapping.");
    }

    public static RemoteActionType ParseAction(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "UP" => RemoteActionType.Up,
            "DOWN" => RemoteActionType.Down,
            _ => RemoteActionType.Press,
        };
    }

    public static bool IsModifier(RemoteKey key)
    {
        return ModifierMasks.ContainsKey(key);
    }

    public static byte GetModifierMask(RemoteKey key)
    {
        if (!ModifierMasks.TryGetValue(key, out var value))
        {
            throw new NotSupportedException($"Key {key} is not a modifier.");
        }

        return value;
    }

    public static byte GetStandardHidUsage(RemoteKey key)
    {
        if (!HidUsages.TryGetValue(key, out var value))
        {
            throw new NotSupportedException($"Key {key} is not supported by the HVDK keyboard driver.");
        }

        return value;
    }

    public static byte GetMouseButtonMask(RemoteMouseButton button)
    {
        return button switch
        {
            RemoteMouseButton.Left => 1 << 0,
            RemoteMouseButton.Right => 1 << 1,
            RemoteMouseButton.Middle => 1 << 2,
            RemoteMouseButton.Back => 1 << 3,
            RemoteMouseButton.Forward => 1 << 4,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
    }

    private static IReadOnlyDictionary<string, RemoteKey> BuildStringToKey()
    {
        var map = new Dictionary<string, RemoteKey>
        {
            ["0"] = RemoteKey.Key0,
            ["1"] = RemoteKey.Key1,
            ["2"] = RemoteKey.Key2,
            ["3"] = RemoteKey.Key3,
            ["4"] = RemoteKey.Key4,
            ["5"] = RemoteKey.Key5,
            ["6"] = RemoteKey.Key6,
            ["7"] = RemoteKey.Key7,
            ["8"] = RemoteKey.Key8,
            ["9"] = RemoteKey.Key9,
            ["A"] = RemoteKey.KeyA,
            ["B"] = RemoteKey.KeyB,
            ["C"] = RemoteKey.KeyC,
            ["D"] = RemoteKey.KeyD,
            ["E"] = RemoteKey.KeyE,
            ["F"] = RemoteKey.KeyF,
            ["G"] = RemoteKey.KeyG,
            ["H"] = RemoteKey.KeyH,
            ["I"] = RemoteKey.KeyI,
            ["J"] = RemoteKey.KeyJ,
            ["K"] = RemoteKey.KeyK,
            ["L"] = RemoteKey.KeyL,
            ["M"] = RemoteKey.KeyM,
            ["N"] = RemoteKey.KeyN,
            ["O"] = RemoteKey.KeyO,
            ["P"] = RemoteKey.KeyP,
            ["Q"] = RemoteKey.KeyQ,
            ["R"] = RemoteKey.KeyR,
            ["S"] = RemoteKey.KeyS,
            ["T"] = RemoteKey.KeyT,
            ["U"] = RemoteKey.KeyU,
            ["V"] = RemoteKey.KeyV,
            ["W"] = RemoteKey.KeyW,
            ["X"] = RemoteKey.KeyX,
            ["Y"] = RemoteKey.KeyY,
            ["Z"] = RemoteKey.KeyZ,
            ["F1"] = RemoteKey.KeyF1,
            ["F2"] = RemoteKey.KeyF2,
            ["F3"] = RemoteKey.KeyF3,
            ["F4"] = RemoteKey.KeyF4,
            ["F5"] = RemoteKey.KeyF5,
            ["F6"] = RemoteKey.KeyF6,
            ["F7"] = RemoteKey.KeyF7,
            ["F8"] = RemoteKey.KeyF8,
            ["F9"] = RemoteKey.KeyF9,
            ["F10"] = RemoteKey.KeyF10,
            ["F11"] = RemoteKey.KeyF11,
            ["F12"] = RemoteKey.KeyF12,
            ["NUM_LOCK"] = RemoteKey.KeyNumLock,
            ["SCROLL_LOCK"] = RemoteKey.KeyScroll,
            ["BACKSPACE"] = RemoteKey.KeyBack,
            ["TAB"] = RemoteKey.KeyTab,
            ["ENTER"] = RemoteKey.KeyReturn,
            ["RETURN"] = RemoteKey.KeyReturn,
            ["LSHIFT"] = RemoteKey.KeyLShift,
            ["RSHIFT"] = RemoteKey.KeyRShift,
            ["SHIFT"] = RemoteKey.KeyLShift,
            ["LCTRL"] = RemoteKey.KeyLControl,
            ["RCTRL"] = RemoteKey.KeyRControl,
            ["CTRL"] = RemoteKey.KeyLControl,
            ["LALT"] = RemoteKey.KeyLMenu,
            ["RALT"] = RemoteKey.KeyRMenu,
            ["ALT"] = RemoteKey.KeyLMenu,
            ["CAPS_LOCK"] = RemoteKey.KeyCapital,
            ["ESCAPE"] = RemoteKey.KeyEscape,
            ["ESC"] = RemoteKey.KeyEscape,
            ["SPACE"] = RemoteKey.KeySpace,
            ["PAGE_UP"] = RemoteKey.KeyPrior,
            ["PAGE_DOWN"] = RemoteKey.KeyNext,
            ["END"] = RemoteKey.KeyEnd,
            ["HOME"] = RemoteKey.KeyHome,
            ["LEFT"] = RemoteKey.KeyLeft,
            ["UP"] = RemoteKey.KeyUp,
            ["RIGHT"] = RemoteKey.KeyRight,
            ["DOWN"] = RemoteKey.KeyDown,
            ["PRINT_SCREEN"] = RemoteKey.KeyPrint,
            ["INSERT"] = RemoteKey.KeyInsert,
            ["DELETE"] = RemoteKey.KeyDelete,
            ["DEL"] = RemoteKey.KeyDelete,
            ["LWIN"] = RemoteKey.KeyLSuper,
            ["RWIN"] = RemoteKey.KeyRSuper,
            ["WIN"] = RemoteKey.KeyLSuper,
            ["NUMPAD_0"] = RemoteKey.KeyNumpad0,
            ["NUMPAD_1"] = RemoteKey.KeyNumpad1,
            ["NUMPAD_2"] = RemoteKey.KeyNumpad2,
            ["NUMPAD_3"] = RemoteKey.KeyNumpad3,
            ["NUMPAD_4"] = RemoteKey.KeyNumpad4,
            ["NUMPAD_5"] = RemoteKey.KeyNumpad5,
            ["NUMPAD_6"] = RemoteKey.KeyNumpad6,
            ["NUMPAD_7"] = RemoteKey.KeyNumpad7,
            ["NUMPAD_8"] = RemoteKey.KeyNumpad8,
            ["NUMPAD_9"] = RemoteKey.KeyNumpad9,
            ["NUMPAD_MULTIPLY"] = RemoteKey.KeyMultiply,
            ["NUMPAD_ADD"] = RemoteKey.KeyAdd,
            ["NUMPAD_SUBTRACT"] = RemoteKey.KeySubtract,
            ["NUMPAD_DECIMAL"] = RemoteKey.KeyDecimal,
            ["NUMPAD_DIVIDE"] = RemoteKey.KeyDivide,
            ["+"] = RemoteKey.KeyOemPlus,
            [","] = RemoteKey.KeyOemComma,
            ["-"] = RemoteKey.KeyOemMinus,
            ["."] = RemoteKey.KeyOemPeriod,
            [";"] = RemoteKey.KeyOemSemicolon,
            ["/"] = RemoteKey.KeyOemForwardSlash,
            ["`"] = RemoteKey.KeyOemBacktick,
            ["["] = RemoteKey.KeyOemBracketOpen,
            ["\\"] = RemoteKey.KeyOemBackslash,
            ["]"] = RemoteKey.KeyOemBracketClose,
            ["'"] = RemoteKey.KeyOemQuote,
        };

        return map;
    }
}

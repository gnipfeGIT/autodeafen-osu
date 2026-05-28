using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AutoDeafenOsu;

public enum HotkeySendMethod
{
    SendInputScanCode,
    SendInputVirtualKey,
    KeybdEventScanCode,
    KeybdEventVirtualKey
}

public sealed class DiscordHotkeySender
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFExtendedKey = 0x0001;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFScanCode = 0x0008;
    private const uint MapVkToVsc = 0;

    public void SendToggleDeafen(string hotkey, HotkeySendMethod method)
    {
        var parsed = Hotkey.Parse(hotkey);
        if (parsed.Key == Keys.None)
        {
            throw new InvalidOperationException("Choose a hotkey with a real key, for example Ctrl+Shift+D.");
        }

        foreach (var modifier in parsed.Modifiers)
        {
            SendKey(modifier, keyUp: false, method);
        }

        Thread.Sleep(45);
        SendKey(parsed.Key, keyUp: false, method);
        Thread.Sleep(55);
        SendKey(parsed.Key, keyUp: true, method);
        Thread.Sleep(35);

        for (var i = parsed.Modifiers.Count - 1; i >= 0; i--)
        {
            SendKey(parsed.Modifiers[i], keyUp: true, method);
            Thread.Sleep(20);
        }
    }

    private static void SendKey(Keys key, bool keyUp, HotkeySendMethod method)
    {
        switch (method)
        {
            case HotkeySendMethod.SendInputScanCode:
                SendInputChecked(KeyboardInputScanCode(key, keyUp));
                break;
            case HotkeySendMethod.SendInputVirtualKey:
                SendInputChecked(KeyboardInputVirtualKey(key, keyUp));
                break;
            case HotkeySendMethod.KeybdEventScanCode:
                keybd_event(0, ToScanCode(key), KeyEventFScanCode | ExtendedKeyFlag(key) | (keyUp ? KeyEventFKeyUp : 0), UIntPtr.Zero);
                break;
            case HotkeySendMethod.KeybdEventVirtualKey:
                keybd_event((byte)key, 0, keyUp ? KeyEventFKeyUp : 0, UIntPtr.Zero);
                break;
            default:
                throw new InvalidOperationException($"Unknown hotkey send method '{method}'.");
        }
    }

    private static void SendInputChecked(Input input)
    {
        var sent = SendInput(1, [input], Marshal.SizeOf<Input>());
        if (sent != 1)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"Windows did not accept the hotkey input. Win32 error: {errorCode}.");
        }
    }

    private static Input KeyboardInputScanCode(Keys key, bool keyUp) => new()
    {
        type = InputKeyboard,
        union = new InputUnion
        {
            ki = new KeyboardInputNative
            {
                wScan = ToScanCode(key),
                dwFlags = KeyEventFScanCode | ExtendedKeyFlag(key) | (keyUp ? KeyEventFKeyUp : 0)
            }
        }
    };

    private static Input KeyboardInputVirtualKey(Keys key, bool keyUp) => new()
    {
        type = InputKeyboard,
        union = new InputUnion
        {
            ki = new KeyboardInputNative
            {
                wVk = (ushort)key,
                dwFlags = ExtendedKeyFlag(key) | (keyUp ? KeyEventFKeyUp : 0)
            }
        }
    };

    private static ushort ToScanCode(Keys key)
    {
        var scanCode = MapVirtualKey((uint)key, MapVkToVsc);
        if (scanCode == 0)
        {
            throw new InvalidOperationException($"Could not map '{key}' to a keyboard scan code.");
        }

        return (ushort)scanCode;
    }

    private static uint ExtendedKeyFlag(Keys key)
    {
        return key is Keys.RControlKey or Keys.RMenu or Keys.Insert or Keys.Delete or Keys.Home or Keys.End
            or Keys.PageUp or Keys.PageDown or Keys.Up or Keys.Down or Keys.Left or Keys.Right
            ? KeyEventFExtendedKey
            : 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, ushort bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int type;
        public InputUnion union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInputNative ki;

        [FieldOffset(0)]
        public MouseInputNative mi;

        [FieldOffset(0)]
        public HardwareInputNative hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputNative
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputNative
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInputNative
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private sealed record Hotkey(IReadOnlyList<Keys> Modifiers, Keys Key)
    {
        public static Hotkey Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new Hotkey([], Keys.None);
            }

            var modifiers = new List<Keys>();
            var key = Keys.None;

            foreach (var rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var part = rawPart.Replace(" ", "", StringComparison.OrdinalIgnoreCase);
                switch (part.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers.Add(Keys.ControlKey);
                        break;
                    case "SHIFT":
                        modifiers.Add(Keys.ShiftKey);
                        break;
                    case "ALT":
                        modifiers.Add(Keys.Menu);
                        break;
                    case "WIN":
                    case "WINDOWS":
                        modifiers.Add(Keys.LWin);
                        break;
                    default:
                        if (!Enum.TryParse(part, true, out Keys parsedKey))
                        {
                            throw new InvalidOperationException($"Unknown hotkey key '{rawPart}'.");
                        }

                        key = parsedKey;
                        break;
                }
            }

            return new Hotkey(modifiers.Distinct().ToArray(), key);
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agi.Types;

namespace Agi;

/// <summary>
/// Cross-platform action executor for desktop automation.
///
/// Coordinates received from the agent are in physical (screenshot) pixel space.
/// On macOS, they need to be converted to logical coordinates for CGEvent.
/// On Windows and Linux, coordinates are used directly.
/// </summary>
public static class Executor
{
    private static bool _xdotoolChecked;
    private static double? _cachedScaleFactor;

    #region Windows P/Invoke

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x1000;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const byte VK_SHIFT = 0x10;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_MENU = 0x12;
    private const byte VK_LWIN = 0x5B;
    private const byte VK_RETURN = 0x0D;
    private const byte VK_TAB = 0x09;
    private const byte VK_ESCAPE = 0x1B;
    private const byte VK_BACK = 0x08;
    private const byte VK_DELETE = 0x2E;
    private const byte VK_UP = 0x26;
    private const byte VK_DOWN = 0x28;
    private const byte VK_LEFT = 0x25;
    private const byte VK_RIGHT = 0x27;
    private const byte VK_SPACE = 0x20;
    private const byte VK_HOME = 0x24;
    private const byte VK_END = 0x23;
    private const byte VK_PRIOR = 0x21;
    private const byte VK_NEXT = 0x22;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public INPUTUNION u; }

    [DllImport("user32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    private static extern short VkKeyScan(char ch);

    [DllImport("shcore.dll"), SuppressUnmanagedCodeSecurity]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    #endregion

    /// <summary>
    /// Execute an action on the local device.
    /// </summary>
    public static async Task<bool> ExecuteActionAsync(DesktopAction action)
    {
        return action.Type switch
        {
            DesktopActionType.Click or DesktopActionType.DoubleClick or
            DesktopActionType.RightClick or DesktopActionType.TripleClick => await ExecuteClickAsync(action),
            DesktopActionType.Hover => await ExecuteHoverAsync(action),
            DesktopActionType.Type => await ExecuteTypeAsync(action),
            DesktopActionType.Key or DesktopActionType.Hotkey => await ExecuteKeyAsync(action),
            DesktopActionType.Scroll => await ExecuteScrollAsync(action),
            DesktopActionType.Drag => await ExecuteDragAsync(action),
            DesktopActionType.Wait => await ExecuteWaitAsync(action),
            DesktopActionType.Finish or DesktopActionType.Fail or
            DesktopActionType.Confirm or DesktopActionType.AskQuestion or
            DesktopActionType.Finished or DesktopActionType.AwaitUserInput => true,
            _ => false
        };
    }

    /// <summary>
    /// Execute multiple actions in sequence.
    /// </summary>
    public static async Task ExecuteActionsAsync(IEnumerable<DesktopAction> actions)
    {
        foreach (var action in actions)
        {
            await ExecuteActionAsync(action);
        }
    }

    /// <summary>
    /// Get the DPI scale factor.
    /// </summary>
    public static async Task<double> GetScaleFactorAsync()
    {
        if (_cachedScaleFactor.HasValue)
            return _cachedScaleFactor.Value;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var jxaScript = @"
ObjC.import('Cocoa');
var screen = $.NSScreen.mainScreen;
screen.backingScaleFactor;";
                var result = await RunJxaScriptWithResultAsync(jxaScript);
                _cachedScaleFactor = double.TryParse(result.Trim(), out var scale) ? scale : 1.0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var point = new POINT { X = 0, Y = 0 };
                var monitor = MonitorFromPoint(point, 1);
                if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0)
                    _cachedScaleFactor = dpiX / 96.0;
                else
                    _cachedScaleFactor = 1.0;
            }
            else
            {
                var scale = Environment.GetEnvironmentVariable("GDK_SCALE");
                _cachedScaleFactor = scale != null && double.TryParse(scale, out var s) ? s : 1.0;
            }
        }
        catch
        {
            _cachedScaleFactor = 1.0;
        }

        return _cachedScaleFactor.Value;
    }

    /// <summary>
    /// Get the current screen size in physical pixels.
    /// </summary>
    public static async Task<(int Width, int Height)> GetScreenSizeAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var result = await RunCommandAsync("system_profiler", "SPDisplaysDataType");
                var match = Regex.Match(result, @"Resolution:\s*(\d+)\s*x\s*(\d+)");
                if (match.Success)
                    return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }
            catch { }

            try
            {
                var jxaScript = @"
ObjC.import('Cocoa');
var screen = $.NSScreen.mainScreen;
var frame = screen.frame;
var scale = screen.backingScaleFactor;
JSON.stringify({ width: frame.size.width * scale, height: frame.size.height * scale });";
                var result = await RunJxaScriptWithResultAsync(jxaScript);
                var json = JsonSerializer.Deserialize<Dictionary<string, double>>(result);
                if (json != null)
                    return ((int)Math.Round(json["width"]), (int)Math.Round(json["height"]));
            }
            catch { }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var result = await RunCommandAsync("powershell", "-Command",
                "[System.Windows.Forms.Screen]::PrimaryScreen.Bounds | Select-Object Width,Height | ConvertTo-Json");
            var json = JsonSerializer.Deserialize<Dictionary<string, int>>(result);
            if (json != null)
                return (json["Width"], json["Height"]);
        }
        else
        {
            try
            {
                var result = await RunCommandAsync("xdpyinfo");
                var match = Regex.Match(result, @"dimensions:\s*(\d+)x(\d+)");
                if (match.Success)
                    return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }
            catch { }
        }

        return (1920, 1080);
    }

    private static async Task<(int X, int Y)> ToLogicalCoordsAsync(int x, int y)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (x, y);

        var scale = await GetScaleFactorAsync();
        return ((int)Math.Round(x / scale), (int)Math.Round(y / scale));
    }

    private static async Task EnsureXdotoolAsync()
    {
        if (_xdotoolChecked) return;
        try
        {
            await RunCommandAsync("which", "xdotool");
            _xdotoolChecked = true;
        }
        catch
        {
            throw new InvalidOperationException(
                "xdotool is required for Linux input simulation but was not found.\n" +
                "Install: sudo apt install xdotool");
        }
    }

    private static async Task<bool> ExecuteClickAsync(DesktopAction action)
    {
        var (x, y) = await ToLogicalCoordsAsync(action.X ?? 0, action.Y ?? 0);
        var actionType = action.Type;
        var isRightClick = actionType == DesktopActionType.RightClick;
        var clickCount = actionType switch
        {
            DesktopActionType.TripleClick => 3,
            DesktopActionType.DoubleClick => 2,
            _ => 1
        };

        Console.WriteLine($"[executor] {actionType}: ({x}, {y})");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var mouseButton = isRightClick ? "kCGMouseButtonRight" : "kCGMouseButtonLeft";
            var mouseDown = isRightClick ? "kCGEventRightMouseDown" : "kCGEventLeftMouseDown";
            var mouseUp = isRightClick ? "kCGEventRightMouseUp" : "kCGEventLeftMouseUp";

            var jxaScript = $@"
ObjC.import('Cocoa');
var point = $.CGPointMake({x}, {y});
for (var i = 0; i < {clickCount}; i++) {{
    var mouseDown = $.CGEventCreateMouseEvent($(), $.{mouseDown}, point, $.{mouseButton});
    $.CGEventSetIntegerValueField(mouseDown, $.kCGMouseEventClickState, i + 1);
    $.CGEventPost($.kCGHIDEventTap, mouseDown);
    var mouseUp = $.CGEventCreateMouseEvent($(), $.{mouseUp}, point, $.{mouseButton});
    $.CGEventSetIntegerValueField(mouseUp, $.kCGMouseEventClickState, i + 1);
    $.CGEventPost($.kCGHIDEventTap, mouseUp);
}}";
            await RunJxaScriptAsync(jxaScript);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetCursorPos(x, y);
            var downFlag = isRightClick ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
            var upFlag = isRightClick ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

            for (int i = 0; i < clickCount; i++)
            {
                mouse_event(downFlag, 0, 0, 0, IntPtr.Zero);
                mouse_event(upFlag, 0, 0, 0, IntPtr.Zero);
                if (i < clickCount - 1) await Task.Delay(50);
            }
        }
        else
        {
            await EnsureXdotoolAsync();
            var clickOpt = isRightClick ? "3" : "1";
            var repeat = clickCount > 1 ? $"--repeat {clickCount}" : "";
            await RunCommandAsync("xdotool", $"mousemove {x} {y} click {repeat} {clickOpt}");
        }

        return true;
    }

    private static async Task<bool> ExecuteHoverAsync(DesktopAction action)
    {
        var (x, y) = await ToLogicalCoordsAsync(action.X ?? 0, action.Y ?? 0);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var jxaScript = $@"
ObjC.import('Cocoa');
var point = $.CGPointMake({x}, {y});
var move = $.CGEventCreateMouseEvent($(), $.kCGEventMouseMoved, point, 0);
$.CGEventPost($.kCGHIDEventTap, move);";
            await RunJxaScriptAsync(jxaScript);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetCursorPos(x, y);
        }
        else
        {
            await EnsureXdotoolAsync();
            await RunCommandAsync("xdotool", $"mousemove {x} {y}");
        }

        return true;
    }

    private static async Task<bool> ExecuteTypeAsync(DesktopAction action)
    {
        var text = action.Content ?? action.Text ?? "";
        if (string.IsNullOrEmpty(text)) return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
            await RunShellCommandAsync($"osascript -e 'tell application \"System Events\" to keystroke \"{escaped}\"'");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var inputs = new INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                inputs[i * 2].type = INPUT_KEYBOARD;
                inputs[i * 2].u.ki.wVk = 0;
                inputs[i * 2].u.ki.wScan = c;
                inputs[i * 2].u.ki.dwFlags = KEYEVENTF_UNICODE;

                inputs[i * 2 + 1].type = INPUT_KEYBOARD;
                inputs[i * 2 + 1].u.ki.wVk = 0;
                inputs[i * 2 + 1].u.ki.wScan = c;
                inputs[i * 2 + 1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
        else
        {
            await EnsureXdotoolAsync();
            await RunCommandAsync("xdotool", $"type \"{text}\"");
        }

        return true;
    }

    private static async Task<bool> ExecuteKeyAsync(DesktopAction action)
    {
        var key = action.Key?.ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(key)) return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var keyMap = TranslateKeyMac(key);
            await RunShellCommandAsync($"osascript -e 'tell application \"System Events\" to {keyMap}'");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await SendWindowsKeyAsync(key);
        }
        else
        {
            await EnsureXdotoolAsync();
            var keyMap = TranslateKeyLinux(key);
            await RunCommandAsync("xdotool", $"key {keyMap}");
        }

        return true;
    }

    private static async Task<bool> ExecuteScrollAsync(DesktopAction action)
    {
        var (x, y) = await ToLogicalCoordsAsync(action.X ?? 0, action.Y ?? 0);
        var direction = action.Direction ?? ScrollDirection.Down;
        var amount = action.Amount ?? 3;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var scrollY = direction == ScrollDirection.Up ? amount * 10 : direction == ScrollDirection.Down ? -amount * 10 : 0;
            var scrollX = direction == ScrollDirection.Left ? amount * 10 : direction == ScrollDirection.Right ? -amount * 10 : 0;

            var jxaScript = $@"
ObjC.import('Cocoa');
var point = $.CGPointMake({x}, {y});
var move = $.CGEventCreateMouseEvent($(), $.kCGEventMouseMoved, point, 0);
$.CGEventPost($.kCGHIDEventTap, move);
var scroll = $.CGEventCreateScrollWheelEvent($(), $.kCGScrollEventUnitLine, 2, {scrollY}, {scrollX});
$.CGEventPost($.kCGHIDEventTap, scroll);";
            await RunJxaScriptAsync(jxaScript);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetCursorPos(x, y);
            var scrollAmount = (direction == ScrollDirection.Up || direction == ScrollDirection.Left ? 120 : -120) * amount;
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, scrollAmount, IntPtr.Zero);
        }
        else
        {
            await EnsureXdotoolAsync();
            await RunCommandAsync("xdotool", $"mousemove {x} {y}");
            var button = direction switch
            {
                ScrollDirection.Up => 4,
                ScrollDirection.Down => 5,
                ScrollDirection.Left => 6,
                _ => 7
            };
            await RunCommandAsync("xdotool", $"click --repeat {amount} {button}");
        }

        return true;
    }

    private static async Task<bool> ExecuteDragAsync(DesktopAction action)
    {
        var (sx, sy) = await ToLogicalCoordsAsync(action.StartX ?? 0, action.StartY ?? 0);
        var (ex, ey) = await ToLogicalCoordsAsync(action.EndX ?? 0, action.EndY ?? 0);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var jxaScript = $@"
ObjC.import('Cocoa');
var startPoint = $.CGPointMake({sx}, {sy});
var endPoint = $.CGPointMake({ex}, {ey});
var mouseDown = $.CGEventCreateMouseEvent($(), $.kCGEventLeftMouseDown, startPoint, $.kCGMouseButtonLeft);
$.CGEventPost($.kCGHIDEventTap, mouseDown);
delay(0.05);
var drag = $.CGEventCreateMouseEvent($(), $.kCGEventLeftMouseDragged, endPoint, $.kCGMouseButtonLeft);
$.CGEventPost($.kCGHIDEventTap, drag);
delay(0.05);
var mouseUp = $.CGEventCreateMouseEvent($(), $.kCGEventLeftMouseUp, endPoint, $.kCGMouseButtonLeft);
$.CGEventPost($.kCGHIDEventTap, mouseUp);";
            await RunJxaScriptAsync(jxaScript);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetCursorPos(sx, sy);
            await Task.Delay(10);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            await Task.Delay(50);

            var steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                var cx = sx + (ex - sx) * i / steps;
                var cy = sy + (ey - sy) * i / steps;
                SetCursorPos(cx, cy);
                await Task.Delay(10);
            }

            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        }
        else
        {
            await EnsureXdotoolAsync();
            await RunCommandAsync("xdotool", $"mousemove {sx} {sy} mousedown 1 mousemove {ex} {ey} mouseup 1");
        }

        return true;
    }

    private static async Task<bool> ExecuteWaitAsync(DesktopAction action)
    {
        var duration = action.Duration ?? 1.0;
        await Task.Delay((int)(duration * 1000));
        return true;
    }

    private static async Task SendWindowsKeyAsync(string key)
    {
        var keysToPress = new List<byte>();
        var mainKey = key;

        if (key.Contains('+'))
        {
            var parts = key.Split('+').Select(k => k.Trim().ToLowerInvariant()).ToArray();
            foreach (var part in parts)
            {
                if (part is "cmd" or "command" or "meta" or "ctrl" or "control")
                    keysToPress.Add(VK_CONTROL);
                else if (part is "alt" or "option")
                    keysToPress.Add(VK_MENU);
                else if (part == "shift")
                    keysToPress.Add(VK_SHIFT);
                else if (part is "win" or "super")
                    keysToPress.Add(VK_LWIN);
                else
                    mainKey = part;
            }
        }

        var vk = TranslateKeyToVK(mainKey);

        foreach (var modifier in keysToPress)
        {
            var input = new INPUT { type = INPUT_KEYBOARD, u = { ki = { wVk = modifier, dwFlags = KEYEVENTF_KEYDOWN } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        var downInput = new INPUT { type = INPUT_KEYBOARD, u = { ki = { wVk = vk, dwFlags = KEYEVENTF_KEYDOWN } } };
        SendInput(1, new[] { downInput }, Marshal.SizeOf<INPUT>());
        await Task.Delay(10);
        var upInput = new INPUT { type = INPUT_KEYBOARD, u = { ki = { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } };
        SendInput(1, new[] { upInput }, Marshal.SizeOf<INPUT>());

        for (int i = keysToPress.Count - 1; i >= 0; i--)
        {
            var input = new INPUT { type = INPUT_KEYBOARD, u = { ki = { wVk = keysToPress[i], dwFlags = KEYEVENTF_KEYUP } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }
    }

    private static ushort TranslateKeyToVK(string key) => key.ToLowerInvariant() switch
    {
        "enter" or "return" => VK_RETURN,
        "tab" => VK_TAB,
        "escape" or "esc" => VK_ESCAPE,
        "backspace" => VK_BACK,
        "delete" => VK_DELETE,
        "up" => VK_UP,
        "down" => VK_DOWN,
        "left" => VK_LEFT,
        "right" => VK_RIGHT,
        "space" => VK_SPACE,
        "home" => VK_HOME,
        "end" => VK_END,
        "pageup" => VK_PRIOR,
        "pagedown" => VK_NEXT,
        _ when key.Length == 1 => (ushort)(VkKeyScan(key[0]) & 0xFF),
        _ => 0
    };

    private static string TranslateKeyMac(string key)
    {
        if (key.Contains('+'))
        {
            var parts = key.Split('+').Select(k => k.Trim()).ToArray();
            var modifiers = new List<string>();
            var mainKey = "";

            foreach (var part in parts)
            {
                if (new[] { "cmd", "command", "meta", "super" }.Contains(part))
                    modifiers.Add("command down");
                else if (new[] { "ctrl", "control" }.Contains(part))
                    modifiers.Add("control down");
                else if (new[] { "alt", "option" }.Contains(part))
                    modifiers.Add("option down");
                else if (part == "shift")
                    modifiers.Add("shift down");
                else
                    mainKey = part;
            }

            var modStr = modifiers.Count > 0 ? $"using {{{string.Join(", ", modifiers)}}}" : "";
            return $"keystroke \"{mainKey}\" {modStr}";
        }

        return key switch
        {
            "enter" or "return" => "key code 36",
            "tab" => "key code 48",
            "escape" or "esc" => "key code 53",
            "backspace" => "key code 51",
            "delete" => "key code 117",
            "up" => "key code 126",
            "down" => "key code 125",
            "left" => "key code 123",
            "right" => "key code 124",
            "space" => "key code 49",
            _ => $"keystroke \"{key}\""
        };
    }

    private static string TranslateKeyLinux(string key)
    {
        if (key.Contains('+'))
        {
            var parts = key.Split('+').Select(k => k.Trim()).ToArray();
            var mapped = parts.Select(part => part switch
            {
                "cmd" or "command" or "meta" or "super" => "super",
                "ctrl" or "control" => "ctrl",
                "alt" or "option" => "alt",
                _ => part
            });
            return string.Join("+", mapped);
        }

        return key switch
        {
            "enter" or "return" => "Return",
            "tab" => "Tab",
            "escape" or "esc" => "Escape",
            "backspace" => "BackSpace",
            "delete" => "Delete",
            "up" => "Up",
            "down" => "Down",
            "left" => "Left",
            "right" => "Right",
            "space" => "space",
            _ => key
        };
    }

    private static async Task RunJxaScriptAsync(string script)
    {
        var tempFile = Path.GetTempFileName() + ".js";
        try
        {
            await File.WriteAllTextAsync(tempFile, script);
            await RunCommandAsync("osascript", "-l", "JavaScript", tempFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static async Task<string> RunJxaScriptWithResultAsync(string script)
    {
        var tempFile = Path.GetTempFileName() + ".js";
        try
        {
            await File.WriteAllTextAsync(tempFile, script);
            return await RunCommandAsync("osascript", "-l", "JavaScript", tempFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static async Task<string> RunCommandAsync(string command, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = string.Join(" ", arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    private static async Task<string> RunShellCommandAsync(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.StandardInput.WriteLineAsync(command);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}

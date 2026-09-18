using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;
using System.Windows.Forms;

ApplicationConfiguration.Initialize();
Application.Run(new ProbeWindow());

sealed class ProbeWindow : Form
{
    const int WM_INPUT = 0x00FF;
    const uint RID_INPUT = 0x10000003;
    const uint RIDEV_INPUTSINK = 0x00000100;
    const uint RIM_TYPEKEYBOARD = 1;

    [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] struct RAWINPUTHEADER { public uint Type, Size; public IntPtr Device, WParam; }
    [StructLayout(LayoutKind.Sequential)] struct RAWKEYBOARD { public ushort MakeCode, Flags, Reserved, VKey; public uint Message, ExtraInformation; }

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);
    [DllImport("user32.dll")] static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);
    [StructLayout(LayoutKind.Sequential)] struct KBDLLHOOKSTRUCT { public uint VkCode, ScanCode, Flags, Time; public UIntPtr ExtraInfo; }
    delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100;
    const int WM_SYSKEYDOWN = 0x0104;
    readonly LowLevelKeyboardProc hookCallback;
    IntPtr keyboardHook;
    const uint KEYEVENTF_KEYUP = 0x0002;
    bool copyPending;
    readonly object gestureLock = new();

    public ProbeWindow()
    {
        ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.FixedToolWindow; Opacity = 0; Width = 1; Height = 1;
        var devices = new[]
        {
            new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x06, Flags = RIDEV_INPUTSINK, Target = Handle },
            new RAWINPUTDEVICE { UsagePage = 0x0C, Usage = 0x01, Flags = RIDEV_INPUTSINK, Target = Handle },
        };
        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        hookCallback = LowLevelKeyboardHook;
        keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, GetModuleHandle(null), 0);
        if (keyboardHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        Console.WriteLine("RAW_INPUT_READY; press M5 keyboard keys, Ctrl+C to stop.");
    }

    protected override void Dispose(bool disposing)
    {
        if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
        base.Dispose(disposing);
    }

    IntPtr LowLevelKeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var key = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (key.VkCode == 0xAC)
            {
                HandleBrowserHomeGesture();
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(keyboardHook, code, wParam, lParam);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT) ReadInput(m.LParam);
        base.WndProc(ref m);
    }

    void ReadInput(IntPtr handle)
    {
        uint size = 0; GetRawInputData(handle, RID_INPUT, IntPtr.Zero, ref size, 24);
        if (size == 0) return;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(handle, RID_INPUT, buffer, ref size, 24) == uint.MaxValue) return;
            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            if (header.Type != RIM_TYPEKEYBOARD) return;
            var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(buffer + 24);
            uint chars = 512; var path = new StringBuilder((int)chars); GetRawInputDeviceInfo(header.Device, 0x20000007, path, ref chars);
            var devicePath = path.ToString();
            var isM5 = devicePath.Contains("VID_1915&PID_1025", StringComparison.OrdinalIgnoreCase);
            var isMedia = keyboard.VKey is 0xAC or 0xAD;
            if (!isM5 && !isMedia) return;
            var label = keyboard.MakeCode switch
            {
                0x48 => "UP/HOME",
                0x4B => "LEFT",
                0x4D => "RIGHT",
                0x50 => "DOWN",
                0x23 => "HOME",
                0x1C => "ENTER/OK",
                0x2E => "FUNCTION-C",
                0x20 => "FUNCTION-D",
                _ => "UNKNOWN"
            };
            Console.WriteLine($"KEY label={label} device={devicePath} make=0x{keyboard.MakeCode:X2} vkey=0x{keyboard.VKey:X2} flags=0x{keyboard.Flags:X}");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    void HandleBrowserHomeGesture()
    {
        lock (gestureLock)
        {
            if (copyPending)
            {
                copyPending = false;
                SendShortcut(0x56); // Ctrl+V
                return;
            }

            copyPending = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(400);
                lock (gestureLock)
                {
                    if (!copyPending) return;
                    copyPending = false;
                    SendShortcut(0x43); // Ctrl+C
                }
            });
        }
    }

    static void SendShortcut(byte key)
    {
        keybd_event(0x11, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Console.WriteLine(key == 0x43 ? "ACTION Ctrl+C" : "ACTION Ctrl+V");
    }
}

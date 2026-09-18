using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;

// M5 2.4G USB receiver (the Bluetooth identity is 1D5A/C081).
const ushort VendorId = 0x3554;
const ushort ProductId = 0xFC03;

var allHidDevices = (await DeviceInformation.FindAllAsync()).ToList();
var deviceInfo = allHidDevices
    .Where(static info => (info.Id.Contains("VID_3554", StringComparison.OrdinalIgnoreCase)
                        || info.Id.Contains("VID&3554", StringComparison.OrdinalIgnoreCase))
                       && (info.Id.Contains("PID_FC03", StringComparison.OrdinalIgnoreCase)
                        || info.Id.Contains("PID&FC03", StringComparison.OrdinalIgnoreCase)))
    .ToList();

if (deviceInfo.Count == 0)
{
    Console.Error.WriteLine("No HID interface found for the keyboard/mouse/consumer usages.");
    foreach (var info in allHidDevices)
    {
        Console.Error.WriteLine($"  name={info.Name} id={info.Id}");
    }
    Console.Error.WriteLine($"No HID device found for VID={VendorId:X4} PID={ProductId:X4}.");
    return 1;
}

var devices = new List<HidDevice>();
foreach (var info in deviceInfo)
{
    HidDevice? device;
    try
    {
        device = await HidDevice.FromIdAsync(info.Id, FileAccessMode.Read);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"OPEN_FAILED name={info.Name} id={info.Id} error={error.Message}");
        continue;
    }
    if (device is null)
    {
        Console.Error.WriteLine($"OPEN_FAILED name={info.Name} id={info.Id}");
        continue;
    }

    devices.Add(device);
    Console.WriteLine($"DEVICE_READY name={info.Name} id={info.Id}");
    device.InputReportReceived += (_, args) =>
    {
        var report = args.Report;
        var bytes = new byte[report.Data.Length];
        using var reader = DataReader.FromBuffer(report.Data);
        reader.ReadBytes(bytes);
        var hex = string.Join(' ', bytes.Select(static b => b.ToString("X2")));
        Console.WriteLine($"REPORT id=0x{report.Id:X2} len={bytes.Length} hex={hex}");
    };
}

if (devices.Count == 0)
{
    return 2;
}

Console.WriteLine("PROBE_READY; press keys on the remote, Ctrl+C to stop.");
await Task.Delay(Timeout.InfiniteTimeSpan);
return 0;

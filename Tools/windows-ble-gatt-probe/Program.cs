using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

const string DeviceName = "M5 keyboard";
var hidServiceUuid = GattServiceUuids.HumanInterfaceDevice;

Console.WriteLine($"Searching for paired BLE device '{DeviceName}'...");
var allBle = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());
var found = allBle.Where(static info =>
    info.Name.Contains("M5", StringComparison.OrdinalIgnoreCase) ||
    info.Id.Contains("4CB593D2A881", StringComparison.OrdinalIgnoreCase) ||
    info.Id.Contains("4cb593d2a881", StringComparison.OrdinalIgnoreCase)).ToList();

if (found.Count == 0)
{
    Console.Error.WriteLine("No exact match. BLE candidates:");
    foreach (var info in allBle.Where(static info =>
        info.Name.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
        info.Name.Contains("M5", StringComparison.OrdinalIgnoreCase)))
    {
        Console.Error.WriteLine($"  name={info.Name} id={info.Id}");
    }
    Console.Error.WriteLine("Device not found. Pair and connect M5 keyboard in Windows Bluetooth settings first.");
    return 1;
}

var device = await BluetoothLEDevice.FromIdAsync(found[0].Id);
if (device is null)
{
    Console.Error.WriteLine($"Could not open {DeviceName}.");
    return 2;
}

Console.WriteLine($"DEVICE_READY name={device.Name} address=0x{device.BluetoothAddress:X12}");
Console.WriteLine($"CONNECTION status={device.ConnectionStatus}");
var servicesResult = await device.GetGattServicesForUuidAsync(hidServiceUuid, BluetoothCacheMode.Cached);
if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
{
    Console.WriteLine($"CACHED_HID_SERVICE status={servicesResult.Status}; retrying uncached...");
    servicesResult = await device.GetGattServicesForUuidAsync(hidServiceUuid, BluetoothCacheMode.Uncached);
}

if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
{
    Console.Error.WriteLine($"HID service unavailable: {servicesResult.Status}");
    return 3;
}

var subscriptions = new List<GattCharacteristic>();
foreach (var service in servicesResult.Services)
{
    Console.WriteLine($"SERVICE {service.Uuid}");
    var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
    if (characteristicsResult.Status != GattCommunicationStatus.Success)
    {
        Console.Error.WriteLine($"  CHARACTERISTICS_FAILED {characteristicsResult.Status}");
        continue;
    }

    foreach (var characteristic in characteristicsResult.Characteristics)
    {
        var properties = characteristic.CharacteristicProperties;
        Console.WriteLine($"  CHARACTERISTIC {characteristic.Uuid} properties={properties}");
        var mode = properties.HasFlag(GattCharacteristicProperties.Notify)
            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
            : properties.HasFlag(GattCharacteristicProperties.Indicate)
                ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                : GattClientCharacteristicConfigurationDescriptorValue.None;
        if (mode == GattClientCharacteristicConfigurationDescriptorValue.None) continue;

        characteristic.ValueChanged += (_, args) =>
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var bytes = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(bytes);
            Console.WriteLine($"REPORT characteristic={characteristic.Uuid} len={bytes.Length} hex={string.Join(' ', bytes.Select(static b => b.ToString("X2")))}");
        };

        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(mode);
        Console.WriteLine($"    SUBSCRIBE {mode} status={status}");
        if (status == GattCommunicationStatus.Success) subscriptions.Add(characteristic);
    }
}

if (subscriptions.Count == 0)
{
    Console.Error.WriteLine("No report characteristic could be subscribed.");
    return 4;
}

Console.WriteLine("GATT_PROBE_READY; press M5 buttons, Ctrl+C to stop.");
await Task.Delay(Timeout.InfiniteTimeSpan);
return 0;

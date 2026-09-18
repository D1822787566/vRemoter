[CmdletBinding()]
param(
    [string]$Name = 'M5 keyboard',
    [string]$VendorId = '1D5A',
    [string]$ProductId = 'C081'
)

$ErrorActionPreference = 'Stop'

Write-Host "Searching Windows devices..." -ForegroundColor Cyan

$devices = Get-PnpDevice -PresentOnly | Where-Object {
    $instance = [string]$_.InstanceId
    $friendly = [string]$_.FriendlyName
    ($friendly -like "*$Name*") -or
    ($instance -match "VID_$VendorId.*PID_$ProductId") -or
    ($instance -match "VID&$VendorId.*PID&$ProductId")
}

if (-not $devices) {
    Write-Error "Device not found. Make sure '$Name' is paired and connected."
}

foreach ($device in $devices) {
    $instanceId = [string]$device.InstanceId
    $properties = @{}
    try {
        foreach ($property in (Get-PnpDeviceProperty -InstanceId $instanceId -ErrorAction Stop)) {
            $properties[$property.KeyName] = $property.Data
        }
    } catch {
        Write-Warning "Could not read properties for $instanceId : $($_.Exception.Message)"
    }

    [PSCustomObject]@{
        FriendlyName = $device.FriendlyName
        Status = $device.Status
        Class = $device.Class
        InstanceId = $instanceId
        HardwareIds = $properties['DEVPKEY_Device_HardwareIds']
        Manufacturer = $properties['DEVPKEY_Device_Manufacturer']
        DeviceDescription = $properties['DEVPKEY_Device_BusReportedDeviceDesc']
        IsPresent = $properties['DEVPKEY_Device_IsPresent']
    } | Format-List
}

Write-Host "Identification complete. Raw report capture will be added in the next probe revision." -ForegroundColor Green

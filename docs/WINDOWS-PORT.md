# Windows 移植路线

## 当前结论

仓库当前是 macOS-only Swift 应用。M5 keyboard 已确认使用 X6 同一组硬件标识：

- VID: `0x1D5A`
- PID: `0xC081`
- 类型：Bluetooth LE HID

因此 Windows 移植应先复用现有的 HID 报告解析和按键语义，再替换平台输入输出层。

## 第一阶段：HID 观察器

目标是只读捕获设备报告，不拦截系统输入：

1. 通过 Windows HID/WinRT API 枚举 VID `1D5A`、PID `C081`。
2. 打开设备的输入报告流，记录 report ID、长度和十六进制字节。
3. 按下遥控器上的方向、确认、返回、音量、语音和鼠标模式键，保存原始样本。
4. 将样本与 `Tools/hid-report-probe.swift` 及 `Sources/vRemote/X6HIDBridge.swift` 的报告分类进行对照。

观察器必须保持非独占打开，避免影响 Windows 默认键盘行为。确认报告格式一致后，再实现按键拦截和 `SendInput` 注入。

### 当前设备实测结果

在 Windows 上，`M5 keyboard` 的设备节点显示为 Bluetooth LE GATT 服务（包含 HID Service `0x1812`），但没有暴露出可由 `Windows.Devices.HumanInterfaceDevice.HidDevice.FromIdAsync` 打开的 HID interface。WinRT HID 探针可以枚举到这些 GATT 节点，却无法打开输入报告流。

因此 Windows 版的下一条验证路径是 **Raw Input**：让 Windows HID 输入栈继续接收设备输入，通过 `RegisterRawInputDevices` / `WM_INPUT` 获取键盘设备句柄和按键扫描码，再按设备句柄做映射。这样也更适合最终实现“只拦截 M5、普通键盘不受影响”。

如果 Raw Input 无法区分设备，再退回 BLE GATT 原生方案，直接订阅 HID Service 的 Report characteristic；这条路径需要处理 GATT 权限、报告描述符和通知订阅，复杂度更高。

仓库已加入一个基于 Windows HID API 的 .NET 探针：

```powershell
cd Tools\windows-hid-probe
dotnet run
```

它会监听 `VID=1D5A`、`PID=C081` 的输入报告，并输出 `report ID`、长度和十六进制内容。当前仓库环境没有 .NET SDK，因此尚未在此处编译；请在 Windows 开发机安装 .NET 8 SDK 后运行。

## 平台替换边界

| macOS | Windows 方向 |
| --- | --- |
| `IOKit.hid` / `IOHIDManager` | WinRT `Windows.Devices.HumanInterfaceDevice` 或 Raw Input |
| `CGEvent` | `SendInput` |
| `CoreBluetooth` | WinRT Bluetooth LE / HID |
| CoreAudio + BlackHole 驱动 | WASAPI + Windows 虚拟音频设备 |
| AppKit / SwiftUI | Windows UI（待定） |

音频链路暂不与 HID 观察器耦合。先完成设备识别和按键报告确认，避免在协议尚未验证时引入虚拟声卡和权限问题。

## 验收标准

- 能识别已配对的 `M5 keyboard`，并显示 VID/PID。
- 每次按键都能输出稳定、可重复的原始 HID 报告。
- 观察器退出后，Windows 默认键盘输入仍然正常。
- 生成一份可提交的按键样本文件，作为 Windows 解析器的回归 fixture。

## 当前已验证结果（2026-09-18）

### 连接方式

实际使用的是 **2.4G USB 接收器**，不是 BLE 连接。Windows 设备信息为：

- 接收器：VID `0x3554` / PID `0xFC03`
- Windows HID 输入接口：VID `0x1915` / PID `0x1025` / `MI_02`

### 已识别按键

| Scan Code | Virtual Key | 当前语义 |
| ---: | ---: | --- |
| `0x4B` | `0x25` | 左 |
| `0x4D` | `0x27` | 右 |
| `0x50` | `0x28` | 下 |
| `0x48` | `0x26` | 上 |
| `0x1C` | `0x0D` | 确认 / Enter |
| `0x2E` | `0x43` | 功能键 C |
| `0x20` | `0x44` | 功能键 D |

`0xAC` 是 Windows `VK_BROWSER_HOME`，`0xAD` 是静音键。两者经过 Windows 系统媒体键层时没有可靠的设备路径。

### 已实现并验证

- `Tools/windows-raw-input-probe/`：Raw Input 监听器，已能识别 M5 的设备路径和普通按键。
- `VK_BROWSER_HOME (0xAC)` 单击发送 `Ctrl+C`。
- `0xAC` 在 400ms 内双击发送 `Ctrl+V`。
- 通过 `WH_KEYBOARD_LL` 拦截 `0xAC`，已验证不会再打开浏览器主页。
- `Tools/windows-hid-probe/`：Windows HID 接口探针，记录了 2.4G 接收器的复合 HID 接口。
- `Tools/windows-ble-gatt-probe/`：BLE 研究探针；仅用于确认当前设备不是实际使用的 BLE 连接。
- `Tools/windows-hid-probe.ps1`：Windows PnP 设备识别脚本。

### 当前限制

- 媒体键事件（如 `0xAC`、`0xAD`）缺少设备路径，因此系统级拦截会影响其他产生同类事件的设备。
- 当前按键动作仍写在探针中，尚未拆成正式 Windows 应用和可配置映射文件。

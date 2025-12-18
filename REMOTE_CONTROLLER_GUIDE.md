# ESP32 Remote HID Controller Setup Guide

This guide explains how to use the .NET WPF Remote HID Controller with your ESP32-S3 HID device.

## Overview

The solution consists of two parts:
1. **ESP32 Device** (`km-usb-hid-device.ino`): Acts as a USB HID device with WebSocket API
2. **Windows Controller** (`RemoteHIDController/`): WPF app that sends mouse/keyboard input via WebSocket

## Quick Start

### 1. Flash ESP32 Device
- Upload `km-usb-hid-device.ino` to your ESP32-S3 board
- Note the IP address shown in the serial monitor (default: 192.168.1.177)

### 2. Run the Controller App
```bash
cd RemoteHIDController
dotnet run
```

### 3. Connect and Control
1. Enter your ESP32's IP address in the connection dialog
2. Click "Connect"
3. Click on the capture area to start sending input
4. Press **ESC** to stop capturing

## How It Works

```
[Your PC] → [WPF App] → WebSocket → [ESP32] → USB HID → [Target Computer]
            Mouse/KB    JSON msgs    W5500     USB-C      Receives input
```

The WPF app captures your mouse and keyboard, converts them to HID codes, and sends JSON messages to the ESP32 via WebSocket. The ESP32 forwards these as USB HID reports to the connected computer.

## WebSocket Message Format

### Mouse Events
```json
{"t":"m", "x":10, "y":-5, "b":1, "w":0}
```
- `x`, `y`: Relative movement (-127 to 127)
- `b`: Buttons (1=left, 2=right, 4=middle)
- `w`: Wheel (-127 to 127)

### Keyboard Events
```json
{"t":"k", "m":2, "k":[4,5]}
```
- `m`: Modifiers (1=ctrl, 2=shift, 4=alt, 8=win)
- `k`: USB HID keycodes (up to 6 keys)

## Features

✅ Real-time mouse movement tracking
✅ Mouse button support (left, right, middle)
✅ Mouse wheel scrolling
✅ Full keyboard support with modifiers
✅ USB HID compliant keycodes
✅ Easy capture on/off with ESC key
✅ Visual feedback and status display

## Building the Controller

**Requirements:**
- .NET 8.0 SDK
- Windows OS

**Build:**
```bash
cd RemoteHIDController
dotnet build
```

**Run:**
```bash
dotnet run
```

Or use the compiled executable:
```
RemoteHIDController\bin\Debug\net8.0-windows\RemoteHIDController.exe
```

## Troubleshooting

### Cannot connect to ESP32
- Verify ESP32 IP address in serial monitor
- Ensure both devices are on same network
- Check firewall settings

### Input not captured
- Click on the capture area to activate
- Verify "Capturing..." message appears
- Check WebSocket connection status

### Keys not working
- Some system key combinations may be blocked
- Press ESC and retry if capture seems stuck
- Check ESP32 serial output for received messages

## Project Files

```
km-usb-hid-device/
├── km-usb-hid-device.ino          # ESP32 Arduino sketch
└── RemoteHIDController/            # .NET WPF application
    ├── MainWindow.xaml/cs          # Main capture window
    ├── ConnectWindow.xaml/cs       # Connection dialog
    ├── WebSocketHIDClient.cs       # WebSocket client
    ├── HIDKeyMapper.cs             # Key to HID mapping
    └── README.md                   # Detailed documentation
```

## Next Steps

1. Test the basic mouse and keyboard capture
2. Verify ESP32 receives WebSocket messages (check serial monitor)
3. Implement the USB HID report sending in the ESP32 sketch
4. Connect ESP32 to target computer via USB-C

## Notes

- The ESP32 sketch currently logs received messages but needs USB HID implementation
- Look for `// USB HID mouse report here` and `// USB HID keyboard report here` comments in the .ino file
- The controller app is ready to use immediately for testing the WebSocket communication

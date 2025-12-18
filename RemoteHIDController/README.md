# Remote HID Controller

A .NET 8 WPF application that captures mouse and keyboard input and sends it to an ESP32-S3 device via WebSocket for HID control.

## Features

- **WebSocket Connection**: Connects to ESP32 device via WebSocket API
- **Mouse Capture**: Captures mouse movements, button clicks, and scroll wheel
- **Keyboard Capture**: Captures keyboard input with proper HID keycodes and modifiers
- **Easy Control**: Click to start capturing, press ESC to stop

## Usage

1. **Launch the Application**
   - Run the application: `dotnet run` or execute the built executable
   
2. **Connect to ESP32**
   - Enter the IP address of your ESP32 device (default: 192.168.1.177)
   - Click "Connect"

3. **Capture Input**
   - Click on the capture area to start capturing mouse and keyboard
   - The cursor will disappear and all input will be sent to the ESP32
   - Press **ESC** to stop capturing and regain control

4. **Disconnect**
   - Click the "Disconnect" button or close the window to end the session

## Message Format

The application sends JSON messages to the ESP32 WebSocket endpoint (`ws://<ip-address>/ws`):

### Mouse Message
```json
{
  "t": "m",
  "x": 10,
  "y": -5,
  "b": 0,
  "w": 0
}
```
- `t`: Type (mouse)
- `x`: Relative X movement (-127 to 127)
- `y`: Relative Y movement (-127 to 127)
- `b`: Button state bitmask (1=left, 2=right, 4=middle)
- `w`: Wheel scroll (-127 to 127, positive=up)

### Keyboard Message
```json
{
  "t": "k",
  "m": 0,
  "k": [4, 5]
}
```
- `t`: Type (keyboard)
- `m`: Modifier bitmask (1=ctrl, 2=shift, 4=alt, 8=gui/win)
- `k`: Array of USB HID keycodes (max 6)

## Building

```bash
cd RemoteHIDController
dotnet build
```

## Running

```bash
cd RemoteHIDController
dotnet run
```

Or run the executable from:
```
bin\Debug\net8.0-windows\RemoteHIDController.exe
```

## Requirements

- .NET 8.0 SDK or runtime
- Windows OS (WPF application)
- ESP32 device with WebSocket server running

## Project Structure

- `MainWindow.xaml/cs`: Main capture window
- `ConnectWindow.xaml/cs`: Connection dialog
- `WebSocketHIDClient.cs`: WebSocket communication handler
- `HIDKeyMapper.cs`: Keyboard to USB HID keycode mapping

## Notes

- The application captures raw input only when the capture area is clicked
- Press ESC at any time to release capture and return control to your PC
- Mouse movements are sent as relative deltas, not absolute positions
- Maximum of 6 simultaneous key presses (USB HID limitation)

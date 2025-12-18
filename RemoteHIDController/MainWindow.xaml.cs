using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace RemoteHIDController
{
    public partial class MainWindow : Window
    {
        private WebSocketHIDClient? _hidClient;
        private bool _isCapturing = false;
        private Point _lastMousePosition;
        private byte _currentMouseButtons = 0;
        private readonly HashSet<Key> _pressedKeys = new();
        private bool _ignoringMouseMove = false;

        // Low-level keyboard hook
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public MainWindow()
        {
            InitializeComponent();
            _proc = HookCallback;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Handle window-level keyboard events to catch system keys
            PreviewKeyDown += Window_PreviewKeyDown;
            PreviewKeyUp += Window_PreviewKeyUp;

            // Install low-level keyboard hook
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null)
                {
                    _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc!, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            var connectWindow = new ConnectWindow();
            var result = connectWindow.ShowDialog();

            if (result == true && connectWindow.Connected)
            {
                _hidClient = new WebSocketHIDClient();
                try
                {
                    await _hidClient.ConnectAsync(connectWindow.IpAddress);
                    StatusTextBlock.Text = $"Connected to {connectWindow.IpAddress}";
                    DisconnectButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
            else
            {
                Close();
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isCapturing)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                // Block Windows keys from reaching the shell when capturing
                if (vkCode == 0x5B || vkCode == 0x5C) // VK_LWIN or VK_RWIN
                {
                    // Manually add to pressed keys for forwarding
                    var msg = (int)wParam;
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        var key = (vkCode == 0x5B) ? Key.LWin : Key.RWin;
                        if (!_pressedKeys.Contains(key))
                        {
                            _pressedKeys.Add(key);
                            System.Diagnostics.Debug.WriteLine($"Key added (hook): {key}");
                            SendKeyboardStateSync();
                        }
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        var key = (vkCode == 0x5B) ? Key.LWin : Key.RWin;
                        if (_pressedKeys.Contains(key))
                        {
                            _pressedKeys.Remove(key);
                            System.Diagnostics.Debug.WriteLine($"Key removed (hook): {key}");
                            SendKeyboardStateSync();
                        }
                    }
                    
                    return (IntPtr)1; // Block the key from reaching Windows shell
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isCapturing)
            {
                e.Handled = true;
                
                if (e.Key == Key.Escape)
                {
                    StopCapture();
                    return;
                }

                var key = e.Key == Key.System ? e.SystemKey : e.Key;

                if (!e.IsRepeat && !_pressedKeys.Contains(key))
                {
                    _pressedKeys.Add(key);
                    System.Diagnostics.Debug.WriteLine($"Key added: {key}");
                    SendKeyboardStateSync();
                }
            }
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (_isCapturing)
            {
                e.Handled = true;

                var key = e.Key == Key.System ? e.SystemKey : e.Key;

                if (_pressedKeys.Contains(key))
                {
                    _pressedKeys.Remove(key);
                    System.Diagnostics.Debug.WriteLine($"Key removed: {key}");
                    SendKeyboardStateSync();
                }
            }
        }

        private async void CaptureArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isCapturing)
            {
                _isCapturing = true;
                CaptureArea.CaptureMouse();
                CaptureArea.Focus();
                CenterMouseInWindow();
                CaptureTextBlock.Text = "Capturing... (Press ESC to stop)";
                CaptureArea.Cursor = Cursors.None;
            }

            UpdateMouseButtons(e);
            
            // Send button state immediately
            if (_hidClient != null)
            {
                await _hidClient.SendMouseAsync(0, 0, _currentMouseButtons, 0);
            }
        }

        private async void CaptureArea_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isCapturing) return;

            UpdateMouseButtons(e);
            
            if (_hidClient != null)
            {
                await _hidClient.SendMouseAsync(0, 0, _currentMouseButtons, 0);
            }
        }

        private async void CaptureArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isCapturing || _ignoringMouseMove) return;

            var currentPosition = e.GetPosition(CaptureArea);
            var deltaX = (int)(currentPosition.X - _lastMousePosition.X);
            var deltaY = (int)(currentPosition.Y - _lastMousePosition.Y);

            if (deltaX != 0 || deltaY != 0)
            {
                if (_hidClient != null)
                {
                    await _hidClient.SendMouseAsync(deltaX, deltaY, _currentMouseButtons, 0);
                }
                
                // Keep mouse centered to prevent escape
                CenterMouseInWindow();
            }
        }

        private void CenterMouseInWindow()
        {
            _ignoringMouseMove = true;
            
            var centerPoint = CaptureArea.PointToScreen(new Point(
                CaptureArea.ActualWidth / 2,
                CaptureArea.ActualHeight / 2));
            
            SetCursorPos((int)centerPoint.X, (int)centerPoint.Y);
            
            // Update last position to center
            _lastMousePosition = new Point(
                CaptureArea.ActualWidth / 2,
                CaptureArea.ActualHeight / 2);

            // Use Dispatcher to reset flag after cursor has been repositioned
            Dispatcher.InvokeAsync(() => { _ignoringMouseMove = false; }, 
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private async void CaptureArea_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isCapturing) return;

            var wheel = e.Delta / 120; // Standard wheel delta is 120 per notch
            
            if (_hidClient != null)
            {
                await _hidClient.SendMouseAsync(0, 0, _currentMouseButtons, wheel);
            }
        }

        private void SendKeyboardStateSync()
        {
            if (_hidClient == null) return;

            bool ctrl = _pressedKeys.Contains(Key.LeftCtrl) || _pressedKeys.Contains(Key.RightCtrl);
            bool shift = _pressedKeys.Contains(Key.LeftShift) || _pressedKeys.Contains(Key.RightShift);
            bool alt = _pressedKeys.Contains(Key.LeftAlt) || _pressedKeys.Contains(Key.RightAlt);
            bool win = _pressedKeys.Contains(Key.LWin) || _pressedKeys.Contains(Key.RWin);

            byte modifiers = HIDKeyMapper.GetModifiers(ctrl, shift, alt, win);

            var keycodes = _pressedKeys
                .Where(k => k != Key.LeftCtrl && k != Key.RightCtrl &&
                           k != Key.LeftShift && k != Key.RightShift &&
                           k != Key.LeftAlt && k != Key.RightAlt &&
                           k != Key.LWin && k != Key.RWin)
                .Select(k => HIDKeyMapper.GetHIDKeycode(k))
                .Where(code => code != 0)
                .Take(6)
                .ToArray();

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Pressed keys: {string.Join(", ", _pressedKeys)}");
            System.Diagnostics.Debug.WriteLine($"Sending: mod={modifiers:X2} keys=[{string.Join(",", keycodes)}]");

            // Send asynchronously but don't await (fire and forget)
            _ = _hidClient.SendKeyboardAsync(modifiers, keycodes);
        }

        private void UpdateMouseButtons(MouseButtonEventArgs e)
        {
            _currentMouseButtons = 0;

            if (e.LeftButton == MouseButtonState.Pressed)
                _currentMouseButtons |= 0x01;
            if (e.RightButton == MouseButtonState.Pressed)
                _currentMouseButtons |= 0x02;
            if (e.MiddleButton == MouseButtonState.Pressed)
                _currentMouseButtons |= 0x04;
        }

        private async void StopCapture()
        {
            if (!_isCapturing) return;

            _isCapturing = false;
            CaptureArea.ReleaseMouseCapture();
            CaptureTextBlock.Text = "Click here to start capturing mouse and keyboard\n\nPress ESC to stop capturing";
            CaptureArea.Cursor = Cursors.Cross;

            // Release all keys
            _pressedKeys.Clear();
            Debug.WriteLine("All keys released");
            _currentMouseButtons = 0;

            if (_hidClient != null)
            {
                await _hidClient.SendKeyboardAsync(0, Array.Empty<byte>());
                await _hidClient.SendMouseAsync(0, 0, 0, 0);
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hidClient != null)
            {
                await _hidClient.DisconnectAsync();
                _hidClient.Dispose();
                _hidClient = null;
            }
            Close();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isCapturing)
            {
                StopCapture();
            }

            // Unhook keyboard hook
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            if (_hidClient != null)
            {
                await _hidClient.DisconnectAsync();
                _hidClient.Dispose();
            }
        }
    }
}
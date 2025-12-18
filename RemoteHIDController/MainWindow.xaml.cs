using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RemoteHIDController
{
    public partial class MainWindow : Window
    {
        private WebSocketHIDClient? _hidClient;
        private bool _isCapturing = false;
        private System.Windows.Point _lastMousePosition;
        private byte _currentMouseButtons = 0;
        private readonly HashSet<Key> _pressedKeys = new();
        private bool _ignoringMouseMove = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Handle window-level keyboard events to catch system keys
            PreviewKeyDown += Window_PreviewKeyDown;
            PreviewKeyUp += Window_PreviewKeyUp;

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
                    _ = SendKeyboardState();
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
                    _ = SendKeyboardState();
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
            
            var centerPoint = CaptureArea.PointToScreen(new System.Windows.Point(
                CaptureArea.ActualWidth / 2,
                CaptureArea.ActualHeight / 2));
            
            SetCursorPos((int)centerPoint.X, (int)centerPoint.Y);
            
            // Update last position to center
            _lastMousePosition = new System.Windows.Point(
                CaptureArea.ActualWidth / 2,
                CaptureArea.ActualHeight / 2);

            // Use Dispatcher to reset flag after cursor has been repositioned
            Dispatcher.InvokeAsync(() => { _ignoringMouseMove = false; }, 
                System.Windows.Threading.DispatcherPriority.Input);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        private async void CaptureArea_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isCapturing) return;

            var wheel = e.Delta / 120; // Standard wheel delta is 120 per notch
            
            if (_hidClient != null)
            {
                await _hidClient.SendMouseAsync(0, 0, _currentMouseButtons, wheel);
            }
        }

        private async System.Threading.Tasks.Task SendKeyboardState()
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

            await _hidClient.SendKeyboardAsync(modifiers, keycodes);
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

            if (_hidClient != null)
            {
                await _hidClient.DisconnectAsync();
                _hidClient.Dispose();
            }
        }
    }
}
using System;
using System.Windows;

namespace RemoteHIDController
{
    public partial class ConnectWindow : Window
    {
        public string IpAddress { get; private set; } = string.Empty;
        public bool Connected { get; private set; }

        public ConnectWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var ipAddress = IpAddressTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                StatusTextBlock.Text = "Please enter an IP address.";
                return;
            }

            ConnectButton.IsEnabled = false;
            StatusTextBlock.Text = "Connecting...";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;

            try
            {
                // Test connection
                var client = new WebSocketHIDClient();
                await client.ConnectAsync(ipAddress);

                if (client.IsConnected)
                {
                    await client.DisconnectAsync();
                    client.Dispose();

                    IpAddress = ipAddress;
                    Connected = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    StatusTextBlock.Text = "Failed to connect to device.";
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    ConnectButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Connection error: {ex.Message}";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                ConnectButton.IsEnabled = true;
            }
        }
    }
}

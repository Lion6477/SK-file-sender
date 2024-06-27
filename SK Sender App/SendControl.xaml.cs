using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Win32;

namespace SK_Sender_App
{
    public partial class SendControl : UserControl
    {
        const int Port = 12345;
        CancellationTokenSource cts = new CancellationTokenSource();
        ObservableCollection<double> speedData = new ObservableCollection<double>();
        Stopwatch stopwatch = new Stopwatch();
        string selectedFilePath;

        public SendControl()
        {
            InitializeComponent();

            speedChartSend.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = speedData,
                    Fill = null
                }
            };

            speedChartSend.XAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("F0"),
                    Name = "Time (s)"
                }
            };

            speedChartSend.YAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("F2"),
                    Name = "Speed (Mbps)"
                }
            };
        }

        private void btnBackSend_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            ((MainWindow)Application.Current.MainWindow).ContentArea.Content = null;
        }

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select file to send";
            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
            }
        }

        private async void btnSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath) || string.IsNullOrEmpty(txtServerIp.Text))
            {
                MessageBox.Show("Please select a file and enter the server IP address.");
                return;
            }

            string serverIp = txtServerIp.Text; // Сохраняем значение IP в локальную переменную

            await Task.Run(() => StartClient(serverIp, selectedFilePath, cts.Token));
        }

        private void StartClient(string serverIp, string filePath, CancellationToken token)
        {
            try
            {
                TcpClient client = new TcpClient(serverIp, Port);
                Dispatcher.Invoke(() => txtStatusSend.Text = $"Connected to server: {serverIp}");
                NetworkStream ns = client.GetStream();

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    long fileSize = fs.Length;
                    Dispatcher.Invoke(() => progressBarSend.Maximum = fileSize);

                    using (BinaryWriter bw = new BinaryWriter(ns))
                    {
                        // Send file size first
                        bw.Write(fileSize);
                        bw.Flush();

                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        long totalBytesSent = 0;
                        stopwatch.Start();

                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ns.Write(buffer, 0, bytesRead);
                            totalBytesSent += bytesRead;

                            // Update progress bar
                            Dispatcher.Invoke(() => progressBarSend.Value = totalBytesSent);

                            // Calculate and update speed
                            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            double speed = (totalBytesSent * 8) / elapsedSeconds / 1_000_000; // Speed in Mbps
                            Dispatcher.Invoke(() => UpdateSpeedData(speed));

                            if (token.IsCancellationRequested)
                            {
                                break;
                            }
                        }

                        stopwatch.Stop();
                    }
                }

                Dispatcher.Invoke(() => txtStatusSend.Text = "File sent successfully.");
                client.Close();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => txtStatusSend.Text = $"Error: {ex.Message}");
            }
        }

        private void UpdateSpeedData(double speed)
        {
            speedData.Add(speed);
            if (speedData.Count > 60) // Limiting the number of data points to the last 60 seconds
            {
                speedData.RemoveAt(0);
            }
        }
    }
}

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
            Dispatcher.Invoke(() => txtStatusSend.Text = "Input ip");
        }

        private void btnBackSend_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            ((MainWindow)Application.Current.MainWindow).ContentArea.Content = null;
        }

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Выберите файл для отправки";
            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
            }
        }

        private async void btnSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Выберите файл для отправки.");
                return;
            }

            string serverIp = txtServerIp.Text;
            if (string.IsNullOrEmpty(serverIp))
            {
                MessageBox.Show("Введите IP сервера.");
                return;
            }

            await Task.Run(() => StartClient(serverIp, selectedFilePath));
        }

        private void StartClient(string serverIp, string filePath)
        {
            TcpClient client = new TcpClient(serverIp, Port);
            NetworkStream ns = client.GetStream();
            Dispatcher.Invoke(() => txtStatusSend.Text = $"Connected to server: {serverIp}");

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
                long fileSize = new FileInfo(filePath).Length;
                long totalBytesSent = 0;
                stopwatch.Start();

                while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ns.Write(buffer, 0, bytesRead);
                    totalBytesSent += bytesRead;

                    // Update progress bar
                    int progress = (int)((totalBytesSent * 100) / fileSize);
                    Dispatcher.Invoke(() => progressBarSend.Value = progress);

                    // Calculate and update speed
                    double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    double speed = (totalBytesSent * 8) / elapsedSeconds / 1_000_000; // Speed in Mbps
                    Dispatcher.Invoke(() => UpdateSpeedData(speed));
                }

                stopwatch.Stop();
            }

            Dispatcher.Invoke(() => txtStatusSend.Text = "File sent.");
            client.Close();
        }

        private void UpdateSpeedData(double speed)
        {
            speedData.Add(speed);
            if (speedData.Count > 100) // Limiting the number of data points
            {
                speedData.RemoveAt(0);
            }
        }
    }
}

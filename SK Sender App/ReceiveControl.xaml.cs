using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace SK_Sender_App
{
    public partial class ReceiveControl : UserControl
    {
        const int Port = 12345;
        CancellationTokenSource cts = new CancellationTokenSource();
        ObservableCollection<double> speedData = new ObservableCollection<double>();
        Stopwatch stopwatch = new Stopwatch();
        long fileSize;

        public ReceiveControl()
        {
            InitializeComponent();
            Task.Run(() => StartServer(cts.Token));

            speedChartReceive.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = speedData,
                    Fill = null
                }
            };

            speedChartReceive.XAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("F0"),
                    Name = "Time (s)"
                }
            };

            speedChartReceive.YAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("F2"),
                    Name = "Speed (Mbps)"
                }
            };
        }

        private void btnBackReceive_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            ((MainWindow)Application.Current.MainWindow).ContentArea.Content = null;
        }

        private async void StartServer(CancellationToken token)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Dispatcher.Invoke(() => txtStatusReceive.Text = "Server started, waiting for connection...");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (listener.Pending())
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        Dispatcher.Invoke(() => txtStatusReceive.Text = $"Client connected: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                        NetworkStream ns = client.GetStream();

                        using (BinaryReader br = new BinaryReader(ns))
                        {
                            // Read file size first
                            fileSize = br.ReadInt64();
                            Dispatcher.Invoke(() => progressBarReceive.Maximum = fileSize);

                            using (FileStream fs = new FileStream("received_file", FileMode.Create, FileAccess.Write))
                            {
                                byte[] buffer = new byte[64 * 1024]; // Buffer size
                                int bytesRead;
                                long totalBytesReceived = 0;
                                stopwatch.Start();

                                while ((bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                                {
                                    await fs.WriteAsync(buffer, 0, bytesRead, token);
                                    totalBytesReceived += bytesRead;

                                    // Update progress bar
                                    Dispatcher.Invoke(() => progressBarReceive.Value = totalBytesReceived);

                                    // Calculate and update speed
                                    double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                    double speed = (totalBytesReceived * 8) / elapsedSeconds / 1_000_000; // Speed in Mbps
                                    Dispatcher.Invoke(() => UpdateSpeedData(speed));
                                }

                                stopwatch.Stop();
                            }
                        }

                        Dispatcher.Invoke(() => txtStatusReceive.Text = "File received.");
                        client.Close();
                    }
                    else
                    {
                        await Task.Delay(10); // Chill
                    }
                }
            }
            finally
            {
                listener.Stop();
                Dispatcher.Invoke(() => txtStatusReceive.Text = "Server stopped.");
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

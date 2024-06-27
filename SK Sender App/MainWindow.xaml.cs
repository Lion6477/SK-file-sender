using System.Windows;

namespace SK_Sender_App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnReceive_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Content = new ReceiveControl();
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Content = new SendControl();
        }
    }
}
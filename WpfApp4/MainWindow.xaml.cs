using System.Windows;

namespace WpfApp4
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenDocumentViewer_Click(object? sender, RoutedEventArgs e)
        {
            MyDocumentViewer viewer = new MyDocumentViewer();
            viewer.Owner = this;
            viewer.Show();
        }
    }
}

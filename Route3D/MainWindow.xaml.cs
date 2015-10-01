using System;
using System.ComponentModel;
using System.Dynamic;
using System.Windows;
using System.Windows.Controls;


namespace Route3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SourceInitialized += (s, a) =>
            {
                WindowState = WindowState.Maximized;
            };

            CancelEventHandler eh = (sender, args) =>
            {
                var page = frmMain.Content as Page;

                if (page != null)
                {
                    var vm = page.DataContext as IDisposable;
                    if (vm != null)
                        vm.Dispose();
                }
            };

            Closing += (sender, args) => eh(sender, args);
            frmMain.Navigating += (sender, args) => eh(sender, args);

            frmMain.Navigated += (sender, args) => {
                var page = frmMain.Content as Page;

                if (page != null)
                {
                    DataContext = page.DataContext;
                }
            };
        }
    }
}

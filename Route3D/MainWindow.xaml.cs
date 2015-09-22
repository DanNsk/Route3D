using System;
using System.Dynamic;
using System.Windows;
using System.Windows.Input;
using Route3D.Helpers;

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

            var viewModel = new ObservableExpando(new MainViewModel(new FileDialogService(), viewPort3d));
            viewPort3d.RotateGesture = new MouseGesture(MouseAction.LeftClick);


            viewModel.GetMember += (object inst, GetMemberBinder binder, out object result) =>
            {
                var name = binder.Name;
                result = null;

                if (binder.Name.EndsWith("Command"))
                {
                    name = name.Substring(0, binder.Name.Length - 7);

                    var del = inst.GetFirstDelegateFromName<Action>(name);
                    var can = inst.GetFirstDelegateFromName<Func<bool>>(name + "CanExecute");

                    if (del != null)
                        result = new DelegateCommand(del, can);
                }

                return result != null;
            };


            SourceInitialized += (s, a) =>
            {
                WindowState = WindowState.Maximized;
            };

            Closing += (sender, args) =>
            {
                viewModel.Dispose();
            };

            
            DataContext = viewModel;
   
        }
    }
}

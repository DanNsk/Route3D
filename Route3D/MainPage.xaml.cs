using System;
using System.Dynamic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Route3D.Helpers;
using Route3D.Helpers.WPF;

namespace Route3D
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        public MainPage()
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





            DataContext = viewModel;
        }
    }
}

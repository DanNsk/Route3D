using System;
using System.ComponentModel;
using System.Dynamic;
using System.Runtime.CompilerServices;
using Route3D.Annotations;

namespace Route3D.Helpers
{
    public class ObservableExpando : Expando, INotifyPropertyChanged
    {
        public delegate bool GetMemberHandler(object instance, GetMemberBinder binder, out object result);

        
        public ObservableExpando(object viewModel)
            : base(viewModel)
        {
            var inpc = viewModel as INotifyPropertyChanged;

            if (inpc != null)
            {
                inpc.PropertyChanged += (sender, args) => {
                    OnPropertyChanged(args.PropertyName);
                };
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) 
                handler(this, new PropertyChangedEventArgs(propertyName));
        }


        public event GetMemberHandler GetMember;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var mem = GetMember;

            if (mem != null && mem(Instance, binder, out result))
                return true;

            return base.TryGetMember(binder, out result);
        }
    }
}
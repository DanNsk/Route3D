using System;
using System.Windows.Threading;

namespace Route3D.Helpers.WPF
{
    public class DispatchedObservable : Observable
    {
        private readonly Dispatcher dispatcher;

        public virtual Dispatcher Dispatcher 
        {
            get 
            { 
                return dispatcher;
            }
        }

        public DispatchedObservable(Dispatcher disp)
        {
            this.dispatcher = disp;
        }

        public DispatchedObservable()
            : this(Dispatcher.CurrentDispatcher)
        {
            
        }

        protected void Dispatch(Action action)
        {
            if (Dispatcher == null)
                action();
            else
                Dispatcher.Invoke(action);
        }
    }
}
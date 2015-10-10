using System;
using System.Collections.Generic;

namespace Route3D.Helpers
{
    public class DelegatedComparer<T> : IComparer<T>
    {
        private readonly Comparison<T> comparer;

        public DelegatedComparer(Comparison<T> comparer)
        {
            this.comparer = comparer;
        }

        public static implicit operator DelegatedComparer<T>(Comparison<T> comparer)
        {
            return new DelegatedComparer<T>(comparer);
        }

        public int Compare(T x, T y)
        {
            return comparer(x,y);
        }
    }
}

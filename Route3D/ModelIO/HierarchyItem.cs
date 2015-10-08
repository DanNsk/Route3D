using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Route3D.ModelIO
{
    public enum HierarchyMergeType
    {
        None = 0,
        FirstLast = 1,
        LastFirst = 2,
        InvLastFirst = 3,
        FirstInvLast = 4
    }

    public class HierarchyItem<T> : List<T>
    {
        public static Comparison<T> DefaultComparison
        {
            get { return defaultComparison ?? (defaultComparison = (a, b) => Comparer<T>.Default.Compare(a, b)); }
            set { defaultComparison = value; }
        }

        private readonly IList<HierarchyItem<T>> children;
        private static Comparison<T> defaultComparison;

        public HierarchyItem()
        {
            children = new ObservableCollection<HierarchyItem<T>>();
        }

        public HierarchyItem<T> Parent 
        { 
            get; 
            set; 
        }

        public T FirstItem 
        {
            get 
            { 
                return Count > 0 ? this[0] : default(T); 
            }
        }

        public T LastItem
        {
            get
            {
                return Count > 0 ? this[Count - 1] : default(T);
            }
        }



        public bool IsClosed
        {
            get { return Count > 0 && DefaultComparison(FirstItem, LastItem) == 0; }
        }

        public IList<HierarchyItem<T>> Children
        {
            get { return children; }
        }

        public HierarchyItem<T> CreateChild(IEnumerable<T> items = null) 
        { 
            var res = new HierarchyItem<T>();
            
            if (items != null)
                res.AddRange(items);
            
            res.Parent = this;

            Children.Add(res);

            return res;
        }

        public void RemoveFromParent()
        {
            if (Parent != null)
            {
                Parent.Children.Remove(this);
                Parent = null;
            }
        }

        public HierarchyMergeType CheckMergeTypeTo(HierarchyItem<T> ind2)
        {
            if (ind2 == null || ind2 == this || IsClosed || ind2.IsClosed)
                return HierarchyMergeType.None;

            if (DefaultComparison(LastItem, ind2.FirstItem) == 0)
                return HierarchyMergeType.FirstLast;
            else if (DefaultComparison(FirstItem, ind2.LastItem) == 0)
                return HierarchyMergeType.LastFirst;
            else if (DefaultComparison(FirstItem, ind2.FirstItem) == 0)
                return HierarchyMergeType.InvLastFirst;
            else if (DefaultComparison(LastItem, ind2.LastItem) == 0)
                return HierarchyMergeType.FirstInvLast;

            return HierarchyMergeType.None;

        }

        public HierarchyItem<T> Merge(HierarchyItem<T> ind1, HierarchyItem<T> ind2, HierarchyMergeType mt)
        {
            if (mt == HierarchyMergeType.None || (ind1 == null && ind2 == null))
                return null;

            if (ind1 == null)
            {
                ind2.RemoveFromParent();
                ind2.Parent = this;
                Children.Add(ind2);
                return ind2;
            }
            else if (ind2 == null)
            {
                ind1.RemoveFromParent();
                ind1.Parent = this;
                Children.Add(ind1);
                return ind1;
            }

            var res = CreateChild();


            foreach (var c in ind1.Children)
            {
                c.Parent = res;
                res.Children.Add(c);
            }

            foreach (var c in ind2.Children)
            {
                c.Parent = res;
                res.Children.Add(c);
            }

            if (mt == HierarchyMergeType.FirstInvLast ||  mt == HierarchyMergeType.InvLastFirst)
                ind2.Reverse();

            if (mt == HierarchyMergeType.FirstLast || mt == HierarchyMergeType.FirstInvLast)
            {
                res.AddRange(ind1);
                res.AddRange(ind2);
            }
            else
            {
                res.AddRange(ind2);
                res.AddRange(ind1);
            }

            ind1.RemoveFromParent();
            ind2.RemoveFromParent();

            return res;
        }
    }
}

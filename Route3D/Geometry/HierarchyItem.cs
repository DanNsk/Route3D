using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Route3D.Geometry
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

        private readonly IList<HierarchyItem<T>> children;
        private double epsilon;

        public HierarchyItem()
        {
            children = new ObservableCollection<HierarchyItem<T>>();
            epsilon = 1e-6;
        }

        public HierarchyItem<T> Parent
        {
            get;
            set;
        }

        public double Epsilon
        {
            get
            {
                return Parent == null ? epsilon : Parent.Epsilon;
            }
            set
            {
                if (Parent != null)
                    Parent.Epsilon = value;
                else
                    epsilon = value;

            }
        }

        public int Level
        {
            get
            {
                var p = this;
                var lvl = 0;
                while ((p = p.Parent) != null)
                {
                    if (p.Count > 0)
                        lvl++;
                }
                return lvl;
            }
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
            get { return Count > 0 && EqualItems(FirstItem, LastItem, Epsilon); }
        }

        public IList<HierarchyItem<T>> Children
        {
            get { return children; }
        }


        public void Optimize()
        {
            for (var i = 0; i < Count - 2; i++)
            {
                if (EqualDistances(this[i], this[i + 1], this[i + 2], Epsilon))
                {
                    RemoveAt(i + 1);
                    i--;
                }
            }
        }

        public void MergeLevelCorrectChildren(double higheps)
        {

            var ac = FlattenHierarchy(false);

            

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < ac.Count; i++)
            {
                if (ac[i].Count == 0 || (ac.Count == 2 && EqualItems(ac[i].FirstItem, ac[i].LastItem, Epsilon)))
                {
                    ac[i].RemoveFromParent();
                    ac.RemoveAt(i--);
                }
                else
                {
                    ac[i].MoveToParent(this);
                    ac[i].Optimize();
                }
            }

            ac = Children;


            higheps = Math.Max(Epsilon, higheps);

            for (var eps = Epsilon; eps < higheps; eps *= 1.1)
            {

                var itmso = ac.Where(x => !x.IsClosed).ToList();
                if (itmso.Count == 0)
                    break;

                var dict = itmso.Select(x => Tuple.Create(CreateItemHash(x.FirstItem, eps), x))
                    .Union(itmso.Select(x => Tuple.Create(CreateItemHash(x.LastItem, eps), x)))
                    .GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.Select(y => y.Item2).ToList());

                bool merged;

                do
                {

                    merged = false;

                    var keys = dict.Keys.Where(x => dict[x].Count > 1).ToList();

                    if (keys.Count == 0)
                        break;


                    foreach (var dval in keys.Select(dkey => dict[dkey]))
                    {
                        for (int i = 0; i < dval.Count - 1; i++)
                        {
                            var dvli = dval[i];

                            if (EqualItems(dvli.FirstItem, dvli.LastItem, eps))
                            {
                                dvli.Close();
                                continue;
                            }

                            for (int j = i + 1; j < dval.Count; j++)
                            {
                                var dvlj = dval[j];

                                var mt = dvli.CheckMergeTypeTo(dvlj, eps);

                                if (mt != HierarchyMergeType.None)
                                {
                                    merged = true;

                                    var res = Merge(dvli, dvlj, mt);

                                    foreach (var key in new HashSet<ulong>(new[]
                                    {
                                        CreateItemHash(dvli.FirstItem, eps),
                                        CreateItemHash(dvli.LastItem, eps),
                                        CreateItemHash(dvlj.FirstItem, eps),
                                        CreateItemHash(dvlj.LastItem, eps)
                                    }))
                                    {
                                        dict[key].Remove(dvli);
                                        dict[key].Remove(dvlj);
                                    }

                                    if (!res.IsClosed)
                                    {
                                        dict[CreateItemHash(dvli.FirstItem, eps)].Add(res);
                                        dict[CreateItemHash(dvli.LastItem, eps)].Add(res);
                                    }

                                    i--;
                                    break;
                                }
                            }
                        }
                    }


                } while (merged);
            }


            foreach (var ch in ac.Where(x => !x.IsClosed))
            {
                ch.Close();
            }


            ac = FlattenHierarchy(false);


            var checklist = new HashSet<Tuple<int, int>>();

            var dlist = Enumerable.Range(0, ac.Count).ToDictionary(x => x, x => new HashSet<int>());



            for (var i = 0; i < ac.Count; i++)
            {
                ac[i].Optimize();

                for (var j = 0; j < ac.Count; j++)
                {
                    if (i == j)
                        continue;

                    if (checklist.Contains(Tuple.Create(i, j)))
                        continue;

                    var res = ac[i].CheckIsSubleveld(ac[j]);

                    if (res)
                    {
                        dlist[j].Add(i);

                        var ilist = dlist[j].ToList();

                        for (var k = 0; k < ilist.Count; k++)
                            ilist.AddRange(dlist[k].Where(li => dlist[j].Add(li)));

                        foreach (var k in ilist)
                        {
                            checklist.Add(Tuple.Create(k, j));
                            checklist.Add(Tuple.Create(j, k));
                        }

                    }
                    else
                    {
                        if (checklist.Contains(Tuple.Create(j, i)))
                            continue;

                        res = ac[j].CheckIsSubleveld(ac[i]);

                        if (res)
                        {
                            dlist[i].Add(j);

                            var ilist = dlist[i].ToList();

                            for (var k = 0; k < ilist.Count; k++)
                                ilist.AddRange(dlist[k].Where(li => dlist[i].Add(li)));

                            foreach (var k in ilist)
                            {
                                checklist.Add(Tuple.Create(k, i));
                                checklist.Add(Tuple.Create(i, k));
                            }

                        }
                    }

                }
            }


            foreach (var tl in dlist.Where(x => x.Value.Count > 0).Select(x =>
            {
                var lst = x.Value.ToList();
                lst.Sort((a, b) => Math.Sign(dlist[b].Count - dlist[a].Count));
                return Tuple.Create(x.Key, lst[0]);
            }))
            {
                ac[tl.Item1].MoveToParent(ac[tl.Item2]);
            }

        }



        public HierarchyItem<T> CreateChild(IEnumerable<T> items = null)
        {
            HierarchyItem<T> res = null;

            try
            {
                res = (HierarchyItem<T>)Activator.CreateInstance(GetType());
            }
            catch
            {
                res = new HierarchyItem<T>();
            }

            if (items != null)
                res.AddRange(items);

            res.Parent = this;

            Children.Add(res);

            return res;
        }



        public void RemoveEmptyChildren()
        {
            for (var i = 0; i < Children.Count; i++)
            {
                var ch = Children[i];
                ch.RemoveEmptyChildren();
                if (ch.Count == 0)
                {
                    ch.RemoveFromParent();
                    i--;
                }
            }
        }

        public bool MoveToParent(HierarchyItem<T> parent)
        {
            if (parent == Parent)
                return true;

            var res = RemoveFromParent();

            if (res)
            {
                Parent = parent;
                Parent.Children.Add(this);
            }
            return res;
        }

        public bool RemoveFromParent()
        {
            var res = true;

            if (Parent != null)
            {
                res = Parent.Children.Remove(this);
                Parent = null;
            }

            return res;
        }

        public HierarchyMergeType CheckMergeTypeTo(HierarchyItem<T> ind2, double eps)
        {
            if (ind2 == null || ind2 == this || IsClosed || ind2.IsClosed)
                return HierarchyMergeType.None;

            if (EqualItems(LastItem, ind2.FirstItem, eps))
                return HierarchyMergeType.FirstLast;
            else if (EqualItems(FirstItem, ind2.LastItem, eps))
                return HierarchyMergeType.LastFirst;
            else if (EqualItems(FirstItem, ind2.FirstItem, eps))
                return HierarchyMergeType.InvLastFirst;
            else if (EqualItems(LastItem, ind2.LastItem, eps))
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

            if (mt == HierarchyMergeType.FirstInvLast || mt == HierarchyMergeType.InvLastFirst)
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


        public IList<HierarchyItem<T>> FlattenHierarchy(bool includeThis = true)
        {
            var res = includeThis ? new List<HierarchyItem<T>> { this } : new List<HierarchyItem<T>>(Children);

            for (var i = 0; i < res.Count; i++)
            {
                res.AddRange(res[i].Children);
            }

            return res.Where(x => x.Count > 0).ToList();
        }

        public void Close()
        {
            if (!IsClosed && Count > 1)
            {
                Add(FirstItem);
            }
        }

        protected virtual ulong CreateItemHash(T p, double eps)
        {
            return (ulong)(Math.Abs(p.GetHashCode()));
        }

        protected virtual bool EqualItems(T x, T y, double eps)
        {
            return Comparer<T>.Default.Compare(x, y) == 0;
        }

        protected virtual bool CheckIsSubleveld(HierarchyItem<T> itm)
        {
            return false;
        }

        protected virtual bool EqualDistances(T p1, T p2, T p3, double eps)
        {
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace Route3D.Geometry.D3
{
    public class Point3DPath : HierarchyItem<Point3D>
    {
        public static Point3D Zero = new Point3D(0, 0, 0);

        public Point3DPath()
        {
            
        }

        public Point3DPath(IEnumerable<Point3D> pts, IEnumerable<HierarchyItem<Point3D>> chl)
            : base(pts, chl)
        {
            
        }


        protected override ulong CreateItemHash(Point3D p, double eps)
        {
            return (ulong)(p.DistanceTo(Zero) / eps);
        }
        
        protected override bool EqualItems(Point3D p1, Point3D p2, double eps)
        {
            return p1.DistanceTo(p2) < eps;
        }

        protected override bool EqualDistances(Point3D p1, Point3D p2, Point3D p3, double eps)
        {
            return p1.DistanceTo(p2) + p2.DistanceTo(p3) - p1.DistanceTo(p3) < eps;
        }

        protected override bool CheckIsSubleveld(HierarchyItem<Point3D> itm)
        {
            var xy1 = this.Select(x => new Point(x.X, x.Y));
            var xy2 = itm.Select(x => new Point(x.X, x.Y));

            var val = xy2.IsPolygonInPolygon(xy1);

            if (!val.HasValue || val.Value)
            {
                xy1 = this.Select(x => new Point(x.X, x.Z));
                xy2 = itm.Select(x => new Point(x.X, x.Z));

                var val1 = xy2.IsPolygonInPolygon(xy1);

                if (!val1.HasValue || val1.Value)
                {
                    xy1 = this.Select(x => new Point(x.Y, x.Z));
                    xy2 = itm.Select(x => new Point(x.Y, x.Z));

                    var val2 = xy2.IsPolygonInPolygon(xy1);

                    if (!val2.HasValue || val2.Value)
                    {
                        return val1.HasValue || val2.HasValue || val.HasValue;
                    }

                }
            }

            return false;
        }

        protected override Tuple<Point3D,Point3D> CalcBounds(IEnumerable<Point3D> union)
        {
            double? minx = null;
            double? maxx = null;
            double? miny = null;
            double? maxy = null;
            double? minz = null;
            double? maxz = null;

            foreach (var v in union)
            {
                if (!minx.HasValue || minx > v.X)
                    minx = v.X;
                if (!miny.HasValue || miny > v.Y)
                    miny = v.Y;

                if (!maxx.HasValue || maxx < v.X)
                    maxx = v.X;
                if (!maxy.HasValue || maxy < v.Y)
                    maxy = v.Y;

                if (!maxz.HasValue || maxz < v.Z)
                    maxz = v.Z;
                if (!minz.HasValue || maxz > v.Z)
                    minz = v.Z;
            }

            return !minx.HasValue ? null : Tuple.Create(new Point3D(minx.Value, miny.Value, minz.Value), new Point3D(maxx.Value, maxy.Value, maxz.Value));
        }

        protected override Point3D MoveItemBy(Point3D x, Point3D delta)
        {
            return new Point3D(x.X + delta.X, x.Y + delta.Y, x.Y + delta.Z);
        }
    }
}

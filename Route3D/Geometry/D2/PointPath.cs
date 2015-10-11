using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace Route3D.Geometry.D2
{
    public class PointPath : HierarchyItem<Point>
    {
        public static Point Zero = new Point(0, 0);

        protected override ulong CreateItemHash(Point p, double eps)
        {
            return (ulong)(p.DistanceTo(Zero) / eps);
        }

        protected override bool EqualItems(Point p1, Point p2, double eps)
        {
            return p1.DistanceTo(p2) < eps;
        }

        protected override bool EqualDistances(Point p1, Point p2, Point p3, double eps)
        {
            return p1.DistanceTo(p2) + p2.DistanceTo(p3) - p1.DistanceTo(p3) < eps;
        }

        protected override bool CheckIsSubleveld(HierarchyItem<Point> itm)
        {
            var val = itm.IsPolygonInPolygon(this);

            return val.HasValue && val.Value;
        }

        protected override Point CalcCenter(IEnumerable<Point> union)
        {
            double? minx = null;
            double? maxx = null;
            double? miny = null;
            double? maxy = null;
 
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
            }

            return !minx.HasValue ? new Point(0, 0) : new Point((minx.Value + maxx.Value) / 2, (miny.Value + maxy.Value) / 2);
        }

        protected override Point MoveItemBy(Point x, Point delta)
        {
            return new Point(x.X + delta.X, x.Y + delta.Y);
        }
    }
}

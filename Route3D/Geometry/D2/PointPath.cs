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
    }
}

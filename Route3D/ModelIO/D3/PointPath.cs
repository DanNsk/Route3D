using System;
using System.Windows.Media.Media3D;

namespace Route3D.ModelIO.D3
{
    public class Point3DPath : HierarchyItem<Point3D>
    {
        public static double Epsilon { get; set; }

        static Point3DPath()
        {
            Epsilon = 1e-6;

            DefaultComparison = (point, point1) =>
            {
                var d1 = point.X * point.X + point.Y * point.Y + point.Z * point.Z;
                var d2 = point1.X * point1.X + point1.Y * point1.Y + point1.Z * point1.Z;

                return Math.Abs(d1 - d2) < Epsilon ? 0 : Math.Sign(Math.Abs(d1 - d2));
            };
        }
    }
}

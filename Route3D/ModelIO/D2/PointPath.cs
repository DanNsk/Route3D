using System;
using System.Windows;

namespace Route3D.ModelIO.D2
{
    public class PointPath : HierarchyItem<Point>
    {
        public static double Epsilon { get; set; }
        
        static PointPath()
        {
            Epsilon = 1e-6;

            DefaultComparison = (point, point1) =>
            {
                var d1 = point.X * point.X + point.Y * point.Y;
                var d2 = point1.X * point1.X + point1.Y * point1.Y;

                return Math.Abs(d1 - d2) < Epsilon ? 0 : Math.Sign(Math.Abs(d1 - d2));
            };
        }
    }
}

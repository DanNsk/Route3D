using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Media3D;

namespace Route3D.Geometry.D3
{
    public class GCode3DExporter : IHierarchyItemExporter<Point3D>
    {
        public void Export(string path, HierarchyItem<Point3D> exp)
        {
            var bounds = exp.Bounds;

            var paths = exp.FlattenHierarchy();

            using (var file = File.CreateText(path))
            {
                file.WriteLine("G21");

                double safez = (bounds != null ? Math.Max(bounds.Item2.Z, bounds.Item1.Z) : 0.0) + 10.0;


                var zsp = 70;
                var xysp = 200;


                foreach (var pathp in paths.Where(x => x.Count > 0).Select(x => { x.Close(); return x; }).OrderByDescending(x => x.FirstItem.Z).ThenBy(x => x.Perimeter()))
                {
                    file.WriteLine("G00 Z{0:F2}", safez);
                    var first = pathp[0];
                    file.WriteLine("G00 X{0:F2} Y{1:F2}", first.X, first.Y);

                    file.WriteLine("G01 Z{0:F2} F{1:F2}", first.Z, zsp);

                    for (var i = 1; i <= pathp.Count; i++)
                    {
                        file.WriteLine("G01 X{0:F2} Y{1:F2} F{2:F2}", pathp[i % pathp.Count].X, pathp[i % pathp.Count].Y, xysp);

                        if (pathp[i % pathp.Count] == first)
                            break;
                    }
                }

                file.WriteLine("G00 Z{0:F2}", safez);
            }
        }
    }
}

using System.IO;
using System.Linq;
using System.Windows;

namespace Route3D.Geometry.D2
{
    public class GCodeExporter : IHierarchyItemExporter<Point>
    {
        public void Export(string path, HierarchyItem<Point> exp)
        {
            var paths = exp.FlattenHierarchy();

            using (var file = File.CreateText(path))
            {
                file.WriteLine("G21");

                double safez = 10;

                var zsp = 70;
                var xysp = 200;


                foreach (var pathp in paths.Where(x => x.Count > 0).Select(x => { x.Close(); return x; }).OrderBy(x => x.Perimeter()))
                {
                    file.WriteLine("G00 Z{0:F2}", safez);
                    var first = pathp[0];
                    file.WriteLine("G00 X{0:F2} Y{1:F2}", first.X, first.Y);

                    file.WriteLine("G01 Z0 F{0:F2}", zsp);

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

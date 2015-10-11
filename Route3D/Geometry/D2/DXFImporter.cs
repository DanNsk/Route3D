using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using netDxf;
using netDxf.Units;

namespace Route3D.Geometry.D2
{
    public class DXFImporter
    {
        public PointPath ImportPath(string path)
        {
            var root = new PointPath();

            var doc = DxfDocument.Load(path);

            var lcoef = UnitHelper.ConversionFactor(doc.DrawingVariables.InsUnits, ImageUnits.Millimeters);
            var acoef = Math.PI / 180.0;
            

            const double dist = 0.5;

            foreach (var line in doc.Lines)
            {
                root.CreateChild(new List<Point> { new Point(line.StartPoint.X * lcoef, line.StartPoint.Y * lcoef), new Point(line.EndPoint.X * lcoef, line.EndPoint.Y * lcoef) });
            }

            foreach (var line in doc.Arcs)
            {

                var dang = Math.Abs(line.StartAngle - line.EndAngle) * acoef;

                var seg = Math.Max((int)Math.Ceiling(line.Radius * dang / dist), 1);

                var pl = line.ToPolyline(seg);

                root.CreateChild(pl.PoligonalVertexes(seg, root.Epsilon, root.Epsilon).Select(poligonalVertex => new Point(poligonalVertex.X * lcoef, poligonalVertex.Y * lcoef)));
            }

            foreach (var line in doc.Circles)
            {


                var seg = Math.Max((int)Math.Ceiling(line.Radius * Math.PI / dist), 1);

                var pl = line.ToPolyline(seg);

                root.CreateChild(pl.PoligonalVertexes(seg, root.Epsilon, root.Epsilon).Select(poligonalVertex => new Point(poligonalVertex.X * lcoef, poligonalVertex.Y * lcoef)));
            }

            foreach (var line in doc.Polylines)
            {
                root.CreateChild(line.Vertexes.Select(poligonalVertex => new Point(poligonalVertex.Location.X * lcoef, poligonalVertex.Location.Y * lcoef)));
            }

            foreach (var line in doc.LwPolylines)
            {
                if (line.Vertexes.Count < 2)
                    continue;

                var fdist = new Point(line.Vertexes[0].Location.X, line.Vertexes[0].Location.Y).DistanceTo(new Point(line.Vertexes.Last().Location.X, line.Vertexes.Last().Location.Y));

                root.CreateChild(line.PoligonalVertexes(Math.Max((int)Math.Ceiling(fdist / dist), 2), root.Epsilon, root.Epsilon).Select(poligonalVertex => new Point(poligonalVertex.X * lcoef, poligonalVertex.Y * lcoef)));
            }

            root.MergeLevelCorrectChildren(dist);
           
            return root;
        }
    }
}

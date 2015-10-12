using System;
using System.Collections.Generic;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Objects;
using netDxf.Tables;
using netDxf.Units;
using Point = System.Windows.Point;

namespace Route3D.Geometry.D2
{
    public class DXFImporterExporter : IHierarchyItemImporter<Point>, IHierarchyItemExporter<Point>
    {
        public void Export(string path, HierarchyItem<Point> exp)
        {
            var res = new DxfDocument();

            res.DrawingVariables.AUnits = AngleUnitType.Radians;
            res.DrawingVariables.LUnits = LinearUnitType.Decimal;
            res.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
            res.DrawingVariables.AcadVer = DxfVersion.AutoCad2007;

            exp.MergeLevelCorrectChildren(0.5);

            var hierarchy = exp.FlattenHierarchy();
            var layer = new Layer("default");

            foreach (var hpath in hierarchy.Where(x=>x.Count > 0))
            {
                var poly = new Polyline();

                poly.Vertexes.AddRange(hpath.Select(x => new PolylineVertex(x.X, x.Y, 0.0)));

                if (!hpath.IsClosed)
                    poly.Vertexes.Add(new PolylineVertex(hpath[0].X, hpath[0].Y, 0.0));

                poly.Layer = layer;

                res.AddEntity(poly);
            }

            var b = exp.Bounds;

            if (b != null)
            {
                res.Viewport.ViewCenter = new Vector2((b.Item1.X + b.Item2.X) / 2.0, (b.Item1.Y + b.Item2.Y) / 2.0);
                res.Viewport.ViewHeight = Math.Abs(b.Item1.Y - b.Item2.Y);
            }


            res.Save(path);
        }

        public HierarchyItem<Point> Import(string path)
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

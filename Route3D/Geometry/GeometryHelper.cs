using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Route3D.Helpers;

namespace Route3D.Geometry
{
    public static class GeometryHelper
    {
        public static readonly IList<Color> GoodColors = typeof(Colors).GetProperties(BindingFlags.Static | BindingFlags.Public).Where(p => p.PropertyType == typeof(Color)).Select(p => (Color)p.GetValue(null)).Where(c => !c.Equals(Colors.White) && !c.Equals(Colors.Transparent)).ToList();

        public static Vector GetNormalWith(this Point pt1, Point pt2)
        {
            var dx = (pt2.X - pt1.X);
            var dy = (pt2.Y - pt1.Y);

            if ((Math.Abs(dx) < double.Epsilon) && (Math.Abs(dy) < double.Epsilon))
                return new Vector(0, 0);

            var f = 1.0 / Math.Sqrt(dx * dx + dy * dy);
            dx *= f;
            dy *= f;

            return new Vector(dy, -dx);
        }


        public static List<List<Point3D>> FixBounds(this List<List<Point3D>> paths, Rect3D rect, double extra, out int nonFixed)
        {
            rect = new Rect3D(rect.X - extra, rect.Y - extra, rect.Z - extra, rect.SizeX + extra * 2, rect.SizeY + extra * 2, rect.SizeZ + extra * 2);

            nonFixed = 0;

            foreach (var path in paths)
            {
                nonFixed += path.Count;
                var j = path.Count - 1;
                for (int i = 0; i < path.Count; i++)
                {
                    var p = path[i];

                    if (!p.IsInside(rect, 0.0))
                    {
                        var x = p.X;
                        var y = p.Y;
                        var z = p.Z;


                        if (x < rect.X)
                        {
                            x = rect.X;
                        }
                        else if (x > rect.X + rect.SizeX)
                        {
                            x = rect.X + rect.SizeX;
                        }

                        if (y < rect.Y)
                        {
                            y = rect.Y;
                        }
                        else if (y > rect.Y + rect.SizeY)
                        {
                            y = rect.Y + rect.SizeY;
                        }

                        if (z < rect.Z)
                        {
                            z = rect.Z;
                        }
                        else if (z > rect.Z + rect.SizeZ)
                        {
                            z = rect.Z + rect.SizeZ;
                        }

                        path[i] = new Point3D(x,y,z);


                        nonFixed--;
                    }
                }
                    
            }

            return paths;
        }

        public static bool IsInside(this Point3D point, Rect3D rect, double extra)
        {
            rect = new Rect3D(rect.X - extra, rect.Y - extra, rect.Z - extra, rect.SizeX + extra * 2, rect.SizeY + extra * 2, rect.SizeZ + extra * 2);

            return point.X >= rect.X && point.X <= rect.X + rect.SizeX &&
                   point.Y >= rect.Y && point.Y <= rect.Y + rect.SizeY &&
                   point.Z >= rect.Z && point.Z <= rect.Z + rect.SizeZ;
        }

        public static MeshGeometry3D Slice(this MeshGeometry3D geom, Point3D start, Vector3D step, double? epsilon = null)
        {
            var res = MeshGeometryHelper.Cut(MeshGeometryHelper.Cut(geom, start, step / step.Length), start + step, -step / step.Length);

            if (epsilon.HasValue)
                res.JoinNearIndices(epsilon.Value);

            return res;
        }

        public static Model3DGroup JoinModelsToOne(this Model3DGroup res, Material mat = null, Transform3D transf = null)
        {
            var geometry = new MeshGeometry3D();


            foreach (var mgeom in res.Children.OfType<GeometryModel3D>()
                .Select(geom => geom.Geometry as MeshGeometry3D)
                .Where(mgeom => mgeom != null))
            {
                var cnt = geometry.Positions.Count;

                foreach (var pos in mgeom.Positions)
                {
                    geometry.Positions.Add(pos);
                }

                foreach (var nor in mgeom.Normals)
                {
                    geometry.Normals.Add(nor);
                }

                foreach (var tex in mgeom.TextureCoordinates)
                {
                    geometry.TextureCoordinates.Add(tex);
                }

                foreach (var index in mgeom.TriangleIndices)
                {
                    geometry.TriangleIndices.Add(cnt + index);
                }
            }


            var model3DGroup = new Model3DGroup();

            model3DGroup.Children.Add(new GeometryModel3D { Geometry = geometry, Material = mat, Transform = transf });

            return model3DGroup;
        }

        public static Rect GetBounds(this List<List<Point>> paths)
        {
            int i = 0, cnt = paths.Count;
            while (i < cnt && paths[i].Count == 0) i++;

            if (i == cnt)
                return new Rect(0, 0, 0, 0);

            var l = paths[i][0].X;
            var r = paths[i][0].X;
            var t = paths[i][0].Y;
            var b = paths[i][0].Y;


            for (; i < cnt; i++)
            {
                for (int j = 0; j < paths[i].Count; j++)
                {


                    if (paths[i][j].X < l) l = paths[i][j].X;
                    else if (paths[i][j].X > r) r = paths[i][j].X;
                    if (paths[i][j].Y < t) t = paths[i][j].Y;
                    else if (paths[i][j].Y > b) b = paths[i][j].Y;
                }
            }

            return new Rect(new Point(l, t), new Point(r, b));
        }


        public static List<List<Point>> ChangePointUnits(this List<List<Point3D>> points)
        {
            return points == null ? null : points.Select(x => x.Select(y => new Point(y.X, y.Y)).ToList()).ToList();
        }

        public static List<List<Point3D>> ChangePointUnits(this List<List<Point>> points, double z)
        {
            return points == null ? null : points.Select(x => x.Select(y => new Point3D(y.X, y.Y, z)).ToList()).ToList();
        }


        public static List<List<Point3D>> RemoveSmallPolygons(this List<List<Point3D>> points, double minArea, double minPer, double minSide)
        {
            var areas = points.Select(x => x.Area()).ToList();
            var perimeters = points.Select(x => x.Area()).ToList();

            var minsides = areas.Select((x, i) => {
                var p = perimeters[i];
                var d = p*p - 16 * x;

                if (d < 0)
                {
                    if (p > 0)
                        return x/(p/2);

                    return Double.NaN;
                }
                return (p - Math.Sqrt(d))/4;
            }).ToList();

            // area = a*b => a = area / b ; perimeter = 2a + 2b => perimeter = 2(area / b) + 2b; 2b*b - perimetr*b + 2area=0
            // D = perimetr * perimetr - 4 * 2 * 2 * area

            return points.Where((x, i) => areas[i] > minArea && perimeters[i] > minPer && (!Double.IsNaN(minsides[i]) || minsides[i] > minSide)).ToList();
        }

        public static double DistanceToSquared(this Point p1, Point p2)
        {
            return (p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y);
        }

        public static double DistanceTo(this Point p1, Point p2)
        {
            return Math.Sqrt(DistanceToSquared(p1, p2));
        }


        public static double Area(this List<Point3D> points)
        {

            int nPts = points.Count;

            var a = default(Point3D);

            for (int i = 0; i < nPts; ++i)
            {
                var j = (i + 1) % nPts;
                var c =  Cross(points[i], points[j]);
                a.X += c.X;
                a.Y += c.Y;
                a.Z += c.Z;
            }

            a.X = a.X / 2;
            a.Y = a.Y / 2;
            a.Z = a.Z / 2;

            return a.DistanceTo(default(Point3D));
        }


        public static double Perimeter(this List<Point3D> points)
        {
            int nPts = points.Count;

            var a = 0.0;

            for (int i = 0; i < nPts; ++i)
            {
                var j = (i + 1) % nPts;
                a += points[i].DistanceTo(points[j]);
                
            }

            return a;
        }

        public static Point3D Cross(this Point3D v0, Point3D v1)
        {
            return new Point3D(v0.Y * v1.Z - v0.Z * v1.Y,
                v0.Z * v1.X - v0.X * v1.Z,
                v0.X * v1.Y - v0.Y * v1.X);
        }

        public static bool Orientation(this IEnumerable<Point> pathen)
        {
            return AreaNonAbs(pathen) >= 0;
        }

        public static double AreaNonAbs(this IEnumerable<Point> pathen)
        {
            
            double area = 0;

            var path = pathen.GetEnumerator();

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (path == null || !path.MoveNext())
                return 0;

            var first = path.Current;
            var ip = first;

            bool finished;

            do
            {
                finished = !path.MoveNext();

                var ipNext = (finished ? first : path.Current);

                area += (ip.X + ipNext.X) * (ip.Y - ipNext.Y);

                ip = ipNext;
            } while (!finished);

           
            return -(area/2);
        }

        public static double Area(this IEnumerable<Point> polygon)
        {
            return Math.Abs(AreaNonAbs(polygon));
        }

        public static double Perimeter(this List<Point> polygon)
        {

            double perimeter = 0;

            for (var i = 0; i < polygon.Count; i++)
            {
                var j = (i + 1) % polygon.Count;

                perimeter += polygon[i].DistanceTo(polygon[j]);
            }


            return perimeter;
        }

        public static bool? IsPolygonInPolygon(this IEnumerable<Point> pathin, IEnumerable<Point> pathen)
        {
            return IsPolygonInPolygon(pathin, pathen, false);
        }

        public static bool? IsPolygonInPolygon(this IEnumerable<Point> pathin, IEnumerable<Point> pathen, bool strictCheck)
        {
            bool? res = null;

            var lst = pathen.ToList();

            foreach (var xv in pathin.Select(pt => pt.IsPointInPolygon(lst)).Where(xv => xv.HasValue))
            {
                if (xv.Value)
                {
                    res = true;

                    if (!strictCheck)
                        break;
                }
                else
                {
                    res = false;
                    break;
                }
            }

            return res;
        }

        public static bool? IsPointInPolygon(this Point pt, IEnumerable<Point> pathen)
        {
            //returns 0 if false, +1 if true, -1 if pt ON polygon boundary
            //See "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos
            //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.88.5498&rep=rep1&type=pdf
            
            var path = pathen.GetEnumerator();

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (path == null || !path.MoveNext())
                return false;

            var result = false;


            var ip = path.Current;
            var first = ip;
            bool finished;

            do
            {
                finished = !path.MoveNext();

                var ipNext = (finished ? first : path.Current);
                if (Math.Abs(ipNext.Y - pt.Y) < Double.Epsilon)
                {
                    if ((Math.Abs(ipNext.X - pt.X) < Double.Epsilon) || (Math.Abs(ip.Y - pt.Y) < Double.Epsilon && ((ipNext.X > pt.X) == (ip.X < pt.X)))) 
                        return null;
                }
                if ((ip.Y < pt.Y) != (ipNext.Y < pt.Y))
                {
                    if (ip.X >= pt.X)
                    {
                        if (ipNext.X > pt.X) result = !result;
                        else
                        {
                            var d = (ip.X - pt.X)*(ipNext.Y - pt.Y) -
                                    (ipNext.X - pt.X)*(ip.Y - pt.Y);
                            if (Math.Abs(d) < Double.Epsilon) return null;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = !result;
                        }
                    }
                    else
                    {
                        if (ipNext.X > pt.X)
                        {
                            var d = (ip.X - pt.X)*(ipNext.Y - pt.Y) -
                                    (ipNext.X - pt.X)*(ip.Y - pt.Y);
                            if (Math.Abs(d) < Double.Epsilon) return null;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = !result;
                        }
                    }
                }
                ip = ipNext;
            } while (!finished);
            return result;
        }

        


        public static List<List<Point3D>> ClipPaths(this List<List<Point3D>> p1, List<List<Point3D>> p2, ClipType ct, double z, double eps)
        {
            if (p1 != null && p2 != null)
            {
                var xpaths1 = p1.ChangePointUnits();
                var xpaths2 = p2.ChangePointUnits();

                var clipper = new Clipper();

                clipper.AddPaths(xpaths1, PolyType.Subject);
                clipper.AddPaths(xpaths2, PolyType.Clip);

                var solutions = new List<List<Point>>();

                solutions = !clipper.Execute(ct, solutions) ? xpaths2 : solutions.Where(x => x.Count > 3).ToList();


                return solutions.ChangePointUnits(z);

            }
            else
            {
                var res = p1 ?? p2;

                if (res != null)
                {
                    res = res.ChangePointUnits().ChangePointUnits(z);
                }

                return res;
            }



        }

        public static IList<Tuple<int, int>> FindBottomContours(this MeshGeometry3D segments, double eps)
        {
            if (segments.TriangleIndices.Count == 0)
                return new List<Tuple<int, int>>();

            var i = segments.TriangleIndices.Select(cz => segments.Positions[cz].Z).Min();

            var m = segments.TriangleIndices.GroupByCount(3).Select(z => z.Select(zi => new { ind = zi, p = segments.Positions[zi], hp = Math.Abs(segments.Positions[zi].Z - i) < eps ? 1 : 0 }).ToList()).Where(z => z.Sum(zi => zi.hp) > 1).ToArray();

            long maxind = segments.TriangleIndices.Max();

            var des = new Dictionary<long, Tuple<int, int>>();

            foreach (var ma in m)
            {
                if (ma.Sum(x => x.hp) == 3)
                    continue;

                for (var j = 0; j < 3; j++)
                {
                    var maj = ma[j];
                    var mak = ma[(j + 1) % 3];

                    if (maj.hp > 0 && mak.hp > 0)
                    {
                        var key = maj.ind + mak.ind * maxind;
                        var ikey = mak.ind + maj.ind * maxind;

                        if (!des.ContainsKey(key) && !des.ContainsKey(ikey) && maj.ind != mak.ind)
                        {
                            des.Add(key, Tuple.Create(maj.ind, mak.ind));
                        }
                    }
                }
            }
            return des.Select(x => x.Value).ToList();
        }

        public static List<List<Point3D>> FixPointPaths(this List<List<Point3D>> paths)
        {
            foreach (var path in paths.Where(x => x.Count > 0 && x[0] != x[x.Count - 1]))
            {
                path.Add(path[path.Count - 1]);
                path.Add(path[0]);
            }

            return paths;
        }

        public static List<List<int>> FixMergeIndexPaths(this List<List<int>> paths, IList<Point3D> poss, double eps)
        {
            List<List<int>> pathsnc;

           

            var ieps = 1 / eps;

            while ((pathsnc = paths.Where(x => x.Count > 0 && x[0] != x[x.Count - 1]).ToList()).Count > 0)
            {
                var ia = new Int32Collection(pathsnc.Select(x => new[] { x.First(), x.Last() }).SelectMany(x => x));

                var dem = ia.JoinNearIndices(poss, eps);
                dem = dem.Union(dem.Select(x => new KeyValuePair<int, int>(x.Value, x.Key))).ToDictionary(x => x.Key, x => x.Value);

                if (dem.Count > 0)
                {
                    for (int j = 0; j < pathsnc.Count; j++)
                    {
                        if (pathsnc[j].Count == 0)
                            continue;

                        var f = pathsnc[j][0];
                        var l = pathsnc[j][pathsnc[j].Count - 1];

                        for (int k = 0; k < pathsnc.Count; k++)
                        {
                            if (pathsnc[k].Count == 0)
                                continue;

                            var lk = pathsnc[k][pathsnc[k].Count - 1];
                            var fk = pathsnc[k][0];

                            if (dem.ContainsKey(fk) && (dem[fk] == f || dem[fk] == l))
                            {
                                if (dem[fk] == f)
                                    pathsnc[j].Reverse();

                                pathsnc[j].Add(dem[fk]);
                                pathsnc[j].Add(fk);

                                dem.Remove(dem[fk]);
                                dem.Remove(fk);

                                if (j != k)
                                {
                                    pathsnc[j].AddRange(pathsnc[k]);
                                    pathsnc[k].Clear();
                                }
                                break;
                            }
                            else if (dem.ContainsKey(lk) && (dem[lk] == f || dem[lk] == l))
                            {
                                if (dem[lk] == l)
                                    pathsnc[j].Reverse();

                                pathsnc[k].Add(lk);
                                pathsnc[k].Add(dem[lk]);

                                dem.Remove(dem[lk]);
                                dem.Remove(lk);

                                if (j != k)
                                {
                                    pathsnc[k].AddRange(pathsnc[j]);
                                    pathsnc[j].Clear();
                                }
                                break;
                            }

                            if (dem.Count == 0)
                                break;
                        }

                        if (dem.Count == 0)
                            break;
                    }
                }
                eps = eps * 2;


                if (eps > ieps)
                    break;
            }

            foreach (var path in paths.Where(x => x.Count > 0))
            {
                for (int i = 0; i < path.Count - 3; i+=4)
                {
                    if (path[i + 1] == path[i + 2])
                    {
                        var distf = poss[path[i]].DistanceTo(poss[path[i + 3]]);
                        var dist = poss[path[i]].DistanceTo(poss[path[i + 1]]) + poss[path[i + 2]].DistanceTo(poss[path[i + 3]]) - distf;

                        if (Math.Abs(dist) <= 1/ieps/distf)
                        {
                            path.RemoveRange(i+1, 2);
                            i -= 4;
                        }
                    }
                }
            }


            foreach (var path in paths.Where(x => x.Count > 0 && x[0] != x[x.Count - 1]))
            {
                path.Add(path[path.Count - 1]);
                path.Add(path[0]);
            }

            return paths;
        }


        public static void JoinNearPoints(this List<List<Point3D>>  paths, double eps)
        {
            foreach (var path in paths)
            {
                if (paths.Count > 2)
                {
                    for (int i = 0; i < path.Count; )
                    {
                        for (int j = i + 1; j <= path.Count; j++)
                        {
                            if (path[i].DistanceTo(path[j % path.Count]) > eps)
                            {
                                i = j;
                                break;
                            }


                            path.RemoveAt(j % path.Count);
                            j--;
                        }
                    }

                    for (int i = 0; i < path.Count - 2; i++)
                    {
                        if (Math.Abs(path[i].DistanceTo(path[i + 1]) + path[i + 1].DistanceTo(path[i + 2]) - path[i].DistanceTo(path[i + 2])) < eps)
                        {
                            path.RemoveAt(i + 1);
                            i--;
                        }

                    }
                }

            }
        }


        public static Dictionary<int, int> JoinNearIndices(this MeshGeometry3D geometry, double eps)
        {
            return JoinNearIndices(geometry.TriangleIndices, geometry.Positions, eps);
        }

        public static Dictionary<int, int> JoinNearIndices(this IList<int> indices, IList<Point3D> positions, double eps)
        {
            if (indices.Count == 0)
                return new Dictionary<int, int>();

            var points = indices.Distinct().OrderBy(x=>x).ToList();

            var rnd = new Random();

            var p1 = positions[points[rnd.Next(points.Count - 1)]];
            var p2 = positions[points[rnd.Next(points.Count - 1)]];

            Func<int, long> f1 = a => (long)((positions[a].DistanceTo(p1) - eps) / eps);
            Func<int, long> f2 = a => (long)((positions[a].DistanceTo(p2) - eps) / eps);

            var d1 = points.GroupBy(f1).ToDictionary(x => x.Key, x => x.ToList());
            var d2 = points.GroupBy(f2).ToDictionary(x => x.Key, x => x.ToList());

            var moved = new Dictionary<int, int>();

            var newind = indices.Select(x =>
            {
                if (moved.ContainsKey(x))
                    return moved[x];

                var vf1 = f1(x);
                var vf2 = f2(x);

                for (long i = vf1 - 1; i <= vf1 + 1; i++)
                {
                    for (long j = vf2-1; j <= vf2 + 1; j++)
                    {
                        if (d1.ContainsKey(i) && d2.ContainsKey(j))
                        {
                            foreach (var item in d1[i].Intersect(d2[j]).OrderBy(y => y).Where(y => y < x && (positions[y].DistanceTo(positions[x]) < eps * eps)))
                            {
                                moved.Add(x, item);
                                return item;
                            }
                        }
                    }
                }


                return x;

            }).ToList();

            indices.Clear();

            foreach (var i in newind)
            {
                indices.Add(i);
            }
                
            return moved.Where(x=>x.Key != x.Value).ToDictionary(x =>x.Key, x=>x.Value);
        }

        public static readonly Point3D ZeroPoint = new Point3D(0, 0, 0);

        public static double DistToZero(this Point3D p)
        {
            return p.DistanceTo(ZeroPoint);
        }

        public static IList<Point3D> GenerateGridLines(Size3D size, Point3D? centerz = null)
        {
            var lines = new List<Point3D>();

            var center = centerz ?? new Point3D();



            for (var i = -size.X / 2.0; i <= size.X / 2.0; i += 10.0)
            {
                lines.Add(new Point3D(i + center.X, -size.Y / 2.0 + center.Y, -size.Z / 2.0 + center.Z));
                lines.Add(new Point3D(i + center.X, size.Y / 2.0 + center.Y, -size.Z / 2.0 + center.Z));
            }

            for (var i = -size.Y / 2.0; i <= size.Y / 2.0; i += 10.0)
            {
                lines.Add(new Point3D(-size.X / 2.0 + center.X, i + center.Y, -size.Z / 2.0 + center.Z));
                lines.Add(new Point3D(size.X / 2.0 + center.X, i + center.Y, -size.Z / 2.0 + center.Z));
            }


            lines.Add(new Point3D(-size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, -size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(-size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, -size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(-size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, -size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(-size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, -size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(-size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(-size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(-size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(-size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            lines.Add(new Point3D(size.X / 2.0 + center.X, -size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));
            lines.Add(new Point3D(size.X / 2.0 + center.X, size.Y / 2.0 + center.Y, size.Z / 2.0 + center.Z));

            return lines;
        }

        public static T GetHitResult<T>(this Visual viewPort3d, Point location)
             where T : Visual3D
        {
            var result = VisualTreeHelper.HitTest(viewPort3d, location);

            if (result != null)
                return result.VisualHit as T;

            return null;
        }


        public static bool TriangleIntersection(Vector3D p1,  // Triangle vertices
                          Vector3D p2,
                          Vector3D p3,
                          Vector3D ro,  //Ray origin
                          Vector3D rd,  //Ray direction
                           out Vector3D outp)
        {
            outp = new Vector3D(Double.NaN, Double.NaN, Double.NaN);

            //Find vectors for two edges sharing V1
            var e1 = p2 - p1;
            var e2 = p3 - p1;
            //Begin calculating determinant - also used to calculate u parameter
            var p = Vector3D.CrossProduct(rd, e2);
            //if determinant is near zero, ray lies in plane of triangle
            var det = Vector3D.DotProduct(e1, p);
            //NOT CULLING
            if (Math.Abs(det) < Double.Epsilon)
                return false;

            var invDet = 1.0 / det;

            //calculate distance from V1 to ray origin
            var T = ro - p1;

            //Calculate u parameter and test bound
            var u = Vector3D.DotProduct(T, p) * invDet;
            //The intersection lies outside of the triangle

            //Prepare to test v parameter
            var q = Vector3D.CrossProduct(T, e1);

            //Calculate V parameter and test bound
            var v = Vector3D.DotProduct(rd, q) * invDet;

            var t = Vector3D.DotProduct(e2, q) * invDet;

            outp = new Vector3D(t, u, v);

            return !(v < 0) && !(u + v > 1) && (!(u < 0) && !(u > 1));
        }
    }
}

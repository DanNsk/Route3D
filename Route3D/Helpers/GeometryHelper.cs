using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace Route3D.Helpers
{
    public static class GeometryHelper
    {
        public static readonly IList<Color> GoodColors = typeof(Colors).GetProperties(BindingFlags.Static | BindingFlags.Public).Where(p => p.PropertyType == typeof(Color)).Select(p => (Color)p.GetValue(null)).Where(c => !c.Equals(Colors.White) && !c.Equals(Colors.Transparent)).ToList();


        public static List<List<Point>> ChangePointUnits(this List<List<Point3D>> points)
        {
            return points == null ? null : points.Select(x => x.Select(y => new Point(y.X, y.Y)).ToList()).ToList();
        }

        public static List<List<Point3D>> ChangePointUnits(this List<List<Point>> points, double z)
        {
            return points == null ? null : points.Select(x => x.Select(y => new Point3D(y.X, y.Y, z)).ToList()).ToList();
        }

        //----------------------------------------------------------------------

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
                if (Math.Abs(ipNext.Y - pt.Y) < double.Epsilon)
                {
                    if ((Math.Abs(ipNext.X - pt.X) < double.Epsilon) || (Math.Abs(ip.Y - pt.Y) < double.Epsilon &&
                                                                         ((ipNext.X > pt.X) == (ip.X < pt.X)))) return null;
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
                            if (Math.Abs(d) < double.Epsilon) return null;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = !result;
                        }
                    }
                    else
                    {
                        if (ipNext.X > pt.X)
                        {
                            var d = (ip.X - pt.X)*(ipNext.Y - pt.Y) -
                                    (ipNext.X - pt.X)*(ip.Y - pt.Y);
                            if (Math.Abs(d) < double.Epsilon) return null;
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

        public static List<List<int>> FixMergeIndexPaths(this List<List<int>> paths, Point3DCollection poss, double eps)
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


        public static Dictionary<int, int> JoinNearIndices(this MeshGeometry3D geometry, double eps)
        {
            return JoinNearIndices(geometry.TriangleIndices, geometry.Positions, eps);
        }

        public static Dictionary<int, int> JoinNearIndices(this Int32Collection indices, Point3DCollection positions, double eps)
        {
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
            outp = new Vector3D(double.NaN, double.NaN, double.NaN);

            //Find vectors for two edges sharing V1
            var e1 = p2 - p1;
            var e2 = p3 - p1;
            //Begin calculating determinant - also used to calculate u parameter
            var p = Vector3D.CrossProduct(rd, e2);
            //if determinant is near zero, ray lies in plane of triangle
            var det = Vector3D.DotProduct(e1, p);
            //NOT CULLING
            if (Math.Abs(det) < double.Epsilon)
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace Route3D.Helpers
{
    public static class GeometryHelper
    {
        public static List<List<IntPoint>> ChangePointUnits(this List<List<Point3D>> points, double eps)
        {
            return points == null ? null : points.Select(x => x.Select(y => new IntPoint(y.X / eps, y.Y / eps)).ToList()).ToList();
        }

        public static List<List<Point3D>> ChangePointUnits(this List<List<IntPoint>> points, double z, double eps)
        {
            return points == null ? null : points.Select(x => x.Select(y => new Point3D(y.X * eps, y.Y * eps, z)).ToList()).ToList();
        }

        public static List<List<Point3D>> ClipPaths(this List<List<Point3D>> p1, List<List<Point3D>> p2, ClipType ct, double z, double eps)
        {
            if (p1 != null && p2 != null)
            {
                var xpaths1 = p1.ChangePointUnits(eps);
                var xpaths2 = p2.ChangePointUnits(eps);

                var clipper = new Clipper();

                clipper.AddPaths(xpaths1, PolyType.Subject, true);
                clipper.AddPaths(xpaths2, PolyType.Clip, true);

                var solutions = new List<List<IntPoint>>();

                solutions = !clipper.Execute(ct, solutions) ? xpaths2 : solutions.Where(x => x.Count > 3).ToList();


                return solutions.ChangePointUnits(z, eps);

            }
            else
            {
                return p1 ?? p2;
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


        public static List<List<int>> CloseMergePaths(this List<List<int>> paths, Point3DCollection poss, double eps)
        {
            List<List<int>> pathsnc;

           

            var ieps = 1 / eps;

            while ((pathsnc = paths.Where(x => x.Count > 0 && x[0] != x[x.Count - 1]).ToList()).Count > 0)
            {
                var ia = new Int32Collection(pathsnc.Select(x => new[] { x.First(), x.Last() }).SelectMany(x => x));

                var dem = ia.JoinCloseIndices(poss, eps);
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

            return paths.ToList();
        }


        public static Dictionary<int, int> JoinCloseIndices(this MeshGeometry3D geometry, double eps)
        {
            return JoinCloseIndices(geometry.TriangleIndices, geometry.Positions, eps);
        }

        public static Dictionary<int, int> JoinCloseIndices(this Int32Collection indices, Point3DCollection positions, double eps)
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

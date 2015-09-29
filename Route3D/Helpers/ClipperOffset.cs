/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.2.1                                                           *
* Date      :  31 October 2014                                                 *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2014                                         *
*                                                                              *
* License:                                                                     *
* Use, modification & distribution is subject to Boost Software License Ver 1. *
* http://www.boost.org/LICENSE_1_0.txt                                         *
*                                                                              *
* Attributions:                                                                *
* The code in this library is an extension of Bala Vatti's clipping algorithm: *
* "A generic solution to polygon clipping"                                     *
* Communications of the ACM, Vol 35, Issue 7 (July 1992) pp 56-63.             *
* http://portal.acm.org/citation.cfm?id=129906                                 *
*                                                                              *
* Computer graphics and geometric modeling: implementation and algorithms      *
* By Max K. Agoston                                                            *
* Springer; 1 edition (January 4, 2005)                                        *
* http://books.google.com/books?q=vatti+clipping+agoston                       *
*                                                                              *
* See also:                                                                    *
* "Polygon Offsetting by Computing Winding Numbers"                            *
* Paper no. DETC2005-85513 pp. 565-575                                         *
* ASME 2005 International Design Engineering Technical Conferences             *
* and Computers and Information in Engineering Conference (IDETC/CIE2005)      *
* September 24-28, 2005 , Long Beach, California, USA                          *
* http://www.me.berkeley.edu/~mcmains/pubs/DAC05OffsetPolygon.pdf              *
*                                                                              *
*******************************************************************************/

using System;
using System.Collections.Generic;
using System.Windows;

namespace Route3D.Helpers
{
    public class PolyNode
    {
        private readonly List<Point> contour = new List<Point>();
        private readonly List<PolyNode> сhilds = new List<PolyNode>();
        private int index;
        public EndType EndType { get; set; }
        public JoinType JoinType { get; set; }
        public bool IsOpen { get; set; }

        public PolyNode Parent { get; set; }


        public int ChildCount
        {
            get { return сhilds.Count; }
        }

        public List<Point> Contour
        {
            get { return contour; }
        }

        public List<PolyNode> Childs
        {
            get { return сhilds; }
        }


        public bool IsHole
        {
            get
            {
                var result = true;
                var node = Parent;
                while (node != null)
                {
                    result = !result;
                    node = node.Parent;
                }

                return result;
            }
        }

        public void AddChild(PolyNode child)
        {
            var cnt = сhilds.Count;
            сhilds.Add(child);
            child.Parent = this;
            child.index = cnt;
        }

        public PolyNode GetNext()
        {
            return сhilds.Count > 0 ? сhilds[0] : GetNextSiblingUp();
        }

        private PolyNode GetNextSiblingUp()
        {
            if (Parent == null)
                return null;
            else if (index == Parent.сhilds.Count - 1)
                return Parent.GetNextSiblingUp();
            else
                return Parent.сhilds[index + 1];
        }
    }


    public class ClipperOffset
    {
        private const double TWO_PI = Math.PI * 2;
        private const double DEF_ARC_TOLERANCE = 0.025;
        private readonly List<Vector> normals = new List<Vector>();
        private readonly PolyNode polyNodes = new PolyNode();
        private double delta, sinA, sin, cos;
        private List<Point> destPoly;
        private List<Point> srcPoly;


        private List<List<Point>> destPolys;
        private Point lowest;
        private double miterLim, stepsPerRad;

        public ClipperOffset(
            double miterLimit = 2.0, double arcTolerance = DEF_ARC_TOLERANCE)
        {
            MiterLimit = miterLimit;
            ArcTolerance = arcTolerance;
            lowest.X = -1;
        }

        public double ArcTolerance { get; set; }
        public double MiterLimit { get; set; }
        //------------------------------------------------------------------------------

        public void Clear()
        {
            polyNodes.Childs.Clear();
            lowest.X = -1;
        }

        //------------------------------------------------------------------------------

        public void AddPath(List<Point> path, JoinType joinType, EndType endType, double eps = double.Epsilon)
        {
            var highI = path.Count - 1;
            if (highI < 0) return;
            var newNode = new PolyNode
            {
                JoinType = joinType,
                EndType = endType
            };

            //strip duplicate points from path and also get index to the lowest point ...
            if (endType == EndType.ClosedLine || endType == EndType.ClosedPolygon)
            {
                while (highI > 0 && path[0] == path[highI])
                {
                    highI--;
                }
            }

            newNode.Contour.Capacity = highI + 1;
            newNode.Contour.Add(path[0]);
            int j = 0, k = 0;

            for (var i = 1; i <= highI; i++)
            {
                if (newNode.Contour[j].DistanceTo(path[i]) > eps)
                {
                    j++;
                    newNode.Contour.Add(path[i]);

                    if (path[i].Y > newNode.Contour[k].Y || (path[i].Y <= newNode.Contour[k].Y && path[i].X < newNode.Contour[k].X))
                    {
                        k = j;
                    }
                }
            }

            if (endType == EndType.ClosedPolygon && j < 2)
                return;

            polyNodes.AddChild(newNode);

            //if this path's lowest pt is lower than all the others then update m_lowest
            if (endType != EndType.ClosedPolygon) return;
            if (lowest.X < 0)
            {
                lowest = new Point(polyNodes.ChildCount - 1, k);
            }
            else
            {
                var ip = polyNodes.Childs[(int)lowest.X].Contour[(int)lowest.Y];
                if (newNode.Contour[k].Y > ip.Y || (newNode.Contour[k].Y < ip.Y && newNode.Contour[k].X < ip.X))
                {
                    lowest = new Point(polyNodes.ChildCount - 1, k);
                }
            }
        }

        //------------------------------------------------------------------------------

        public void AddPaths(List<List<Point>> paths, JoinType joinType, EndType endType, double eps = double.Epsilon)
        {
            foreach (var p in paths)
            {
                AddPath(p, joinType, endType, eps);
            }
        }

        //------------------------------------------------------------------------------

        private void FixOrientations()
        {
            //fixup orientations of all closed paths if the orientation of the
            //closed path with the lowermost vertex is wrong ...
            if (lowest.X >= 0 && !(polyNodes.Childs[(int)lowest.X].Contour).Orientation())
            {
                for (var i = 0; i < polyNodes.ChildCount; i++)
                {
                    var node = polyNodes.Childs[i];
                    if (node.EndType == EndType.ClosedPolygon || (node.EndType == EndType.ClosedLine && (node.Contour.Orientation())))
                        node.Contour.Reverse();
                }
            }
            else
            {
                for (var i = 0; i < polyNodes.ChildCount; i++)
                {
                    var node = polyNodes.Childs[i];
                    if (node.EndType == EndType.ClosedLine && !(node.Contour.Orientation()))
                        node.Contour.Reverse();
                }
            }
        }

        //------------------------------------------------------------------------------


        //------------------------------------------------------------------------------

        private void DoOffset(double deltap, double eps)
        {
            destPolys = new List<List<Point>>();
            this.delta = deltap;

            //if Zero offset, just copy any CLOSED polygons to m_p and return ...
            if (Math.Abs(deltap) < double.Epsilon)
            {
                destPolys.Capacity = polyNodes.ChildCount;
                for (var i = 0; i < polyNodes.ChildCount; i++)
                {
                    var node = polyNodes.Childs[i];
                    if (node.EndType == EndType.ClosedPolygon)
                        destPolys.Add(node.Contour);
                }
                return;
            }

            //see offset_triginometry3.svg in the documentation folder ...
            if (MiterLimit > 2) miterLim = 2 / (MiterLimit * MiterLimit);
            else miterLim = 0.5;

            double y;
            if (ArcTolerance <= 0.0)
                y = DEF_ARC_TOLERANCE;
            else if (ArcTolerance > Math.Abs(deltap) * DEF_ARC_TOLERANCE)
                y = Math.Abs(deltap) * DEF_ARC_TOLERANCE;
            else
                y = ArcTolerance;
            //see offset_triginometry2.svg in the documentation folder ...
            var steps = (Math.PI / Math.Acos(1 - y / Math.Abs(deltap)));
            sin = Math.Sin(TWO_PI / steps);
            cos = Math.Cos(TWO_PI / steps);
            stepsPerRad = steps / TWO_PI;
            if (deltap < 0.0) sin = -sin;

            destPolys.Capacity = polyNodes.ChildCount * 2;
            for (var i = 0; i < polyNodes.ChildCount; i++)
            {
                var node = polyNodes.Childs[i];
                srcPoly = node.Contour;

                var len = srcPoly.Count;

                if (len == 0 || (deltap <= 0 && (len < 3 || node.EndType != EndType.ClosedPolygon)))
                    continue;

                destPoly = new List<Point>();

                if (len == 1)
                {
                    if (node.JoinType == JoinType.Round)
                    {
                        double X = 1.0, Y = 0.0;
                        for (var j = 1; j <= steps; j++)
                        {
                            destPoly.Add(new Point((srcPoly[0].X + X * deltap), (srcPoly[0].Y + Y * deltap)));
                            var X2 = X;
                            X = X * cos - sin * Y;
                            Y = X2 * sin + Y * cos;
                        }
                    }
                    else
                    {
                        double X = -1.0, Y = -1.0;
                        for (var j = 0; j < 4; ++j)
                        {
                            destPoly.Add(new Point(
                                (srcPoly[0].X + X * deltap),
                                (srcPoly[0].Y + Y * deltap)));
                            if (X < 0) X = 1;
                            else if (Y < 0) Y = 1;
                            else X = -1;
                        }
                    }
                    destPolys.Add(destPoly);
                    continue;
                }

                //build m_normals ...
                normals.Clear();
                normals.Capacity = len;
                for (var j = 0; j < len - 1; j++)
                    normals.Add(srcPoly[j].GetNormalWith(srcPoly[j + 1]));

                if (node.EndType == EndType.ClosedLine ||
                    node.EndType == EndType.ClosedPolygon)
                    normals.Add(srcPoly[len - 1].GetNormalWith(srcPoly[0]));
                else
                    normals.Add(new Vector(normals[len - 2].X, normals[len - 2].Y));

                switch (node.EndType)
                {
                    case EndType.ClosedPolygon:
                        {
                            var k = len - 1;
                            for (var j = 0; j < len; j++)
                                OffsetPoint(j, ref k, node.JoinType, eps);
                            destPolys.Add(destPoly);
                        }
                        break;
                    case EndType.ClosedLine:
                        {
                            var k = len - 1;
                            for (var j = 0; j < len; j++)
                                OffsetPoint(j, ref k, node.JoinType, eps);
                            destPolys.Add(destPoly);
                            destPoly = new List<Point>();
                            //re-build m_normals ...
                            var n = normals[len - 1];
                            for (var j = len - 1; j > 0; j--)
                                normals[j] = new Vector(-normals[j - 1].X, -normals[j - 1].Y);
                            normals[0] = new Vector(-n.X, -n.Y);


                            k = 0;
                            for (var j = len - 1; j >= 0; j--)
                                OffsetPoint(j, ref k, node.JoinType, eps);
                            destPolys.Add(destPoly);
                        }
                        break;
                    default:
                        {
                            var k = 0;
                            for (var j = 1; j < len - 1; ++j)
                                OffsetPoint(j, ref k, node.JoinType, eps);

                            Point pt1;
                            if (node.EndType == EndType.OpenButt)
                            {
                                var j = len - 1;
                                pt1 = new Point((srcPoly[j].X + normals[j].X * deltap), (srcPoly[j].Y + normals[j].Y * deltap));
                                destPoly.Add(pt1);
                                pt1 = new Point((srcPoly[j].X - normals[j].X * deltap), (srcPoly[j].Y - normals[j].Y * deltap));
                                destPoly.Add(pt1);
                            }
                            else
                            {
                                var j = len - 1;
                                k = len - 2;
                                sinA = 0;
                                normals[j] = new Vector(-normals[j].X, -normals[j].Y);
                                if (node.EndType == EndType.OpenSquare)
                                    DoSquare(j, k);
                                else
                                    Do(j, k);
                            }

                            //re-build m_normals ...
                            for (var j = len - 1; j > 0; j--)
                                normals[j] = new Vector(-normals[j - 1].X, -normals[j - 1].Y);

                            normals[0] = new Vector(-normals[1].X, -normals[1].Y);

                            k = len - 1;
                            for (var j = k - 1; j > 0; --j)
                                OffsetPoint(j, ref k, node.JoinType, eps);

                            if (node.EndType == EndType.OpenButt)
                            {
                                pt1 = new Point((srcPoly[0].X - normals[0].X * deltap), (srcPoly[0].Y - normals[0].Y * deltap));
                                destPoly.Add(pt1);
                                pt1 = new Point((srcPoly[0].X + normals[0].X * deltap), (srcPoly[0].Y + normals[0].Y * deltap));
                                destPoly.Add(pt1);
                            }
                            else
                            {
                                sinA = 0;
                                if (node.EndType == EndType.OpenSquare)
                                    DoSquare(0, 1);
                                else
                                    Do(0, 1);
                            }
                            destPolys.Add(destPoly);
                        }
                        break;
                }
            }
        }

        //------------------------------------------------------------------------------

        public List<List<Point>> Execute(double deltap, double eps = double.Epsilon)
        {
            var solution = new List<List<Point>>();
            FixOrientations();
            DoOffset(deltap, eps);
            //now clean up 'corners' ...

            var clpr = new Clipper();

            clpr.AddPaths(destPolys, PolyType.Subject);

            if (deltap > 0)
            {
                clpr.Execute(ClipType.Union, solution, PolyFillType.Positive, PolyFillType.Positive);
            }
            else
            {
                var r = destPolys.GetBounds();


                var outer = new List<Point>(4)
                {
                    new Point(r.Left - 10, r.Bottom + 10),
                    new Point(r.Right + 10, r.Bottom + 10),
                    new Point(r.Right + 10, r.Top - 10),
                    new Point(r.Left - 10, r.Top - 10)
                };


                clpr.AddPath(outer, PolyType.Subject);
                clpr.ReverseSolution = true;
                clpr.Execute(ClipType.Union, solution, PolyFillType.Negative, PolyFillType.Negative);
                if (solution.Count > 0) solution.RemoveAt(0);
            }

            return solution;
        }

        //------------------------------------------------------------------------------

        private void OffsetPoint(int j, ref int k, JoinType jointype, double eps)
        {
            sinA = (normals[k].X * normals[j].Y - normals[j].X * normals[k].Y);

            if (sinA > 1.0) sinA = 1.0;
            else if (sinA < -1.0) sinA = -1.0;

            if (sinA * delta < 0)
            {
                var p1 = new Point((srcPoly[j].X + normals[k].X * delta), (srcPoly[j].Y + normals[k].Y * delta));
                var p2 = new Point((srcPoly[j].X + normals[j].X * delta), (srcPoly[j].Y + normals[j].Y * delta));
                destPoly.Add(p1);

                if (p1.DistanceTo(srcPoly[j]) > eps)
                    destPoly.Add(srcPoly[j]);

                if (p2.DistanceTo(srcPoly[j]) > eps)
                    destPoly.Add(p2);
 
            }
            else
            {
                switch (jointype)
                {
                    case JoinType.Miter:
                        {
                            var r = 1 + (normals[j].X * normals[k].X +
                                         normals[j].Y * normals[k].Y);
                            if (r >= miterLim) DoMiter(j, k, r);
                            else DoSquare(j, k);
                            break;
                        }
                    case JoinType.Square:
                        DoSquare(j, k);
                        break;
                    case JoinType.Round:
                        Do(j, k);
                        break;
                }
            }
            k = j;
        }

        //------------------------------------------------------------------------------

        private void DoSquare(int j, int k)
        {
            var dx = Math.Tan(Math.Atan2(sinA,
                normals[k].X * normals[j].X + normals[k].Y * normals[j].Y) / 4);
            destPoly.Add(new Point((srcPoly[j].X + delta * (normals[k].X - normals[k].Y * dx)), (srcPoly[j].Y + delta * (normals[k].Y + normals[k].X * dx))));
            destPoly.Add(new Point((srcPoly[j].X + delta * (normals[j].X + normals[j].Y * dx)), (srcPoly[j].Y + delta * (normals[j].Y - normals[j].X * dx))));
        }

        //------------------------------------------------------------------------------

        private void DoMiter(int j, int k, double r)
        {
            var q = delta / r;
            destPoly.Add(new Point((srcPoly[j].X + (normals[k].X + normals[j].X) * q), (srcPoly[j].Y + (normals[k].Y + normals[j].Y) * q)));
        }

        //------------------------------------------------------------------------------

        private void Do(int j, int k)
        {
            var a = Math.Atan2(sinA, normals[k].X * normals[j].X + normals[k].Y * normals[j].Y);

            var steps = Math.Max((stepsPerRad * Math.Abs(a)), 1);

            double x = normals[k].X, y = normals[k].Y;
            for (var i = 0; i < steps; ++i)
            {
                destPoly.Add(new Point((srcPoly[j].X + x * delta), (srcPoly[j].Y + y * delta)));
                var x2 = x;
                x = x * cos - sin * y;
                y = x2 * sin + y * cos;
            }
            destPoly.Add(new Point(
                (srcPoly[j].X + normals[j].X * delta),
                (srcPoly[j].Y + normals[j].Y * delta)));
        }
    }
}
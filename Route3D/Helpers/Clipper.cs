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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Route3D.Geometry;

namespace Route3D.Helpers
{
    public enum ClipType
    {
        Intersection,
        Union,
        Difference,
        Xor
    };

    public enum PolyType
    {
        Subject,
        Clip
    };

    //By far the most widely used winding rules for polygon filling are
    //EvenOdd & NonZero (GDI, GDI+, XLib, OpenGL, Cairo, AGG, Quartz, SVG, Gr32)
    //Others rules include Positive, Negative and ABS_GTR_EQ_TWO (only in OpenGL)
    //see http://glprogramming.com/red/chapter11.html
    public enum PolyFillType
    {
        EvenOdd,
        NonZero,
        Positive,
        Negative
    };

    public enum JoinType
    {
        Square,
        Round,
        Miter
    };

    public enum EndType
    {
        ClosedPolygon,
        ClosedLine,
        OpenButt,
        OpenSquare,
        OpenRound
    };

    public  enum EdgeSide
    {
        Left,
        Right
    };

    public enum Direction
    {
        RightToLeft,
        LeftToRight
    };

    public class TEdge
    {
        public  Point Bot;
        public  Point Curr;
        public  Point Delta;
        public  double Dx;
        public  TEdge Next;
        public  TEdge NextInAEL;
        public  TEdge NextInLML;
        public  TEdge NextInSEL;
        public  int OutIdx;
        public  PolyType PolyTyp;
        public  TEdge Prev;
        public  TEdge PrevInAEL;
        public  TEdge PrevInSEL;
        public  EdgeSide Side;
        public  Point Top;
        public  int WindCnt;
        public  int WindCnt2; //winding count of the opposite polytype
        public  int WindDelta; //1 or -1 depending on winding direction
    };

    public class IntersectNode
    {
        public  TEdge Edge1;
        public  TEdge Edge2;
        public  Point Pt;
    };

    public class LocalMinima
    {
        public  TEdge LeftBound;
        public  LocalMinima Next;
        public  TEdge RightBound;
        public  double Y;
    };

    public class Scanbeam
    {
        public  Scanbeam Next;
        public  double Y;
    };

    public class OutRec
    {
        public  OutPt BottomPt;
        public  OutRec FirstLeft;
        public  int Idx;
        public  bool IsHole;
        public  bool IsOpen;
        public  OutPt Pts;
    };

    public class OutPt : IEnumerable<Point>
    {
        public  int Idx;
        public  OutPt Next;
        public  OutPt Prev;
        public  Point Pt;

        public IEnumerator<Point> GetEnumerator()
        {
            yield return Pt;

            var start = this;
            var op = start;

            while((op = op.Next) != start)
            {
                yield return op.Pt;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    };

    public  class Join
    {
        public  Point OffPt;
        public  OutPt OutPt1;
        public  OutPt OutPt2;
    };

    public class ClipperBase
    {
        protected const double horizontal = -3.4E+38;
        protected const int Skip = -2;
        protected const int Unassigned = -1;
        protected  LocalMinima m_CurrentLM;
        protected  List<List<TEdge>> m_edges = new List<List<TEdge>>();
        protected  LocalMinima m_MinimaList;

        //------------------------------------------------------------------------------

        protected ClipperBase() //constructor (nb: no external instantiation)
        {
            m_MinimaList = null;
            m_CurrentLM = null;
        }

        //------------------------------------------------------------------------------

        public bool PreserveCollinear { get; set; }

        //------------------------------------------------------------------------------


        protected bool IsHorizontal(TEdge e)
        {
            return Math.Abs(e.Delta.Y) < double.Epsilon;
        }

        //------------------------------------------------------------------------------


      
        protected  bool SlopesEqual(TEdge e1, TEdge e2)
        {
            return Math.Abs((e1.Delta.Y)*(e2.Delta.X) - (e1.Delta.X)*(e2.Delta.Y)) < double.Epsilon;
        }

        //------------------------------------------------------------------------------

        protected static bool SlopesEqual(Point pt1, Point pt2, Point pt3)
        {
            return Math.Abs((pt1.Y - pt2.Y)*(pt2.X - pt3.X) - (pt1.X - pt2.X)*(pt2.Y - pt3.Y)) < double.Epsilon;
        }

        //------------------------------------------------------------------------------

        public virtual void Clear()
        {
            DisposeLocalMinimaList();
            foreach (var t in m_edges)
            {
                for (var j = 0; j < t.Count; ++j) t[j] = null;
                t.Clear();
            }
            m_edges.Clear();
        }

        //------------------------------------------------------------------------------

        private void DisposeLocalMinimaList()
        {
            while (m_MinimaList != null)
            {
                var tmpLm = m_MinimaList.Next;
                m_MinimaList = null;
                m_MinimaList = tmpLm;
            }
            m_CurrentLM = null;
        }

        //------------------------------------------------------------------------------

        private void InitEdge(TEdge e, TEdge eNext,
            TEdge ePrev, Point pt)
        {
            e.Next = eNext;
            e.Prev = ePrev;
            e.Curr = pt;
            e.OutIdx = Unassigned;
        }

        //------------------------------------------------------------------------------

        private void InitEdge2(TEdge e, PolyType polyType)
        {
            if (e.Curr.Y >= e.Next.Curr.Y)
            {
                e.Bot = e.Curr;
                e.Top = e.Next.Curr;
            }
            else
            {
                e.Top = e.Curr;
                e.Bot = e.Next.Curr;
            }
            SetDx(e);
            e.PolyTyp = polyType;
        }

        //------------------------------------------------------------------------------

        private TEdge FindNextLocMin(TEdge E)
        {
            for (;;)
            {
                while (E.Bot != E.Prev.Bot || E.Curr == E.Top) E = E.Next;
                if (Math.Abs(E.Dx - horizontal) > double.Epsilon && Math.Abs(E.Prev.Dx - horizontal) > double.Epsilon) break;
                while (Math.Abs(E.Prev.Dx - horizontal) < double.Epsilon) E = E.Prev;
                var e2 = E;
                while (Math.Abs(E.Dx - horizontal) < double.Epsilon)
                    E = E.Next;

                if (Math.Abs(E.Top.Y - E.Prev.Bot.Y) < double.Epsilon)
                    continue; //ie just an intermediate horz.

                if (e2.Prev.Bot.X < E.Bot.X)
                    E = e2;

                break;
            }
            return E;
        }

        //------------------------------------------------------------------------------

        private TEdge ProcessBound(TEdge E, bool LeftBoundIsForward)
        {
            TEdge EStart, Result = E;
            TEdge Horz;

            if (Result.OutIdx == Skip)
            {
                //check if there are edges beyond the skip edge in the bound and if so
                //create another LocMin and calling ProcessBound once more ...
                E = Result;
                if (LeftBoundIsForward)
                {
                    while (Math.Abs(E.Top.Y - E.Next.Bot.Y) < double.Epsilon) E = E.Next;
                    while (E != Result && Math.Abs(E.Dx - horizontal) < double.Epsilon) E = E.Prev;
                }
                else
                {
                    while (Math.Abs(E.Top.Y - E.Prev.Bot.Y) < double.Epsilon) E = E.Prev;
                    while (E != Result && Math.Abs(E.Dx - horizontal) < double.Epsilon) E = E.Next;
                }
                if (E == Result)
                {
                    Result = LeftBoundIsForward ? E.Next : E.Prev;
                }
                else
                {
                    //there are more edges in the bound beyond result starting with E
                    E = LeftBoundIsForward ? Result.Next : Result.Prev;


                    var locMin = new LocalMinima
                    {
                        Next = null,
                        Y = E.Bot.Y,
                        LeftBound = null,
                        RightBound = E
                    };
                    E.WindDelta = 0;
                    Result = ProcessBound(E, LeftBoundIsForward);
                    InsertLocalMinima(locMin);
                }
                return Result;
            }

            if (Math.Abs(E.Dx - horizontal) < double.Epsilon)
            {
                //We need to be careful with open paths because this may not be a
                //true local minima (ie E may be following a skip edge).
                //Also, consecutive horz. edges may start heading left before going right.
                EStart = LeftBoundIsForward ? E.Prev : E.Next;
                if (EStart.OutIdx != Skip)
                {
                    if (Math.Abs(EStart.Dx - horizontal) < double.Epsilon) //ie an adjoining horizontal skip edge
                    {
                        if (Math.Abs(EStart.Bot.X - E.Bot.X) > double.Epsilon && Math.Abs(EStart.Top.X - E.Bot.X) > double.Epsilon)
                            ReverseHorizontal(E);
                    }
                    else if (Math.Abs(EStart.Bot.X - E.Bot.X) > double.Epsilon)
                        ReverseHorizontal(E);
                }
            }

            EStart = E;
            if (LeftBoundIsForward)
            {
                while (Math.Abs(Result.Top.Y - Result.Next.Bot.Y) < double.Epsilon && Result.Next.OutIdx != Skip)
                    Result = Result.Next;
                if (Math.Abs(Result.Dx - horizontal) < double.Epsilon && Result.Next.OutIdx != Skip)
                {
                    //nb: at the top of a bound, horizontals are added to the bound
                    //only when the preceding edge attaches to the horizontal's left vertex
                    //unless a Skip edge is encountered when that becomes the top divide
                    Horz = Result;
                    while (Math.Abs(Horz.Prev.Dx - horizontal) < double.Epsilon) Horz = Horz.Prev;

                    if (Math.Abs(Horz.Prev.Top.X - Result.Next.Top.X) > double.Epsilon)
                    {
                        if (Horz.Prev.Top.X > Result.Next.Top.X) Result = Horz.Prev;
                    }
                }
                while (E != Result)
                {
                    E.NextInLML = E.Next;
                    if (Math.Abs(E.Dx - horizontal) < double.Epsilon && E != EStart && Math.Abs(E.Bot.X - E.Prev.Top.X) > double.Epsilon)
                        ReverseHorizontal(E);
                    E = E.Next;
                }
                if (Math.Abs(E.Dx - horizontal) < double.Epsilon && E != EStart && Math.Abs(E.Bot.X - E.Prev.Top.X) > double.Epsilon)
                    ReverseHorizontal(E);
                Result = Result.Next; //move to the edge just beyond current bound
            }
            else
            {
                while (Math.Abs(Result.Top.Y - Result.Prev.Bot.Y) < double.Epsilon && Result.Prev.OutIdx != Skip)
                    Result = Result.Prev;
                if (Math.Abs(Result.Dx - horizontal) < double.Epsilon && Result.Prev.OutIdx != Skip)
                {
                    Horz = Result;
                    while (Math.Abs(Horz.Next.Dx - horizontal) < double.Epsilon) Horz = Horz.Next;
                    if (Math.Abs(Horz.Next.Top.X - Result.Prev.Top.X) < double.Epsilon)
                    {
                        Result = Horz.Next;
                    }
                    else if (Horz.Next.Top.X > Result.Prev.Top.X) Result = Horz.Next;
                }

                while (E != Result)
                {
                    E.NextInLML = E.Prev;
                    if (Math.Abs(E.Dx - horizontal) < double.Epsilon && E != EStart && Math.Abs(E.Bot.X - E.Next.Top.X) > double.Epsilon)
                        ReverseHorizontal(E);
                    E = E.Prev;
                }
                if (Math.Abs(E.Dx - horizontal) < double.Epsilon && E != EStart && Math.Abs(E.Bot.X - E.Next.Top.X) > double.Epsilon)
                    ReverseHorizontal(E);
                Result = Result.Prev; //move to the edge just beyond current bound
            }
            return Result;
        }

        //------------------------------------------------------------------------------


        public bool AddPath(List<Point> pg, PolyType polyType)
        {
            var highI = pg.Count - 1;

            while (highI > 0 && (pg[highI] == pg[0])) --highI;

            while (highI > 0 && (pg[highI] == pg[highI - 1])) --highI;

            if ((highI < 2))
                return false;

            //create a new edge array ...
            var edges = new List<TEdge>(highI + 1);
            for (var i = 0; i <= highI; i++) edges.Add(new TEdge());

            var IsFlat = true;

            //1. Basic (first) edge initialization ...
            edges[1].Curr = pg[1];

            InitEdge(edges[0], edges[1], edges[highI], pg[0]);
            InitEdge(edges[highI], edges[0], edges[highI - 1], pg[highI]);
            for (var i = highI - 1; i >= 1; --i)
            {
                InitEdge(edges[i], edges[i + 1], edges[i - 1], pg[i]);
            }
            var eStart = edges[0];

            //2. Remove duplicate vertices, and (when closed) collinear edges ...
            TEdge E = eStart, eLoopStop = eStart;
            for (;;)
            {
                //nb: allows matching start and end points when not Closed ...
                if (E.Curr == E.Next.Curr)
                {
                    if (E == E.Next) break;
                    if (E == eStart) eStart = E.Next;
                    E = RemoveEdge(E);
                    eLoopStop = E;
                    continue;
                }
                if (E.Prev == E.Next)
                    break; //only two vertices
                else if (SlopesEqual(E.Prev.Curr, E.Curr, E.Next.Curr) &&
                         (!PreserveCollinear ||
                          !Pt2IsBetweenPt1AndPt3(E.Prev.Curr, E.Curr, E.Next.Curr)))
                {
                    //Collinear edges are allowed for open paths but in closed paths
                    //the default is to merge adjacent collinear edges into a single edge.
                    //However, if the PreserveCollinear property is enabled, only overlapping
                    //collinear edges (ie spikes) will be removed from closed paths.
                    if (E == eStart) eStart = E.Next;
                    E = RemoveEdge(E);
                    E = E.Prev;
                    eLoopStop = E;
                    continue;
                }
                E = E.Next;
                if ((E == eLoopStop)) break;
            }

            if (((E.Prev == E.Next)))
                return false;

            //3. Do second stage of edge initialization ...
            E = eStart;
            do
            {
                InitEdge2(E, polyType);
                E = E.Next;
                if (IsFlat && Math.Abs(E.Curr.Y - eStart.Curr.Y) > double.Epsilon) IsFlat = false;
            } while (E != eStart);

            //4. Finally, add edge bounds to LocalMinima list ...

            //Totally flat paths must be handled differently when adding them
            //to LocalMinima list to avoid endless loops etc ...
            if (IsFlat)
            {
                return false;
            }

            m_edges.Add(edges);
            TEdge EMin = null;

            //workaround to avoid an endless loop in the while loop below when
            //open paths have matching start and end points ...
            if (E.Prev.Bot == E.Prev.Top) E = E.Next;

            for (;;)
            {
                E = FindNextLocMin(E);
                if (E == EMin) break;
                else if (EMin == null) EMin = E;

                //E and E.Prev now share a local minima (left aligned if horizontal).
                //Compare their slopes to find which starts which bound ...
                var locMin = new LocalMinima
                {
                    Next = null,
                    Y = E.Bot.Y
                };
                bool leftBoundIsForward;
                if (E.Dx < E.Prev.Dx)
                {
                    locMin.LeftBound = E.Prev;
                    locMin.RightBound = E;
                    leftBoundIsForward = false; //Q.nextInLML = Q.prev
                }
                else
                {
                    locMin.LeftBound = E;
                    locMin.RightBound = E.Prev;
                    leftBoundIsForward = true; //Q.nextInLML = Q.next
                }
                locMin.LeftBound.Side = EdgeSide.Left;
                locMin.RightBound.Side = EdgeSide.Right;

                if (locMin.LeftBound.Next == locMin.RightBound)
                    locMin.LeftBound.WindDelta = -1;
                else locMin.LeftBound.WindDelta = 1;
                locMin.RightBound.WindDelta = -locMin.LeftBound.WindDelta;

                E = ProcessBound(locMin.LeftBound, leftBoundIsForward);
                if (E.OutIdx == Skip) E = ProcessBound(E, leftBoundIsForward);

                var E2 = ProcessBound(locMin.RightBound, !leftBoundIsForward);
                if (E2.OutIdx == Skip) E2 = ProcessBound(E2, !leftBoundIsForward);

                if (locMin.LeftBound.OutIdx == Skip)
                    locMin.LeftBound = null;
                else if (locMin.RightBound.OutIdx == Skip)
                    locMin.RightBound = null;
                InsertLocalMinima(locMin);
                if (!leftBoundIsForward) E = E2;
            }
            return true;
        }

        //------------------------------------------------------------------------------

        public bool AddPaths(List<List<Point>> ppg, PolyType polyType)
        {
            var result = false;

            foreach (var t in ppg)
                if (AddPath(t, polyType)) result = true;

            return result;
        }

        //------------------------------------------------------------------------------

        protected  bool Pt2IsBetweenPt1AndPt3(Point pt1, Point pt2, Point pt3)
        {
            if ((pt1 == pt3) || (pt1 == pt2) || (pt3 == pt2)) return false;
            else if (Math.Abs(pt1.X - pt3.X) > double.Epsilon) return (pt2.X > pt1.X) == (pt2.X < pt3.X);
            else return (pt2.Y > pt1.Y) == (pt2.Y < pt3.Y);
        }

        //------------------------------------------------------------------------------

        private TEdge RemoveEdge(TEdge e)
        {
            //removes e from double_linked_list (but without removing from memory)
            e.Prev.Next = e.Next;
            e.Next.Prev = e.Prev;
            var result = e.Next;
            e.Prev = null; //flag as removed (see ClipperBase.Clear)
            return result;
        }

        //------------------------------------------------------------------------------

        private void SetDx(TEdge e)
        {
            e.Delta.X = (e.Top.X - e.Bot.X);
            e.Delta.Y = (e.Top.Y - e.Bot.Y);
            if (Math.Abs(e.Delta.Y) < double.Epsilon) e.Dx = horizontal;
            else e.Dx = (e.Delta.X)/(e.Delta.Y);
        }

        //---------------------------------------------------------------------------

        private void InsertLocalMinima(LocalMinima newLm)
        {
            if (m_MinimaList == null)
            {
                m_MinimaList = newLm;
            }
            else if (newLm.Y >= m_MinimaList.Y)
            {
                newLm.Next = m_MinimaList;
                m_MinimaList = newLm;
            }
            else
            {
                var tmpLm = m_MinimaList;
                while (tmpLm.Next != null && (newLm.Y < tmpLm.Next.Y))
                    tmpLm = tmpLm.Next;
                newLm.Next = tmpLm.Next;
                tmpLm.Next = newLm;
            }
        }

        //------------------------------------------------------------------------------

        protected void PopLocalMinima()
        {
            if (m_CurrentLM == null) return;
            m_CurrentLM = m_CurrentLM.Next;
        }

        //------------------------------------------------------------------------------

        private void ReverseHorizontal(TEdge e)
        {
            //swap horizontal edges' top and bottom x's so they follow the natural
            //progression of the bounds - ie so their xbots will align with the
            //adjoining lower edge. [Helpful in the ProcessHorizontal() method.]

            var t = e.Top.X;
            e.Top.X = e.Bot.X;
            e.Bot.X = t;
        }

        //------------------------------------------------------------------------------

        protected virtual void Reset()
        {
            m_CurrentLM = m_MinimaList;
            if (m_CurrentLM == null) return; //ie nothing to process

            //reset all edges ...
            var lm = m_MinimaList;
            while (lm != null)
            {
                var e = lm.LeftBound;
                if (e != null)
                {
                    e.Curr = e.Bot;
                    e.Side = EdgeSide.Left;
                    e.OutIdx = Unassigned;
                }
                e = lm.RightBound;
                if (e != null)
                {
                    e.Curr = e.Bot;
                    e.Side = EdgeSide.Right;
                    e.OutIdx = Unassigned;
                }
                lm = lm.Next;
            }
        }
    } //end ClipperBase

    public class Clipper : ClipperBase
    {
        private readonly List<Join> m_GhostJoins;
        private readonly List<IntersectNode> m_IntersectList;
        private readonly IComparer<IntersectNode> m_IntersectNodeComparer;
        private readonly List<Join> m_Joins;
        private readonly List<OutRec> m_PolyOuts;
        private TEdge m_ActiveEdges;
        private PolyFillType m_ClipFillType;
        private ClipType m_ClipType;
        private Scanbeam m_Scanbeam;
        private TEdge m_SortedEdges;
        private PolyFillType m_SubjFillType;


        private class MyIntersectNodeSort : IComparer<IntersectNode>
        {
            public int Compare(IntersectNode node1, IntersectNode node2)
            {
                var i = node2.Pt.Y - node1.Pt.Y;
                if (i > 0) return 1;
                else if (i < 0) return -1;
                else return 0;
            }
        }


        public Clipper(bool ioReverseSolution = false, bool ioStrictlySimple = false, bool ioPreserveCollinear = false)
        {
            m_Scanbeam = null;
            m_ActiveEdges = null;
            m_SortedEdges = null;
            m_IntersectList = new List<IntersectNode>();
            m_IntersectNodeComparer = new MyIntersectNodeSort();
            m_PolyOuts = new List<OutRec>();
            m_Joins = new List<Join>();
            m_GhostJoins = new List<Join>();
            ReverseSolution = ioReverseSolution;
            StrictlySimple = ioStrictlySimple;
            PreserveCollinear = ioPreserveCollinear;
        }

        //------------------------------------------------------------------------------

        public bool ReverseSolution { get; set; }

        //------------------------------------------------------------------------------

        public bool StrictlySimple { get; set; }

        //------------------------------------------------------------------------------

        private void DisposeScanbeamList()
        {
            while (m_Scanbeam != null)
            {
                var sb2 = m_Scanbeam.Next;
                m_Scanbeam = null;
                m_Scanbeam = sb2;
            }
        }

        public override void Clear()
        {
            DisposeScanbeamList();
            base.Clear();
        }

        //------------------------------------------------------------------------------

        protected override void Reset()
        {
            base.Reset();
            m_Scanbeam = null;
            m_ActiveEdges = null;
            m_SortedEdges = null;
            var lm = m_MinimaList;
            while (lm != null)
            {
                InsertScanbeam(lm.Y);
                lm = lm.Next;
            }
        }

        //------------------------------------------------------------------------------

        private void InsertScanbeam(double Y)
        {
            if (m_Scanbeam == null)
            {
                m_Scanbeam = new Scanbeam
                {
                    Next = null,
                    Y = Y
                };
            }
            else if (Y > m_Scanbeam.Y)
            {
                var newSb = new Scanbeam
                {
                    Y = Y,
                    Next = m_Scanbeam
                };
                m_Scanbeam = newSb;
            }
            else
            {
                var sb2 = m_Scanbeam;
                while (sb2.Next != null && (Y <= sb2.Next.Y)) sb2 = sb2.Next;
                if (Math.Abs(Y - sb2.Y) < double.Epsilon) return; //ie ignores duplicates
                var newSb = new Scanbeam
                {
                    Y = Y,
                    Next = sb2.Next
                };
                sb2.Next = newSb;
            }
        }

        //------------------------------------------------------------------------------

        public bool Execute(ClipType clipType, List<List<Point>> solution,
            PolyFillType subjFillType, PolyFillType clipFillType)
        {
            
            solution.Clear();
            m_SubjFillType = subjFillType;
            m_ClipFillType = clipFillType;
            m_ClipType = clipType;
            bool succeeded;
            try
            {
                succeeded = Executeprotected ();
                //build the return polygons ...
                if (succeeded) BuildResult(solution);
            }
            finally
            {
                DisposeAllPolyPts();
            }
            return succeeded;
        }

        //------------------------------------------------------------------------------

        public bool Execute(ClipType clipType, List<List<Point>> solution)
        {
            return Execute(clipType, solution,
                PolyFillType.EvenOdd, PolyFillType.EvenOdd);
        }

        //------------------------------------------------------------------------------


        protected  void FixHoleLinkage(OutRec outRec)
        {
            //skip if an outermost polygon or
            //already already points to the correct FirstLeft ...
            if (outRec.FirstLeft == null ||
                (outRec.IsHole != outRec.FirstLeft.IsHole &&
                 outRec.FirstLeft.Pts != null)) return;

            var orfl = outRec.FirstLeft;
            while (orfl != null && ((orfl.IsHole == outRec.IsHole) || orfl.Pts == null))
                orfl = orfl.FirstLeft;
            outRec.FirstLeft = orfl;
        }

        //------------------------------------------------------------------------------

        private bool Executeprotected ()
        {
            try
            {
                Reset();
                if (m_CurrentLM == null) return false;

                var botY = PopScanbeam();
                do
                {
                    InsertLocalMinimaIntoAEL(botY);
                    m_GhostJoins.Clear();
                    ProcessHorizontals(false);
                    if (m_Scanbeam == null) break;
                    var topY = PopScanbeam();
                    if (!ProcessIntersections(topY)) return false;
                    ProcessEdgesAtTopOfScanbeam(topY);
                    botY = topY;
                } while (m_Scanbeam != null || m_CurrentLM != null);

                //fix orientations ...
                foreach (var outRec in m_PolyOuts)
                {
                    if (outRec.Pts == null || outRec.IsOpen) continue;
                    if ((outRec.IsHole ^ ReverseSolution) == (Area(outRec) > 0))
                        ReversePolyPtLinks(outRec.Pts);
                }

                JoinCommonEdges();

                foreach (var outRec in m_PolyOuts)
                {
                    if (outRec.Pts != null && !outRec.IsOpen)
                        FixupOutPolygon(outRec);
                }

                if (StrictlySimple) DoSimplePolygons();
                return true;
            }
                //catch { return false; }
            finally
            {
                m_Joins.Clear();
                m_GhostJoins.Clear();
            }
        }

        //------------------------------------------------------------------------------

        private double PopScanbeam()
        {
            var Y = m_Scanbeam.Y;
            m_Scanbeam = m_Scanbeam.Next;
            return Y;
        }

        //------------------------------------------------------------------------------

        private void DisposeAllPolyPts()
        {
            for (var i = 0; i < m_PolyOuts.Count; ++i) DisposeOutRec(i);
            m_PolyOuts.Clear();
        }

        //------------------------------------------------------------------------------

        private void DisposeOutRec(int index)
        {
            var outRec = m_PolyOuts[index];
            outRec.Pts = null;
            m_PolyOuts[index] = null;
        }

        //------------------------------------------------------------------------------

        private void AddJoin(OutPt Op1, OutPt Op2, Point OffPt)
        {
            var j = new Join
            {
                OutPt1 = Op1,
                OutPt2 = Op2,
                OffPt = OffPt
            };
            m_Joins.Add(j);
        }

        //------------------------------------------------------------------------------

        private void AddGhostJoin(OutPt Op, Point OffPt)
        {
            var j = new Join
            {
                OutPt1 = Op,
                OffPt = OffPt
            };
            m_GhostJoins.Add(j);
        }

        //------------------------------------------------------------------------------


        private void InsertLocalMinimaIntoAEL(double botY)
        {
            while (m_CurrentLM != null && (Math.Abs(m_CurrentLM.Y - botY) < double.Epsilon))
            {
                var lb = m_CurrentLM.LeftBound;
                var rb = m_CurrentLM.RightBound;
                PopLocalMinima();

                OutPt Op1 = null;
                if (lb == null)
                {
                    InsertEdgeIntoAEL(rb, null);
                    SetWindingCount(rb);
                    if (IsContributing(rb))
                        Op1 = AddOutPt(rb, rb.Bot);
                }
                else if (rb == null)
                {
                    InsertEdgeIntoAEL(lb, null);
                    SetWindingCount(lb);
                    if (IsContributing(lb))
                        Op1 = AddOutPt(lb, lb.Bot);
                    InsertScanbeam(lb.Top.Y);
                }
                else
                {
                    InsertEdgeIntoAEL(lb, null);
                    InsertEdgeIntoAEL(rb, lb);
                    SetWindingCount(lb);
                    rb.WindCnt = lb.WindCnt;
                    rb.WindCnt2 = lb.WindCnt2;
                    if (IsContributing(lb))
                        Op1 = AddLocalMinPoly(lb, rb, lb.Bot);
                    InsertScanbeam(lb.Top.Y);
                }

                if (rb != null)
                {
                    if (IsHorizontal(rb))
                        AddEdgeToSEL(rb);
                    else
                        InsertScanbeam(rb.Top.Y);
                }

                if (lb == null || rb == null) continue;

                //if output polygons share an Edge with a horizontal rb, they'll need joining later ...
                if (Op1 != null && IsHorizontal(rb) &&
                    m_GhostJoins.Count > 0 && rb.WindDelta != 0)
                {
                    foreach (var j in m_GhostJoins)
                    {
                        if (HorzSegmentsOverlap(j.OutPt1.Pt.X, j.OffPt.X, rb.Bot.X, rb.Top.X))
                            AddJoin(j.OutPt1, Op1, j.OffPt);
                    }
                }

                if (lb.OutIdx >= 0 && lb.PrevInAEL != null &&
                    Math.Abs(lb.PrevInAEL.Curr.X - lb.Bot.X) < double.Epsilon &&
                    lb.PrevInAEL.OutIdx >= 0 &&
                    SlopesEqual(lb.PrevInAEL, lb) &&
                    lb.WindDelta != 0 && lb.PrevInAEL.WindDelta != 0)
                {
                    var Op2 = AddOutPt(lb.PrevInAEL, lb.Bot);
                    AddJoin(Op1, Op2, lb.Top);
                }

                if (lb.NextInAEL != rb)
                {
                    if (rb.OutIdx >= 0 && rb.PrevInAEL.OutIdx >= 0 &&
                        SlopesEqual(rb.PrevInAEL, rb) &&
                        rb.WindDelta != 0 && rb.PrevInAEL.WindDelta != 0)
                    {
                        var Op2 = AddOutPt(rb.PrevInAEL, rb.Bot);
                        AddJoin(Op1, Op2, rb.Top);
                    }

                    var e = lb.NextInAEL;
                    if (e != null)
                        while (e != rb)
                        {
                            //nb: For calculating winding counts etc, IntersectEdges() assumes
                            //that param1 will be to the right of param2 ABOVE the intersection ...
                            IntersectEdges(rb, e, lb.Curr); //order important here
                            e = e.NextInAEL;
                        }
                }
            }
        }

        //------------------------------------------------------------------------------

        private void InsertEdgeIntoAEL(TEdge edge, TEdge startEdge)
        {
            if (m_ActiveEdges == null)
            {
                edge.PrevInAEL = null;
                edge.NextInAEL = null;
                m_ActiveEdges = edge;
            }
            else if (startEdge == null && E2InsertsBeforeE1(m_ActiveEdges, edge))
            {
                edge.PrevInAEL = null;
                edge.NextInAEL = m_ActiveEdges;
                m_ActiveEdges.PrevInAEL = edge;
                m_ActiveEdges = edge;
            }
            else
            {
                if (startEdge == null) startEdge = m_ActiveEdges;
                while (startEdge.NextInAEL != null &&
                       !E2InsertsBeforeE1(startEdge.NextInAEL, edge))
                    startEdge = startEdge.NextInAEL;
                edge.NextInAEL = startEdge.NextInAEL;
                if (startEdge.NextInAEL != null) startEdge.NextInAEL.PrevInAEL = edge;
                edge.PrevInAEL = startEdge;
                startEdge.NextInAEL = edge;
            }
        }

        //----------------------------------------------------------------------

        private bool E2InsertsBeforeE1(TEdge e1, TEdge e2)
        {
            if (Math.Abs(e2.Curr.X - e1.Curr.X) < double.Epsilon)
            {
                if (e2.Top.Y > e1.Top.Y)
                    return e2.Top.X < TopX(e1, e2.Top.Y);
                else return e1.Top.X > TopX(e2, e1.Top.Y);
            }
            else return e2.Curr.X < e1.Curr.X;
        }

        //------------------------------------------------------------------------------

        private bool IsEvenOddFillType(TEdge edge)
        {
            if (edge.PolyTyp == PolyType.Subject)
                return m_SubjFillType == PolyFillType.EvenOdd;
            else
                return m_ClipFillType == PolyFillType.EvenOdd;
        }

        //------------------------------------------------------------------------------

        private bool IsEvenOddAltFillType(TEdge edge)
        {
            if (edge.PolyTyp == PolyType.Subject)
                return m_ClipFillType == PolyFillType.EvenOdd;
            else
                return m_SubjFillType == PolyFillType.EvenOdd;
        }

        //------------------------------------------------------------------------------

        private bool IsContributing(TEdge edge)
        {
            PolyFillType pft, pft2;
            if (edge.PolyTyp == PolyType.Subject)
            {
                pft = m_SubjFillType;
                pft2 = m_ClipFillType;
            }
            else
            {
                pft = m_ClipFillType;
                pft2 = m_SubjFillType;
            }

            switch (pft)
            {
                case PolyFillType.EvenOdd:
                    //return false if a subj line has been flagged as inside a subj polygon
                    if (edge.WindDelta == 0 && edge.WindCnt != 1) return false;
                    break;
                case PolyFillType.NonZero:
                    if (Math.Abs(edge.WindCnt) != 1) return false;
                    break;
                case PolyFillType.Positive:
                    if (edge.WindCnt != 1) return false;
                    break;
                default: //PolyFillType.pftNegative
                    if (edge.WindCnt != -1) return false;
                    break;
            }

            switch (m_ClipType)
            {
                case ClipType.Intersection:
                    switch (pft2)
                    {
                        case PolyFillType.EvenOdd:
                        case PolyFillType.NonZero:
                            return (edge.WindCnt2 != 0);
                        case PolyFillType.Positive:
                            return (edge.WindCnt2 > 0);
                        default:
                            return (edge.WindCnt2 < 0);
                    }
                case ClipType.Union:
                    switch (pft2)
                    {
                        case PolyFillType.EvenOdd:
                        case PolyFillType.NonZero:
                            return (edge.WindCnt2 == 0);
                        case PolyFillType.Positive:
                            return (edge.WindCnt2 <= 0);
                        default:
                            return (edge.WindCnt2 >= 0);
                    }
                case ClipType.Difference:
                    if (edge.PolyTyp == PolyType.Subject)
                        switch (pft2)
                        {
                            case PolyFillType.EvenOdd:
                            case PolyFillType.NonZero:
                                return (edge.WindCnt2 == 0);
                            case PolyFillType.Positive:
                                return (edge.WindCnt2 <= 0);
                            default:
                                return (edge.WindCnt2 >= 0);
                        }
                    else
                        switch (pft2)
                        {
                            case PolyFillType.EvenOdd:
                            case PolyFillType.NonZero:
                                return (edge.WindCnt2 != 0);
                            case PolyFillType.Positive:
                                return (edge.WindCnt2 > 0);
                            default:
                                return (edge.WindCnt2 < 0);
                        }
                case ClipType.Xor:
                    if (edge.WindDelta == 0) //XOr always contributing unless open
                        switch (pft2)
                        {
                            case PolyFillType.EvenOdd:
                            case PolyFillType.NonZero:
                                return (edge.WindCnt2 == 0);
                            case PolyFillType.Positive:
                                return (edge.WindCnt2 <= 0);
                            default:
                                return (edge.WindCnt2 >= 0);
                        }
                    else
                        return true;
            }
            return true;
        }

        //------------------------------------------------------------------------------

        private void SetWindingCount(TEdge edge)
        {
            var e = edge.PrevInAEL;
            //find the edge of the same polytype that immediately preceeds 'edge' in AEL
            while (e != null && ((e.PolyTyp != edge.PolyTyp) || (e.WindDelta == 0))) e = e.PrevInAEL;
            if (e == null)
            {
                edge.WindCnt = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
                edge.WindCnt2 = 0;
                e = m_ActiveEdges; //ie get ready to calc WindCnt2
            }
            else if (edge.WindDelta == 0 && m_ClipType != ClipType.Union)
            {
                edge.WindCnt = 1;
                edge.WindCnt2 = e.WindCnt2;
                e = e.NextInAEL; //ie get ready to calc WindCnt2
            }
            else if (IsEvenOddFillType(edge))
            {
                //EvenOdd filling ...
                if (edge.WindDelta == 0)
                {
                    //are we inside a subj polygon ...
                    var Inside = true;
                    var e2 = e.PrevInAEL;
                    while (e2 != null)
                    {
                        if (e2.PolyTyp == e.PolyTyp && e2.WindDelta != 0)
                            Inside = !Inside;
                        e2 = e2.PrevInAEL;
                    }
                    edge.WindCnt = (Inside ? 0 : 1);
                }
                else
                {
                    edge.WindCnt = edge.WindDelta;
                }
                edge.WindCnt2 = e.WindCnt2;
                e = e.NextInAEL; //ie get ready to calc WindCnt2
            }
            else
            {
                //nonZero, Positive or Negative filling ...
                if (e.WindCnt*e.WindDelta < 0)
                {
                    //prev edge is 'decreasing' WindCount (WC) toward zero
                    //so we're outside the previous polygon ...
                    if (Math.Abs(e.WindCnt) > 1)
                    {
                        //outside prev poly but still inside another.
                        //when reversing direction of prev poly use the same WC 
                        if (e.WindDelta*edge.WindDelta < 0) edge.WindCnt = e.WindCnt;
                        //otherwise continue to 'decrease' WC ...
                        else edge.WindCnt = e.WindCnt + edge.WindDelta;
                    }
                    else
                    //now outside all polys of same polytype so set own WC ...
                        edge.WindCnt = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
                }
                else
                {
                    //prev edge is 'increasing' WindCount (WC) away from zero
                    //so we're inside the previous polygon ...
                    if (edge.WindDelta == 0)
                        edge.WindCnt = (e.WindCnt < 0 ? e.WindCnt - 1 : e.WindCnt + 1);
                    //if wind direction is reversing prev then use same WC
                    else if (e.WindDelta*edge.WindDelta < 0)
                        edge.WindCnt = e.WindCnt;
                    //otherwise add to WC ...
                    else edge.WindCnt = e.WindCnt + edge.WindDelta;
                }
                edge.WindCnt2 = e.WindCnt2;
                e = e.NextInAEL; //ie get ready to calc WindCnt2
            }

            //update WindCnt2 ...
            if (IsEvenOddAltFillType(edge))
            {
                //EvenOdd filling ...
                while (e != edge)
                {
                    if (e.WindDelta != 0)
                        edge.WindCnt2 = (edge.WindCnt2 == 0 ? 1 : 0);
                    e = e.NextInAEL;
                }
            }
            else
            {
                //nonZero, Positive or Negative filling ...
                while (e != edge)
                {
                    edge.WindCnt2 += e.WindDelta;
                    e = e.NextInAEL;
                }
            }
        }

        //------------------------------------------------------------------------------

        private void AddEdgeToSEL(TEdge edge)
        {
            //SEL pointers in PEdge are reused to build a list of horizontal edges.
            //However, we don't need to worry about order with horizontal edge processing.
            if (m_SortedEdges == null)
            {
                m_SortedEdges = edge;
                edge.PrevInSEL = null;
                edge.NextInSEL = null;
            }
            else
            {
                edge.NextInSEL = m_SortedEdges;
                edge.PrevInSEL = null;
                m_SortedEdges.PrevInSEL = edge;
                m_SortedEdges = edge;
            }
        }

        //------------------------------------------------------------------------------

        private void CopyAELToSEL()
        {
            var e = m_ActiveEdges;
            m_SortedEdges = e;
            while (e != null)
            {
                e.PrevInSEL = e.PrevInAEL;
                e.NextInSEL = e.NextInAEL;
                e = e.NextInAEL;
            }
        }

        //------------------------------------------------------------------------------

        private void SwapPositionsInAEL(TEdge edge1, TEdge edge2)
        {
            //check that one or other edge hasn't already been removed from AEL ...
            if (edge1.NextInAEL == edge1.PrevInAEL ||
                edge2.NextInAEL == edge2.PrevInAEL) return;

            if (edge1.NextInAEL == edge2)
            {
                var next = edge2.NextInAEL;
                if (next != null)
                    next.PrevInAEL = edge1;
                var prev = edge1.PrevInAEL;
                if (prev != null)
                    prev.NextInAEL = edge2;
                edge2.PrevInAEL = prev;
                edge2.NextInAEL = edge1;
                edge1.PrevInAEL = edge2;
                edge1.NextInAEL = next;
            }
            else if (edge2.NextInAEL == edge1)
            {
                var next = edge1.NextInAEL;
                if (next != null)
                    next.PrevInAEL = edge2;
                var prev = edge2.PrevInAEL;
                if (prev != null)
                    prev.NextInAEL = edge1;
                edge1.PrevInAEL = prev;
                edge1.NextInAEL = edge2;
                edge2.PrevInAEL = edge1;
                edge2.NextInAEL = next;
            }
            else
            {
                var next = edge1.NextInAEL;
                var prev = edge1.PrevInAEL;
                edge1.NextInAEL = edge2.NextInAEL;
                if (edge1.NextInAEL != null)
                    edge1.NextInAEL.PrevInAEL = edge1;
                edge1.PrevInAEL = edge2.PrevInAEL;
                if (edge1.PrevInAEL != null)
                    edge1.PrevInAEL.NextInAEL = edge1;
                edge2.NextInAEL = next;
                if (edge2.NextInAEL != null)
                    edge2.NextInAEL.PrevInAEL = edge2;
                edge2.PrevInAEL = prev;
                if (edge2.PrevInAEL != null)
                    edge2.PrevInAEL.NextInAEL = edge2;
            }

            if (edge1.PrevInAEL == null)
                m_ActiveEdges = edge1;
            else if (edge2.PrevInAEL == null)
                m_ActiveEdges = edge2;
        }

        //------------------------------------------------------------------------------

        private void SwapPositionsInSEL(TEdge edge1, TEdge edge2)
        {
            if (edge1.NextInSEL == null && edge1.PrevInSEL == null)
                return;
            if (edge2.NextInSEL == null && edge2.PrevInSEL == null)
                return;

            if (edge1.NextInSEL == edge2)
            {
                var next = edge2.NextInSEL;
                if (next != null)
                    next.PrevInSEL = edge1;
                var prev = edge1.PrevInSEL;
                if (prev != null)
                    prev.NextInSEL = edge2;
                edge2.PrevInSEL = prev;
                edge2.NextInSEL = edge1;
                edge1.PrevInSEL = edge2;
                edge1.NextInSEL = next;
            }
            else if (edge2.NextInSEL == edge1)
            {
                var next = edge1.NextInSEL;
                if (next != null)
                    next.PrevInSEL = edge2;
                var prev = edge2.PrevInSEL;
                if (prev != null)
                    prev.NextInSEL = edge1;
                edge1.PrevInSEL = prev;
                edge1.NextInSEL = edge2;
                edge2.PrevInSEL = edge1;
                edge2.NextInSEL = next;
            }
            else
            {
                var next = edge1.NextInSEL;
                var prev = edge1.PrevInSEL;
                edge1.NextInSEL = edge2.NextInSEL;
                if (edge1.NextInSEL != null)
                    edge1.NextInSEL.PrevInSEL = edge1;
                edge1.PrevInSEL = edge2.PrevInSEL;
                if (edge1.PrevInSEL != null)
                    edge1.PrevInSEL.NextInSEL = edge1;
                edge2.NextInSEL = next;
                if (edge2.NextInSEL != null)
                    edge2.NextInSEL.PrevInSEL = edge2;
                edge2.PrevInSEL = prev;
                if (edge2.PrevInSEL != null)
                    edge2.PrevInSEL.NextInSEL = edge2;
            }

            if (edge1.PrevInSEL == null)
                m_SortedEdges = edge1;
            else if (edge2.PrevInSEL == null)
                m_SortedEdges = edge2;
        }

        //------------------------------------------------------------------------------


        private void AddLocalMaxPoly(TEdge e1, TEdge e2, Point pt)
        {
            AddOutPt(e1, pt);
            if (e2.WindDelta == 0) AddOutPt(e2, pt);
            if (e1.OutIdx == e2.OutIdx)
            {
                e1.OutIdx = Unassigned;
                e2.OutIdx = Unassigned;
            }
            else if (e1.OutIdx < e2.OutIdx)
                AppendPolygon(e1, e2);
            else
                AppendPolygon(e2, e1);
        }

        //------------------------------------------------------------------------------

        private OutPt AddLocalMinPoly(TEdge e1, TEdge e2, Point pt)
        {
            OutPt result;
            TEdge e, prevE;
            if (IsHorizontal(e2) || (e1.Dx > e2.Dx))
            {
                result = AddOutPt(e1, pt);
                e2.OutIdx = e1.OutIdx;
                e1.Side = EdgeSide.Left;
                e2.Side = EdgeSide.Right;
                e = e1;
                prevE = e.PrevInAEL == e2 ? e2.PrevInAEL : e.PrevInAEL;
            }
            else
            {
                result = AddOutPt(e2, pt);
                e1.OutIdx = e2.OutIdx;
                e1.Side = EdgeSide.Right;
                e2.Side = EdgeSide.Left;
                e = e2;
                prevE = e.PrevInAEL == e1 ? e1.PrevInAEL : e.PrevInAEL;
            }

            if (prevE != null && prevE.OutIdx >= 0 &&
                (Math.Abs(TopX(prevE, pt.Y) - TopX(e, pt.Y)) < double.Epsilon) &&
                SlopesEqual(e, prevE) &&
                (e.WindDelta != 0) && (prevE.WindDelta != 0))
            {
                var outPt = AddOutPt(prevE, pt);
                AddJoin(result, outPt, e.Top);
            }
            return result;
        }

        //------------------------------------------------------------------------------

        private OutRec CreateOutRec()
        {
            var result = new OutRec
            {
                Idx = Unassigned,
                IsHole = false,
                IsOpen = false,
                FirstLeft = null,
                Pts = null,
                BottomPt = null
            };

            m_PolyOuts.Add(result);
            result.Idx = m_PolyOuts.Count - 1;
            return result;
        }

        //------------------------------------------------------------------------------

        private OutPt AddOutPt(TEdge e, Point pt)
        {
            var ToFront = (e.Side == EdgeSide.Left);
            if (e.OutIdx < 0)
            {
                var outRec = CreateOutRec();
                outRec.IsOpen = (e.WindDelta == 0);
                var newOp = new OutPt();
                outRec.Pts = newOp;
                newOp.Idx = outRec.Idx;
                newOp.Pt = pt;
                newOp.Next = newOp;
                newOp.Prev = newOp;
                if (!outRec.IsOpen)
                    SetHoleState(e, outRec);
                e.OutIdx = outRec.Idx; //nb: do this after SetZ !
                return newOp;
            }
            else
            {
                var outRec = m_PolyOuts[e.OutIdx];
                //OutRec.Pts is the 'Left-most' point & OutRec.Pts.Prev is the 'Right-most'
                var op = outRec.Pts;
                if (ToFront && pt == op.Pt) return op;
                else if (!ToFront && pt == op.Prev.Pt) return op.Prev;

                var newOp = new OutPt
                {
                    Idx = outRec.Idx,
                    Pt = pt,
                    Next = op,
                    Prev = op.Prev
                };
                newOp.Prev.Next = newOp;
                op.Prev = newOp;
                if (ToFront) outRec.Pts = newOp;
                return newOp;
            }
        }

        //------------------------------------------------------------------------------

        protected  void SwapPoints(ref Point pt1, ref Point pt2)
        {
            var tmp = new Point(pt1.X, pt1.Y);
            pt1 = pt2;
            pt2 = tmp;
        }

        //------------------------------------------------------------------------------

        private bool HorzSegmentsOverlap(double seg1a, double seg1b, double seg2a, double seg2b)
        {
            if (seg1a > seg1b) GeneralHelpers.Swap(ref seg1a, ref seg1b);
            if (seg2a > seg2b) GeneralHelpers.Swap(ref seg2a, ref seg2b);

            return (seg1a < seg2b) && (seg2a < seg1b);
        }

        //------------------------------------------------------------------------------

        private void SetHoleState(TEdge e, OutRec outRec)
        {
            var isHole = false;
            var e2 = e.PrevInAEL;
            while (e2 != null)
            {
                if (e2.OutIdx >= 0 && e2.WindDelta != 0)
                {
                    isHole = !isHole;
                    if (outRec.FirstLeft == null)
                        outRec.FirstLeft = m_PolyOuts[e2.OutIdx];
                }
                e2 = e2.PrevInAEL;
            }
            if (isHole)
                outRec.IsHole = true;
        }

        //------------------------------------------------------------------------------

        private double GetDx(Point pt1, Point pt2)
        {
            if (Math.Abs(pt1.Y - pt2.Y) < double.Epsilon) return horizontal;
            else return (pt2.X - pt1.X)/(pt2.Y - pt1.Y);
        }

        //---------------------------------------------------------------------------

        private bool FirstIsBottomPt(OutPt btmPt1, OutPt btmPt2)
        {
            var p = btmPt1.Prev;
            while ((p.Pt == btmPt1.Pt) && (p != btmPt1)) p = p.Prev;
            var dx1p = Math.Abs(GetDx(btmPt1.Pt, p.Pt));
            p = btmPt1.Next;
            while ((p.Pt == btmPt1.Pt) && (p != btmPt1)) p = p.Next;
            var dx1n = Math.Abs(GetDx(btmPt1.Pt, p.Pt));

            p = btmPt2.Prev;
            while ((p.Pt == btmPt2.Pt) && (p != btmPt2)) p = p.Prev;
            var dx2p = Math.Abs(GetDx(btmPt2.Pt, p.Pt));
            p = btmPt2.Next;
            while ((p.Pt == btmPt2.Pt) && (p != btmPt2)) p = p.Next;
            var dx2n = Math.Abs(GetDx(btmPt2.Pt, p.Pt));
            return (dx1p >= dx2p && dx1p >= dx2n) || (dx1n >= dx2p && dx1n >= dx2n);
        }

        //------------------------------------------------------------------------------

        private OutPt GetBottomPt(OutPt pp)
        {
            OutPt dups = null;
            var p = pp.Next;
            while (p != pp)
            {
                if (p.Pt.Y > pp.Pt.Y)
                {
                    pp = p;
                    dups = null;
                }
                else if (Math.Abs(p.Pt.Y - pp.Pt.Y) < double.Epsilon && p.Pt.X <= pp.Pt.X)
                {
                    if (p.Pt.X < pp.Pt.X)
                    {
                        dups = null;
                        pp = p;
                    }
                    else
                    {
                        if (p.Next != pp && p.Prev != pp) dups = p;
                    }
                }
                p = p.Next;
            }
            if (dups != null)
            {
                //there appears to be at least 2 vertices at bottomPt so ...
                while (dups != p)
                {
                    if (!FirstIsBottomPt(p, dups)) pp = dups;
                    dups = dups.Next;
                    while (dups.Pt != pp.Pt) dups = dups.Next;
                }
            }
            return pp;
        }

        //------------------------------------------------------------------------------

        private OutRec GetLowermostRec(OutRec outRec1, OutRec outRec2)
        {
            //work out which polygon fragment has the correct hole state ...
            if (outRec1.BottomPt == null)
                outRec1.BottomPt = GetBottomPt(outRec1.Pts);
            if (outRec2.BottomPt == null)
                outRec2.BottomPt = GetBottomPt(outRec2.Pts);
            var bPt1 = outRec1.BottomPt;
            var bPt2 = outRec2.BottomPt;
            if (bPt1.Pt.Y > bPt2.Pt.Y) return outRec1;
            else if (bPt1.Pt.Y < bPt2.Pt.Y) return outRec2;
            else if (bPt1.Pt.X < bPt2.Pt.X) return outRec1;
            else if (bPt1.Pt.X > bPt2.Pt.X) return outRec2;
            else if (bPt1.Next == bPt1) return outRec2;
            else if (bPt2.Next == bPt2) return outRec1;
            else if (FirstIsBottomPt(bPt1, bPt2)) return outRec1;
            else return outRec2;
        }

        //------------------------------------------------------------------------------

        private bool Param1RightOfParam2(OutRec outRec1, OutRec outRec2)
        {
            do
            {
                outRec1 = outRec1.FirstLeft;
                if (outRec1 == outRec2) return true;
            } while (outRec1 != null);
            return false;
        }

        //------------------------------------------------------------------------------

        private OutRec GetOutRec(int idx)
        {
            var outrec = m_PolyOuts[idx];
            while (outrec != m_PolyOuts[outrec.Idx])
                outrec = m_PolyOuts[outrec.Idx];
            return outrec;
        }

        //------------------------------------------------------------------------------

        private void AppendPolygon(TEdge e1, TEdge e2)
        {
            //get the start and ends of both output polygons ...
            var outRec1 = m_PolyOuts[e1.OutIdx];
            var outRec2 = m_PolyOuts[e2.OutIdx];

            OutRec holeStateRec;
            if (Param1RightOfParam2(outRec1, outRec2))
                holeStateRec = outRec2;
            else if (Param1RightOfParam2(outRec2, outRec1))
                holeStateRec = outRec1;
            else
                holeStateRec = GetLowermostRec(outRec1, outRec2);

            var p1_lft = outRec1.Pts;
            var p1_rt = p1_lft.Prev;
            var p2_lft = outRec2.Pts;
            var p2_rt = p2_lft.Prev;

            EdgeSide side;
            //join e2 poly onto e1 poly and delete pointers to e2 ...
            if (e1.Side == EdgeSide.Left)
            {
                if (e2.Side == EdgeSide.Left)
                {
                    //z y x a b c
                    ReversePolyPtLinks(p2_lft);
                    p2_lft.Next = p1_lft;
                    p1_lft.Prev = p2_lft;
                    p1_rt.Next = p2_rt;
                    p2_rt.Prev = p1_rt;
                    outRec1.Pts = p2_rt;
                }
                else
                {
                    //x y z a b c
                    p2_rt.Next = p1_lft;
                    p1_lft.Prev = p2_rt;
                    p2_lft.Prev = p1_rt;
                    p1_rt.Next = p2_lft;
                    outRec1.Pts = p2_lft;
                }
                side = EdgeSide.Left;
            }
            else
            {
                if (e2.Side == EdgeSide.Right)
                {
                    //a b c z y x
                    ReversePolyPtLinks(p2_lft);
                    p1_rt.Next = p2_rt;
                    p2_rt.Prev = p1_rt;
                    p2_lft.Next = p1_lft;
                    p1_lft.Prev = p2_lft;
                }
                else
                {
                    //a b c x y z
                    p1_rt.Next = p2_lft;
                    p2_lft.Prev = p1_rt;
                    p1_lft.Prev = p2_rt;
                    p2_rt.Next = p1_lft;
                }
                side = EdgeSide.Right;
            }

            outRec1.BottomPt = null;
            if (holeStateRec == outRec2)
            {
                if (outRec2.FirstLeft != outRec1)
                    outRec1.FirstLeft = outRec2.FirstLeft;
                outRec1.IsHole = outRec2.IsHole;
            }
            outRec2.Pts = null;
            outRec2.BottomPt = null;

            outRec2.FirstLeft = outRec1;

            var OKIdx = e1.OutIdx;
            var ObsoleteIdx = e2.OutIdx;

            e1.OutIdx = Unassigned; //nb: safe because we only get here via AddLocalMaxPoly
            e2.OutIdx = Unassigned;

            var e = m_ActiveEdges;
            while (e != null)
            {
                if (e.OutIdx == ObsoleteIdx)
                {
                    e.OutIdx = OKIdx;
                    e.Side = side;
                    break;
                }
                e = e.NextInAEL;
            }
            outRec2.Idx = outRec1.Idx;
        }

        //------------------------------------------------------------------------------

        private void ReversePolyPtLinks(OutPt pp)
        {
            if (pp == null) return;
            var pp1 = pp;
            do
            {
                var pp2 = pp1.Next;
                pp1.Next = pp1.Prev;
                pp1.Prev = pp2;
                pp1 = pp2;
            } while (pp1 != pp);
        }

        //------------------------------------------------------------------------------

        private static void SwapSides(TEdge edge1, TEdge edge2)
        {
            var side = edge1.Side;
            edge1.Side = edge2.Side;
            edge2.Side = side;
        }

        //------------------------------------------------------------------------------

        private static void SwapPolyIndexes(TEdge edge1, TEdge edge2)
        {
            var outIdx = edge1.OutIdx;
            edge1.OutIdx = edge2.OutIdx;
            edge2.OutIdx = outIdx;
        }

        //------------------------------------------------------------------------------

        private void IntersectEdges(TEdge e1, TEdge e2, Point pt)
        {
            //e1 will be to the left of e2 BELOW the intersection. Therefore e1 is before
            //e2 in AEL except when e1 is being inserted at the intersection point ...

            var e1Contributing = (e1.OutIdx >= 0);
            var e2Contributing = (e2.OutIdx >= 0);

            //update winding counts...
            //assumes that e1 will be to the Right of e2 ABOVE the intersection
            if (e1.PolyTyp == e2.PolyTyp)
            {
                if (IsEvenOddFillType(e1))
                {
                    var oldE1WindCnt = e1.WindCnt;
                    e1.WindCnt = e2.WindCnt;
                    e2.WindCnt = oldE1WindCnt;
                }
                else
                {
                    if (e1.WindCnt + e2.WindDelta == 0) e1.WindCnt = -e1.WindCnt;
                    else e1.WindCnt += e2.WindDelta;
                    if (e2.WindCnt - e1.WindDelta == 0) e2.WindCnt = -e2.WindCnt;
                    else e2.WindCnt -= e1.WindDelta;
                }
            }
            else
            {
                if (!IsEvenOddFillType(e2)) e1.WindCnt2 += e2.WindDelta;
                else e1.WindCnt2 = (e1.WindCnt2 == 0) ? 1 : 0;
                if (!IsEvenOddFillType(e1)) e2.WindCnt2 -= e1.WindDelta;
                else e2.WindCnt2 = (e2.WindCnt2 == 0) ? 1 : 0;
            }

            PolyFillType e1FillType, e2FillType, e1FillType2, e2FillType2;
            if (e1.PolyTyp == PolyType.Subject)
            {
                e1FillType = m_SubjFillType;
                e1FillType2 = m_ClipFillType;
            }
            else
            {
                e1FillType = m_ClipFillType;
                e1FillType2 = m_SubjFillType;
            }
            if (e2.PolyTyp == PolyType.Subject)
            {
                e2FillType = m_SubjFillType;
                e2FillType2 = m_ClipFillType;
            }
            else
            {
                e2FillType = m_ClipFillType;
                e2FillType2 = m_SubjFillType;
            }

            int e1Wc, e2Wc;
            switch (e1FillType)
            {
                case PolyFillType.Positive:
                    e1Wc = e1.WindCnt;
                    break;
                case PolyFillType.Negative:
                    e1Wc = -e1.WindCnt;
                    break;
                default:
                    e1Wc = Math.Abs(e1.WindCnt);
                    break;
            }
            switch (e2FillType)
            {
                case PolyFillType.Positive:
                    e2Wc = e2.WindCnt;
                    break;
                case PolyFillType.Negative:
                    e2Wc = -e2.WindCnt;
                    break;
                default:
                    e2Wc = Math.Abs(e2.WindCnt);
                    break;
            }

            if (e1Contributing && e2Contributing)
            {
                if ((e1Wc != 0 && e1Wc != 1) || (e2Wc != 0 && e2Wc != 1) ||
                    (e1.PolyTyp != e2.PolyTyp && m_ClipType != ClipType.Xor))
                {
                    AddLocalMaxPoly(e1, e2, pt);
                }
                else
                {
                    AddOutPt(e1, pt);
                    AddOutPt(e2, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if (e1Contributing)
            {
                if (e2Wc == 0 || e2Wc == 1)
                {
                    AddOutPt(e1, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if (e2Contributing)
            {
                if (e1Wc == 0 || e1Wc == 1)
                {
                    AddOutPt(e2, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if ((e1Wc == 0 || e1Wc == 1) && (e2Wc == 0 || e2Wc == 1))
            {
                //neither edge is currently contributing ...
                long e1Wc2, e2Wc2;
                switch (e1FillType2)
                {
                    case PolyFillType.Positive:
                        e1Wc2 = e1.WindCnt2;
                        break;
                    case PolyFillType.Negative:
                        e1Wc2 = -e1.WindCnt2;
                        break;
                    default:
                        e1Wc2 = Math.Abs(e1.WindCnt2);
                        break;
                }
                switch (e2FillType2)
                {
                    case PolyFillType.Positive:
                        e2Wc2 = e2.WindCnt2;
                        break;
                    case PolyFillType.Negative:
                        e2Wc2 = -e2.WindCnt2;
                        break;
                    default:
                        e2Wc2 = Math.Abs(e2.WindCnt2);
                        break;
                }

                if (e1.PolyTyp != e2.PolyTyp)
                {
                    AddLocalMinPoly(e1, e2, pt);
                }
                else if (e1Wc == 1 && e2Wc == 1)
                    switch (m_ClipType)
                    {
                        case ClipType.Intersection:
                            if (e1Wc2 > 0 && e2Wc2 > 0)
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipType.Union:
                            if (e1Wc2 <= 0 && e2Wc2 <= 0)
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipType.Difference:
                            if (((e1.PolyTyp == PolyType.Clip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((e1.PolyTyp == PolyType.Subject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipType.Xor:
                            AddLocalMinPoly(e1, e2, pt);
                            break;
                    }
                else
                    SwapSides(e1, e2);
            }
        }

        //------------------------------------------------------------------------------

        private void DeleteFromAEL(TEdge e)
        {
            var AelPrev = e.PrevInAEL;
            var AelNext = e.NextInAEL;
            if (AelPrev == null && AelNext == null && (e != m_ActiveEdges))
                return; //already deleted
            if (AelPrev != null)
                AelPrev.NextInAEL = AelNext;
            else m_ActiveEdges = AelNext;
            if (AelNext != null)
                AelNext.PrevInAEL = AelPrev;
            e.NextInAEL = null;
            e.PrevInAEL = null;
        }

        //------------------------------------------------------------------------------

        private void DeleteFromSEL(TEdge e)
        {
            var SelPrev = e.PrevInSEL;
            var SelNext = e.NextInSEL;
            if (SelPrev == null && SelNext == null && (e != m_SortedEdges))
                return; //already deleted
            if (SelPrev != null)
                SelPrev.NextInSEL = SelNext;
            else m_SortedEdges = SelNext;
            if (SelNext != null)
                SelNext.PrevInSEL = SelPrev;
            e.NextInSEL = null;
            e.PrevInSEL = null;
        }

        //------------------------------------------------------------------------------

        private void UpdateEdgeIntoAEL(ref TEdge e)
        {
            if (e.NextInLML == null)
                throw new Exception("UpdateEdgeIntoAEL: invalid call");
            var AelPrev = e.PrevInAEL;
            var AelNext = e.NextInAEL;
            e.NextInLML.OutIdx = e.OutIdx;
            if (AelPrev != null)
                AelPrev.NextInAEL = e.NextInLML;
            else m_ActiveEdges = e.NextInLML;
            if (AelNext != null)
                AelNext.PrevInAEL = e.NextInLML;
            e.NextInLML.Side = e.Side;
            e.NextInLML.WindDelta = e.WindDelta;
            e.NextInLML.WindCnt = e.WindCnt;
            e.NextInLML.WindCnt2 = e.WindCnt2;
            e = e.NextInLML;
            e.Curr = e.Bot;
            e.PrevInAEL = AelPrev;
            e.NextInAEL = AelNext;
            if (!IsHorizontal(e)) InsertScanbeam(e.Top.Y);
        }

        //------------------------------------------------------------------------------

        private void ProcessHorizontals(bool isTopOfScanbeam)
        {
            var horzEdge = m_SortedEdges;
            while (horzEdge != null)
            {
                DeleteFromSEL(horzEdge);
                ProcessHorizontal(horzEdge, isTopOfScanbeam);
                horzEdge = m_SortedEdges;
            }
        }

        //------------------------------------------------------------------------------

        private void GetHorzDirection(TEdge horzEdge, out Direction dir, out double left, out double right)
        {
            if (horzEdge.Bot.X < horzEdge.Top.X)
            {
                left = horzEdge.Bot.X;
                right = horzEdge.Top.X;
                dir = Direction.LeftToRight;
            }
            else
            {
                left = horzEdge.Top.X;
                right = horzEdge.Bot.X;
                dir = Direction.RightToLeft;
            }
        }

        //------------------------------------------------------------------------

        private void ProcessHorizontal(TEdge horzEdge, bool isTopOfScanbeam)
        {
            Direction dir;
            double horzLeft, horzRight;

            GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

            TEdge eLastHorz = horzEdge, eMaxPair = null;
            while (eLastHorz.NextInLML != null && IsHorizontal(eLastHorz.NextInLML))
                eLastHorz = eLastHorz.NextInLML;
            if (eLastHorz.NextInLML == null)
                eMaxPair = GetMaximaPair(eLastHorz);

            for (;;)
            {
                var IsLastHorz = (horzEdge == eLastHorz);
                var e = GetNextInAEL(horzEdge, dir);
                while (e != null)
                {
                    //Break if we've got to the end of an intermediate horizontal edge ...
                    //nb: Smaller Dx's are to the right of larger Dx's ABOVE the horizontal.
                    if (Math.Abs(e.Curr.X - horzEdge.Top.X) < double.Epsilon && horzEdge.NextInLML != null &&
                        e.Dx < horzEdge.NextInLML.Dx) break;

                    var eNext = GetNextInAEL(e, dir); //saves eNext for later

                    if ((dir == Direction.LeftToRight && e.Curr.X <= horzRight) ||
                        (dir == Direction.RightToLeft && e.Curr.X >= horzLeft))
                    {
                        //so far we're still in range of the horizontal Edge  but make sure
                        //we're at the last of consec. horizontals when matching with eMaxPair
                        if (e == eMaxPair && IsLastHorz)
                        {
                            if (horzEdge.OutIdx >= 0)
                            {
                                var op1 = AddOutPt(horzEdge, horzEdge.Top);
                                var eNextHorz = m_SortedEdges;
                                while (eNextHorz != null)
                                {
                                    if (eNextHorz.OutIdx >= 0 &&
                                        HorzSegmentsOverlap(horzEdge.Bot.X,
                                            horzEdge.Top.X, eNextHorz.Bot.X, eNextHorz.Top.X))
                                    {
                                        var op2 = AddOutPt(eNextHorz, eNextHorz.Bot);
                                        AddJoin(op2, op1, eNextHorz.Top);
                                    }
                                    eNextHorz = eNextHorz.NextInSEL;
                                }
                                AddGhostJoin(op1, horzEdge.Bot);
                                AddLocalMaxPoly(horzEdge, eMaxPair, horzEdge.Top);
                            }
                            DeleteFromAEL(horzEdge);
                            DeleteFromAEL(eMaxPair);
                            return;
                        }
                        else if (dir == Direction.LeftToRight)
                        {
                            var Pt = new Point(e.Curr.X, horzEdge.Curr.Y);
                            IntersectEdges(horzEdge, e, Pt);
                        }
                        else
                        {
                            var Pt = new Point(e.Curr.X, horzEdge.Curr.Y);
                            IntersectEdges(e, horzEdge, Pt);
                        }
                        SwapPositionsInAEL(horzEdge, e);
                    }
                    else if ((dir == Direction.LeftToRight && e.Curr.X >= horzRight) ||
                             (dir == Direction.RightToLeft && e.Curr.X <= horzLeft)) break;
                    e = eNext;
                } //end while

                if (horzEdge.NextInLML != null && IsHorizontal(horzEdge.NextInLML))
                {
                    UpdateEdgeIntoAEL(ref horzEdge);
                    if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Bot);
                    GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);
                }
                else
                    break;
            } //end for (;;)

            if (horzEdge.NextInLML != null)
            {
                if (horzEdge.OutIdx >= 0)
                {
                    var op1 = AddOutPt(horzEdge, horzEdge.Top);
                    if (isTopOfScanbeam) AddGhostJoin(op1, horzEdge.Bot);

                    UpdateEdgeIntoAEL(ref horzEdge);
                    if (horzEdge.WindDelta == 0) return;
                    //nb: HorzEdge is no longer horizontal here
                    var ePrev = horzEdge.PrevInAEL;
                    var eNext = horzEdge.NextInAEL;
                    if (ePrev != null && Math.Abs(ePrev.Curr.X - horzEdge.Bot.X) < double.Epsilon && Math.Abs(ePrev.Curr.Y - horzEdge.Bot.Y) < double.Epsilon && ePrev.WindDelta != 0 &&
                        (ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
                         SlopesEqual(horzEdge, ePrev)))
                    {
                        var op2 = AddOutPt(ePrev, horzEdge.Bot);
                        AddJoin(op1, op2, horzEdge.Top);
                    }
                    else if (eNext != null && Math.Abs(eNext.Curr.X - horzEdge.Bot.X) < double.Epsilon && Math.Abs(eNext.Curr.Y - horzEdge.Bot.Y) < double.Epsilon && eNext.WindDelta != 0 &&
                             eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
                             SlopesEqual(horzEdge, eNext))
                    {
                        var op2 = AddOutPt(eNext, horzEdge.Bot);
                        AddJoin(op1, op2, horzEdge.Top);
                    }
                }
                else
                    UpdateEdgeIntoAEL(ref horzEdge);
            }
            else
            {
                if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Top);
                DeleteFromAEL(horzEdge);
            }
        }

        //------------------------------------------------------------------------------

        private TEdge GetNextInAEL(TEdge e, Direction Direction)
        {
            return Direction == Direction.LeftToRight ? e.NextInAEL : e.PrevInAEL;
        }

        //------------------------------------------------------------------------------

        private bool IsMinima(TEdge e)
        {
            return e != null && (e.Prev.NextInLML != e) && (e.Next.NextInLML != e);
        }

        //------------------------------------------------------------------------------

        private bool IsMaxima(TEdge e, double Y)
        {
            return (e != null && Math.Abs(e.Top.Y - Y) < double.Epsilon && e.NextInLML == null);
        }

        //------------------------------------------------------------------------------

        private bool IsIntermediate(TEdge e, double Y)
        {
            return (Math.Abs(e.Top.Y - Y) < double.Epsilon && e.NextInLML != null);
        }

        //------------------------------------------------------------------------------

        private TEdge GetMaximaPair(TEdge e)
        {
            TEdge result = null;
            if ((e.Next.Top == e.Top) && e.Next.NextInLML == null)
                result = e.Next;
            else if ((e.Prev.Top == e.Top) && e.Prev.NextInLML == null)
                result = e.Prev;
            if (result != null && (result.OutIdx == Skip ||
                                   (result.NextInAEL == result.PrevInAEL && !IsHorizontal(result))))
                return null;
            return result;
        }

        //------------------------------------------------------------------------------

        private bool ProcessIntersections(double topY)
        {
            if (m_ActiveEdges == null) return true;
            try
            {
                BuildIntersectList(topY);
                if (m_IntersectList.Count == 0) return true;
                if (m_IntersectList.Count == 1 || FixupIntersectionOrder())
                    ProcessIntersectList();
                else
                    return false;
            }
            catch
            {
                m_SortedEdges = null;
                m_IntersectList.Clear();
                throw new Exception("ProcessIntersections error");
            }
            m_SortedEdges = null;
            return true;
        }

        //------------------------------------------------------------------------------

        private void BuildIntersectList(double topY)
        {
            if (m_ActiveEdges == null) return;

            //prepare for sorting ...
            var e = m_ActiveEdges;
            m_SortedEdges = e;
            while (e != null)
            {
                e.PrevInSEL = e.PrevInAEL;
                e.NextInSEL = e.NextInAEL;
                e.Curr.X = TopX(e, topY);
                e = e.NextInAEL;
            }

            //bubblesort ...
            var isModified = true;
            while (isModified && m_SortedEdges != null)
            {
                isModified = false;
                e = m_SortedEdges;
                while (e.NextInSEL != null)
                {
                    var eNext = e.NextInSEL;
                    if (e.Curr.X > eNext.Curr.X)
                    {
                        Point pt;
                        IntersectPoint(e, eNext, out pt);
                        var newNode = new IntersectNode
                        {
                            Edge1 = e,
                            Edge2 = eNext,
                            Pt = pt
                        };
                        m_IntersectList.Add(newNode);

                        SwapPositionsInSEL(e, eNext);
                        isModified = true;
                    }
                    else
                        e = eNext;
                }
                if (e.PrevInSEL != null) e.PrevInSEL.NextInSEL = null;
                else break;
            }
            m_SortedEdges = null;
        }

        //------------------------------------------------------------------------------

        private bool EdgesAdjacent(IntersectNode inode)
        {
            return (inode.Edge1.NextInSEL == inode.Edge2) ||
                   (inode.Edge1.PrevInSEL == inode.Edge2);
        }

       
        private bool FixupIntersectionOrder()
        {
            //pre-condition: intersections are sorted bottom-most first.
            //Now it's crucial that intersections are made only between adjacent edges,
            //so to ensure this the order of intersections may need adjusting ...
            m_IntersectList.Sort(m_IntersectNodeComparer);

            CopyAELToSEL();
            var cnt = m_IntersectList.Count;
            for (var i = 0; i < cnt; i++)
            {
                if (!EdgesAdjacent(m_IntersectList[i]))
                {
                    var j = i + 1;
                    while (j < cnt && !EdgesAdjacent(m_IntersectList[j])) j++;
                    if (j == cnt) return false;

                    var tmp = m_IntersectList[i];
                    m_IntersectList[i] = m_IntersectList[j];
                    m_IntersectList[j] = tmp;
                }
                SwapPositionsInSEL(m_IntersectList[i].Edge1, m_IntersectList[i].Edge2);
            }
            return true;
        }

        //------------------------------------------------------------------------------

        private void ProcessIntersectList()
        {
            foreach (var iNode in m_IntersectList)
            {
                {
                    IntersectEdges(iNode.Edge1, iNode.Edge2, iNode.Pt);
                    SwapPositionsInAEL(iNode.Edge1, iNode.Edge2);
                }
            }
            m_IntersectList.Clear();
        }

        //------------------------------------------------------------------------------

        private static double TopX(TEdge edge, double currentY)
        {
            if (Math.Abs(currentY - edge.Top.Y) < double.Epsilon)
                return edge.Top.X;
            return edge.Bot.X + (edge.Dx*(currentY - edge.Bot.Y));
        }

        //------------------------------------------------------------------------------

        private void IntersectPoint(TEdge edge1, TEdge edge2, out Point ip)
        {
            ip = new Point();
            double b1, b2;
            //nb: with very large coordinate values, it's possible for SlopesEqual() to 
            //return false but for the edge.Dx value be equal due to double precision rounding.
            if (Math.Abs(edge1.Dx - edge2.Dx) < double.Epsilon)
            {
                ip.Y = edge1.Curr.Y;
                ip.X = TopX(edge1, ip.Y);
                return;
            }

            if (Math.Abs(edge1.Delta.X) < double.Epsilon)
            {
                ip.X = edge1.Bot.X;
                if (IsHorizontal(edge2))
                {
                    ip.Y = edge2.Bot.Y;
                }
                else
                {
                    b2 = edge2.Bot.Y - (edge2.Bot.X/edge2.Dx);
                    ip.Y = (ip.X/edge2.Dx + b2);
                }
            }
            else if (Math.Abs(edge2.Delta.X) < double.Epsilon)
            {
                ip.X = edge2.Bot.X;
                if (IsHorizontal(edge1))
                {
                    ip.Y = edge1.Bot.Y;
                }
                else
                {
                    b1 = edge1.Bot.Y - (edge1.Bot.X/edge1.Dx);
                    ip.Y = (ip.X/edge1.Dx + b1);
                }
            }
            else
            {
                b1 = edge1.Bot.X - edge1.Bot.Y*edge1.Dx;
                b2 = edge2.Bot.X - edge2.Bot.Y*edge2.Dx;
                var q = (b2 - b1)/(edge1.Dx - edge2.Dx);
                ip.Y = (q);
                ip.X = Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx) ? edge1.Dx*q + b1 : edge2.Dx*q + b2;
            }

            if (ip.Y < edge1.Top.Y || ip.Y < edge2.Top.Y)
            {
                ip.Y = edge1.Top.Y > edge2.Top.Y ? edge1.Top.Y : edge2.Top.Y;
                ip.X = TopX(Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx) ? edge1 : edge2, ip.Y);
            }
            //finally, don't allow 'ip' to be BELOW curr.Y (ie bottom of scanbeam) ...
            if (ip.Y > edge1.Curr.Y)
            {
                ip.Y = edge1.Curr.Y;
                //better to use the more vertical edge to derive X ...
                ip.X = TopX(Math.Abs(edge1.Dx) > Math.Abs(edge2.Dx) ? edge2 : edge1, ip.Y);
            }
        }

        //------------------------------------------------------------------------------

        private void ProcessEdgesAtTopOfScanbeam(double topY)
        {
            var e = m_ActiveEdges;
            while (e != null)
            {
                //1. process maxima, treating them as if they're 'bent' horizontal edges,
                //   but exclude maxima with horizontal edges. nb: e can't be a horizontal.
                var IsMaximaEdge = IsMaxima(e, topY);

                if (IsMaximaEdge)
                {
                    var eMaxPair = GetMaximaPair(e);
                    IsMaximaEdge = (eMaxPair == null || !IsHorizontal(eMaxPair));
                }

                if (IsMaximaEdge)
                {
                    var ePrev = e.PrevInAEL;
                    DoMaxima(e);
                    e = ePrev == null ? m_ActiveEdges : ePrev.NextInAEL;
                }
                else
                {
                    //2. promote horizontal edges, otherwise update Curr.X and Curr.Y ...
                    if (IsIntermediate(e, topY) && IsHorizontal(e.NextInLML))
                    {
                        UpdateEdgeIntoAEL(ref e);
                        if (e.OutIdx >= 0)
                            AddOutPt(e, e.Bot);
                        AddEdgeToSEL(e);
                    }
                    else
                    {
                        e.Curr.X = TopX(e, topY);
                        e.Curr.Y = topY;
                    }

                    if (StrictlySimple)
                    {
                        var ePrev = e.PrevInAEL;
                        if ((e.OutIdx >= 0) && (e.WindDelta != 0) && ePrev != null &&
                            (ePrev.OutIdx >= 0) && (Math.Abs(ePrev.Curr.X - e.Curr.X) < double.Epsilon) &&
                            (ePrev.WindDelta != 0))
                        {
                            var ip = new Point(e.Curr.X, e.Curr.Y);
                            var op = AddOutPt(ePrev, ip);
                            var op2 = AddOutPt(e, ip);
                            AddJoin(op, op2, ip); //StrictlySimple (type-3) join
                        }
                    }

                    e = e.NextInAEL;
                }
            }

            //3. Process horizontals at the Top of the scanbeam ...
            ProcessHorizontals(true);

            //4. Promote intermediate vertices ...
            e = m_ActiveEdges;
            while (e != null)
            {
                if (IsIntermediate(e, topY))
                {
                    OutPt op = null;
                    if (e.OutIdx >= 0)
                        op = AddOutPt(e, e.Top);
                    UpdateEdgeIntoAEL(ref e);

                    //if output polygons share an edge, they'll need joining later ...
                    var ePrev = e.PrevInAEL;
                    var eNext = e.NextInAEL;
                    if (ePrev != null && Math.Abs(ePrev.Curr.X - e.Bot.X) < double.Epsilon &&
                        Math.Abs(ePrev.Curr.Y - e.Bot.Y) < double.Epsilon && op != null &&
                        ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
                        SlopesEqual(e, ePrev) &&
                        (e.WindDelta != 0) && (ePrev.WindDelta != 0))
                    {
                        var op2 = AddOutPt(ePrev, e.Bot);
                        AddJoin(op, op2, e.Top);
                    }
                    else if (eNext != null && Math.Abs(eNext.Curr.X - e.Bot.X) < double.Epsilon &&
                             Math.Abs(eNext.Curr.Y - e.Bot.Y) < double.Epsilon && op != null &&
                             eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
                             SlopesEqual(e, eNext) &&
                             (e.WindDelta != 0) && (eNext.WindDelta != 0))
                    {
                        var op2 = AddOutPt(eNext, e.Bot);
                        AddJoin(op, op2, e.Top);
                    }
                }
                e = e.NextInAEL;
            }
        }

        //------------------------------------------------------------------------------

        private void DoMaxima(TEdge e)
        {
            var eMaxPair = GetMaximaPair(e);
            if (eMaxPair == null)
            {
                if (e.OutIdx >= 0)
                    AddOutPt(e, e.Top);
                DeleteFromAEL(e);
                return;
            }

            var eNext = e.NextInAEL;
            while (eNext != null && eNext != eMaxPair)
            {
                IntersectEdges(e, eNext, e.Top);
                SwapPositionsInAEL(e, eNext);
                eNext = e.NextInAEL;
            }

            if (e.OutIdx == Unassigned && eMaxPair.OutIdx == Unassigned)
            {
                DeleteFromAEL(e);
                DeleteFromAEL(eMaxPair);
            }
            else if (e.OutIdx >= 0 && eMaxPair.OutIdx >= 0)
            {
                if (e.OutIdx >= 0) AddLocalMaxPoly(e, eMaxPair, e.Top);
                DeleteFromAEL(e);
                DeleteFromAEL(eMaxPair);
            }
            else throw new Exception("DoMaxima error");
        }

        //------------------------------------------------------------------------------

        public static void ReversePaths(List<List<Point>> polys)
        {
            foreach (var poly in polys)
            {
                poly.Reverse();
            }
        }

        //------------------------------------------------------------------------------

        private int PointCount(OutPt pts)
        {
            if (pts == null) return 0;
            var result = 0;
            var p = pts;
            do
            {
                result++;
                p = p.Next;
            } while (p != pts);
            return result;
        }

        //------------------------------------------------------------------------------

        private void BuildResult(List<List<Point>> polyg)
        {
            polyg.Clear();
            polyg.Capacity = m_PolyOuts.Count;
            foreach (var outRec in m_PolyOuts)
            {
                if (outRec.Pts == null) continue;
                var p = outRec.Pts.Prev;
                var cnt = PointCount(p);
                if (cnt < 2) continue;
                var pg = new List<Point>(cnt);
                for (var j = 0; j < cnt; j++)
                {
                    pg.Add(p.Pt);
                    p = p.Prev;
                }
                polyg.Add(pg);
            }
        }

        //------------------------------------------------------------------------------

        private void FixupOutPolygon(OutRec outRec)
        {
            //FixupOutPolygon() - removes duplicate points and simplifies consecutive
            //parallel edges by removing the middle vertex.
            OutPt lastOK = null;
            outRec.BottomPt = null;
            var pp = outRec.Pts;
            for (;;)
            {
                if (pp.Prev == pp || pp.Prev == pp.Next)
                {
                    outRec.Pts = null;
                    return;
                }
                //test for duplicate points and collinear edges ...
                if ((pp.Pt == pp.Next.Pt) || (pp.Pt == pp.Prev.Pt) ||
                    (SlopesEqual(pp.Prev.Pt, pp.Pt, pp.Next.Pt) &&
                     (!PreserveCollinear || !Pt2IsBetweenPt1AndPt3(pp.Prev.Pt, pp.Pt, pp.Next.Pt))))
                {
                    lastOK = null;
                    pp.Prev.Next = pp.Next;
                    pp.Next.Prev = pp.Prev;
                    pp = pp.Prev;
                }
                else if (pp == lastOK) break;
                else
                {
                    if (lastOK == null) lastOK = pp;
                    pp = pp.Next;
                }
            }
            outRec.Pts = pp;
        }

        //------------------------------------------------------------------------------

        private OutPt DupOutPt(OutPt outPt, bool InsertAfter)
        {
            var result = new OutPt
            {
                Pt = outPt.Pt,
                Idx = outPt.Idx
            };

            if (InsertAfter)
            {
                result.Next = outPt.Next;
                result.Prev = outPt;
                outPt.Next.Prev = result;
                outPt.Next = result;
            }
            else
            {
                result.Prev = outPt.Prev;
                result.Next = outPt;
                outPt.Prev.Next = result;
                outPt.Prev = result;
            }
            return result;
        }

        //------------------------------------------------------------------------------

        private bool GetOverlap(double a1, double a2, double b1, double b2, out double Left, out double Right)
        {
            if (a1 < a2)
            {
                if (b1 < b2)
                {
                    Left = Math.Max(a1, b1);
                    Right = Math.Min(a2, b2);
                }
                else
                {
                    Left = Math.Max(a1, b2);
                    Right = Math.Min(a2, b1);
                }
            }
            else
            {
                if (b1 < b2)
                {
                    Left = Math.Max(a2, b1);
                    Right = Math.Min(a1, b2);
                }
                else
                {
                    Left = Math.Max(a2, b2);
                    Right = Math.Min(a1, b1);
                }
            }
            return Left < Right;
        }

        //------------------------------------------------------------------------------

        private bool JoinHorz(OutPt op1, OutPt op1b, OutPt op2, OutPt op2b,
            Point Pt, bool DiscardLeft)
        {
            var Dir1 = (op1.Pt.X > op1b.Pt.X ?
                Direction.RightToLeft : Direction.LeftToRight);
            var Dir2 = (op2.Pt.X > op2b.Pt.X ?
                Direction.RightToLeft : Direction.LeftToRight);
            if (Dir1 == Dir2) return false;

            //When DiscardLeft, we want Op1b to be on the Left of Op1, otherwise we
            //want Op1b to be on the Right. (And likewise with Op2 and Op2b.)
            //So, to facilitate this while inserting Op1b and Op2b ...
            //when DiscardLeft, make sure we're AT or RIGHT of Pt before adding Op1b,
            //otherwise make sure we're AT or LEFT of Pt. (Likewise with Op2b.)
            if (Dir1 == Direction.LeftToRight)
            {
                while (op1.Next.Pt.X <= Pt.X &&
                       op1.Next.Pt.X >= op1.Pt.X && Math.Abs(op1.Next.Pt.Y - Pt.Y) < double.Epsilon)
                    op1 = op1.Next;
                if (DiscardLeft && (Math.Abs(op1.Pt.X - Pt.X) > double.Epsilon)) op1 = op1.Next;
                op1b = DupOutPt(op1, !DiscardLeft);
                if (op1b.Pt != Pt)
                {
                    op1 = op1b;
                    op1.Pt = Pt;
                    op1b = DupOutPt(op1, !DiscardLeft);
                }
            }
            else
            {
                while (op1.Next.Pt.X >= Pt.X &&
                       op1.Next.Pt.X <= op1.Pt.X && Math.Abs(op1.Next.Pt.Y - Pt.Y) < double.Epsilon)
                    op1 = op1.Next;
                if (!DiscardLeft && (Math.Abs(op1.Pt.X - Pt.X) > double.Epsilon)) op1 = op1.Next;
                op1b = DupOutPt(op1, DiscardLeft);
                if (op1b.Pt != Pt)
                {
                    op1 = op1b;
                    op1.Pt = Pt;
                    op1b = DupOutPt(op1, DiscardLeft);
                }
            }

            if (Dir2 == Direction.LeftToRight)
            {
                while (op2.Next.Pt.X <= Pt.X &&
                       op2.Next.Pt.X >= op2.Pt.X && Math.Abs(op2.Next.Pt.Y - Pt.Y) < double.Epsilon)
                    op2 = op2.Next;
                if (DiscardLeft && (Math.Abs(op2.Pt.X - Pt.X) > double.Epsilon)) op2 = op2.Next;
                op2b = DupOutPt(op2, !DiscardLeft);
                if (op2b.Pt != Pt)
                {
                    op2 = op2b;
                    op2.Pt = Pt;
                    op2b = DupOutPt(op2, !DiscardLeft);
                }
            }
            else
            {
                while (op2.Next.Pt.X >= Pt.X &&
                       op2.Next.Pt.X <= op2.Pt.X && Math.Abs(op2.Next.Pt.Y - Pt.Y) < double.Epsilon)
                    op2 = op2.Next;
                if (!DiscardLeft && (Math.Abs(op2.Pt.X - Pt.X) > double.Epsilon)) op2 = op2.Next;
                op2b = DupOutPt(op2, DiscardLeft);
                if (op2b.Pt != Pt)
                {
                    op2 = op2b;
                    op2.Pt = Pt;
                    op2b = DupOutPt(op2, DiscardLeft);
                }
            }

            if ((Dir1 == Direction.LeftToRight) == DiscardLeft)
            {
                op1.Prev = op2;
                op2.Next = op1;
                op1b.Next = op2b;
                op2b.Prev = op1b;
            }
            else
            {
                op1.Next = op2;
                op2.Prev = op1;
                op1b.Prev = op2b;
                op2b.Next = op1b;
            }
            return true;
        }

        //------------------------------------------------------------------------------

        private bool JoinPoints(Join j, OutRec outRec1, OutRec outRec2)
        {
            OutPt op1 = j.OutPt1, op1b;
            OutPt op2 = j.OutPt2, op2b;

            //There are 3 kinds of joins for output polygons ...
            //1. Horizontal joins where Join.OutPt1 & Join.OutPt2 are a vertices anywhere
            //along (horizontal) collinear edges (& Join.OffPt is on the same horizontal).
            //2. Non-horizontal joins where Join.OutPt1 & Join.OutPt2 are at the same
            //location at the Bottom of the overlapping segment (& Join.OffPt is above).
            //3. StrictlySimple joins where edges touch but are not collinear and where
            //Join.OutPt1, Join.OutPt2 & Join.OffPt all share the same point.
            var isHorizontal = (Math.Abs(j.OutPt1.Pt.Y - j.OffPt.Y) < double.Epsilon);

            if (isHorizontal && (j.OffPt == j.OutPt1.Pt) && (j.OffPt == j.OutPt2.Pt))
            {
                //Strictly Simple join ...
                if (outRec1 != outRec2) return false;
                op1b = j.OutPt1.Next;
                while (op1b != op1 && (op1b.Pt == j.OffPt))
                    op1b = op1b.Next;
                var reverse1 = (op1b.Pt.Y > j.OffPt.Y);
                op2b = j.OutPt2.Next;
                while (op2b != op2 && (op2b.Pt == j.OffPt))
                    op2b = op2b.Next;
                var reverse2 = (op2b.Pt.Y > j.OffPt.Y);
                if (reverse1 == reverse2) return false;
                if (reverse1)
                {
                    op1b = DupOutPt(op1, false);
                    op2b = DupOutPt(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1b.Next = op2b;
                    op2b.Prev = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
                else
                {
                    op1b = DupOutPt(op1, true);
                    op2b = DupOutPt(op2, false);
                    op1.Next = op2;
                    op2.Prev = op1;
                    op1b.Prev = op2b;
                    op2b.Next = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
            }
            else if (isHorizontal)
            {
                //treat horizontal joins differently to non-horizontal joins since with
                //them we're not yet sure where the overlapping is. OutPt1.Pt & OutPt2.Pt
                //may be anywhere along the horizontal edge.
                op1b = op1;
                while (Math.Abs(op1.Prev.Pt.Y - op1.Pt.Y) < double.Epsilon && op1.Prev != op1b && op1.Prev != op2)
                    op1 = op1.Prev;
                while (Math.Abs(op1b.Next.Pt.Y - op1b.Pt.Y) < double.Epsilon && op1b.Next != op1 && op1b.Next != op2)
                    op1b = op1b.Next;
                if (op1b.Next == op1 || op1b.Next == op2) return false; //a flat 'polygon'

                op2b = op2;
                while (Math.Abs(op2.Prev.Pt.Y - op2.Pt.Y) < double.Epsilon && op2.Prev != op2b && op2.Prev != op1b)
                    op2 = op2.Prev;
                while (Math.Abs(op2b.Next.Pt.Y - op2b.Pt.Y) < double.Epsilon && op2b.Next != op2 && op2b.Next != op1)
                    op2b = op2b.Next;
                if (op2b.Next == op2 || op2b.Next == op1) return false; //a flat 'polygon'

                double Left, Right;
                //Op1 -. Op1b & Op2 -. Op2b are the extremites of the horizontal edges
                if (!GetOverlap(op1.Pt.X, op1b.Pt.X, op2.Pt.X, op2b.Pt.X, out Left, out Right))
                    return false;

                //DiscardLeftSide: when overlapping edges are joined, a spike will created
                //which needs to be cleaned up. However, we don't want Op1 or Op2 caught up
                //on the discard Side as either may still be needed for other joins ...
                Point Pt;
                bool DiscardLeftSide;
                if (op1.Pt.X >= Left && op1.Pt.X <= Right)
                {
                    Pt = op1.Pt;
                    DiscardLeftSide = (op1.Pt.X > op1b.Pt.X);
                }
                else if (op2.Pt.X >= Left && op2.Pt.X <= Right)
                {
                    Pt = op2.Pt;
                    DiscardLeftSide = (op2.Pt.X > op2b.Pt.X);
                }
                else if (op1b.Pt.X >= Left && op1b.Pt.X <= Right)
                {
                    Pt = op1b.Pt;
                    DiscardLeftSide = op1b.Pt.X > op1.Pt.X;
                }
                else
                {
                    Pt = op2b.Pt;
                    DiscardLeftSide = (op2b.Pt.X > op2.Pt.X);
                }
                j.OutPt1 = op1;
                j.OutPt2 = op2;
                return JoinHorz(op1, op1b, op2, op2b, Pt, DiscardLeftSide);
            }
            else
            {
                //nb: For non-horizontal joins ...
                //    1. Jr.OutPt1.Pt.Y == Jr.OutPt2.Pt.Y
                //    2. Jr.OutPt1.Pt > Jr.OffPt.Y

                //make sure the polygons are correctly oriented ...
                op1b = op1.Next;
                while ((op1b.Pt == op1.Pt) && (op1b != op1)) op1b = op1b.Next;
                var Reverse1 = ((op1b.Pt.Y > op1.Pt.Y) ||
                                !SlopesEqual(op1.Pt, op1b.Pt, j.OffPt));
                if (Reverse1)
                {
                    op1b = op1.Prev;
                    while ((op1b.Pt == op1.Pt) && (op1b != op1)) op1b = op1b.Prev;
                    if ((op1b.Pt.Y > op1.Pt.Y) ||
                        !SlopesEqual(op1.Pt, op1b.Pt, j.OffPt)) return false;
                }
                op2b = op2.Next;
                while ((op2b.Pt == op2.Pt) && (op2b != op2)) op2b = op2b.Next;
                var Reverse2 = ((op2b.Pt.Y > op2.Pt.Y) ||
                                !SlopesEqual(op2.Pt, op2b.Pt, j.OffPt));
                if (Reverse2)
                {
                    op2b = op2.Prev;
                    while ((op2b.Pt == op2.Pt) && (op2b != op2)) op2b = op2b.Prev;
                    if ((op2b.Pt.Y > op2.Pt.Y) ||
                        !SlopesEqual(op2.Pt, op2b.Pt, j.OffPt)) return false;
                }

                if ((op1b == op1) || (op2b == op2) || (op1b == op2b) ||
                    ((outRec1 == outRec2) && (Reverse1 == Reverse2))) return false;

                if (Reverse1)
                {
                    op1b = DupOutPt(op1, false);
                    op2b = DupOutPt(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1b.Next = op2b;
                    op2b.Prev = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
                else
                {
                    op1b = DupOutPt(op1, true);
                    op2b = DupOutPt(op2, false);
                    op1.Next = op2;
                    op2.Prev = op1;
                    op1b.Prev = op2b;
                    op2b.Next = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
            }
        }

       
       
        //------------------------------------------------------------------------------

        private static bool Poly2ContainsPoly1(OutPt outPt1, OutPt outPt2)
        {
            var op = outPt1;
            do
            {
                //nb: PointInPolygon returns 0 if false, +1 if true, -1 if pt on polygon
                var res = op.Pt.IsPointInPolygon(outPt2);
                
                if (res.HasValue) 
                    return res.Value;

                op = op.Next;
            } while (op != outPt1);
            return true;
        }

       

        //------------------------------------------------------------------------------

        private void JoinCommonEdges()
        {
            foreach (var jn in m_Joins)
            {
                var outRec1 = GetOutRec(jn.OutPt1.Idx);
                var outRec2 = GetOutRec(jn.OutPt2.Idx);

                if (outRec1.Pts == null || outRec2.Pts == null) continue;

                //get the polygon fragment with the correct hole state (FirstLeft)
                //before calling JoinPoints() ...
                OutRec holeStateRec;
                if (outRec1 == outRec2) holeStateRec = outRec1;
                else if (Param1RightOfParam2(outRec1, outRec2)) holeStateRec = outRec2;
                else if (Param1RightOfParam2(outRec2, outRec1)) holeStateRec = outRec1;
                else holeStateRec = GetLowermostRec(outRec1, outRec2);

                if (!JoinPoints(jn, outRec1, outRec2)) continue;

                if (outRec1 == outRec2)
                {
                    //instead of joining two polygons, we've just created a new one by
                    //splitting one polygon into two.
                    outRec1.Pts = jn.OutPt1;
                    outRec1.BottomPt = null;
                    outRec2 = CreateOutRec();
                    outRec2.Pts = jn.OutPt2;

                    //update all OutRec2.Pts Idx's ...
                    UpdateOutPtIdxs(outRec2);

                   
                    if (Poly2ContainsPoly1(outRec2.Pts, outRec1.Pts))
                    {
                        //outRec2 is contained by outRec1 ...
                        outRec2.IsHole = !outRec1.IsHole;
                        outRec2.FirstLeft = outRec1;

                        //fixup FirstLeft pointers that may need reassigning to OutRec1
                       
                        if ((outRec2.IsHole ^ ReverseSolution) == (Area(outRec2) > 0))
                            ReversePolyPtLinks(outRec2.Pts);
                    }
                    else if (Poly2ContainsPoly1(outRec1.Pts, outRec2.Pts))
                    {
                        //outRec1 is contained by outRec2 ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec1.IsHole = !outRec2.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;
                        outRec1.FirstLeft = outRec2;

                        //fixup FirstLeft pointers that may need reassigning to OutRec1
                       
                        if ((outRec1.IsHole ^ ReverseSolution) == (Area(outRec1) > 0))
                            ReversePolyPtLinks(outRec1.Pts);
                    }
                    else
                    {
                        //the 2 polygons are completely separate ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;

                        //fixup FirstLeft pointers that may need reassigning to OutRec2
                    }
                }
                else
                {
                    //joined 2 polygons together ...

                    outRec2.Pts = null;
                    outRec2.BottomPt = null;
                    outRec2.Idx = outRec1.Idx;

                    outRec1.IsHole = holeStateRec.IsHole;
                    if (holeStateRec == outRec2)
                        outRec1.FirstLeft = outRec2.FirstLeft;
                    outRec2.FirstLeft = outRec1;

                    //fixup FirstLeft pointers that may need reassigning to OutRec1
                }
            }
        }

        //------------------------------------------------------------------------------

        private void UpdateOutPtIdxs(OutRec outrec)
        {
            var op = outrec.Pts;
            do
            {
                op.Idx = outrec.Idx;
                op = op.Prev;
            } while (op != outrec.Pts);
        }

        //------------------------------------------------------------------------------

        private void DoSimplePolygons()
        {
            var i = 0;
            while (i < m_PolyOuts.Count)
            {
                var outrec = m_PolyOuts[i++];
                var op = outrec.Pts;
                if (op == null || outrec.IsOpen) continue;
                do //for each Pt in Polygon until duplicate found do ...
                {
                    var op2 = op.Next;
                    while (op2 != outrec.Pts)
                    {
                        if ((op.Pt == op2.Pt) && op2.Next != op && op2.Prev != op)
                        {
                            //split the polygon into two ...
                            var op3 = op.Prev;
                            var op4 = op2.Prev;
                            op.Prev = op4;
                            op4.Next = op;
                            op2.Prev = op3;
                            op3.Next = op2;

                            outrec.Pts = op;
                            var outrec2 = CreateOutRec();
                            outrec2.Pts = op2;
                            UpdateOutPtIdxs(outrec2);
                            if (Poly2ContainsPoly1(outrec2.Pts, outrec.Pts))
                            {
                                //OutRec2 is contained by OutRec1 ...
                                outrec2.IsHole = !outrec.IsHole;
                                outrec2.FirstLeft = outrec;
                            }
                            else if (Poly2ContainsPoly1(outrec.Pts, outrec2.Pts))
                            {
                                //OutRec1 is contained by OutRec2 ...
                                outrec2.IsHole = outrec.IsHole;
                                outrec.IsHole = !outrec2.IsHole;
                                outrec2.FirstLeft = outrec.FirstLeft;
                                outrec.FirstLeft = outrec2;
                            }
                            else
                            {
                                //the 2 polygons are separate ...
                                outrec2.IsHole = outrec.IsHole;
                                outrec2.FirstLeft = outrec.FirstLeft;
                            }
                            op2 = op; //ie get ready for the next iteration
                        }
                        op2 = op2.Next;
                    }
                    op = op.Next;
                } while (op != outrec.Pts);
            }
        }

   
        private double Area(OutRec outRec)
        {
            var op = outRec.Pts;
            if (op == null) return 0;
            double a = 0;
            do
            {
                a = a + (op.Prev.Pt.X + op.Pt.X)*(op.Prev.Pt.Y - op.Pt.Y);
                op = op.Next;
            } while (op != outRec.Pts);
            return a*0.5;
        }

        public static List<List<Point>> SimplifyPolygon(List<Point> poly,
            PolyFillType fillType = PolyFillType.EvenOdd)
        {
            var result = new List<List<Point>>();
            var c = new Clipper {StrictlySimple = true};
            c.AddPath(poly, PolyType.Subject);
            c.Execute(ClipType.Union, result, fillType, fillType);
            return result;
        }

        //------------------------------------------------------------------------------

        public static List<List<Point>> SimplifyPolygons(List<List<Point>> polys,
            PolyFillType fillType = PolyFillType.EvenOdd)
        {
            var result = new List<List<Point>>();
            var c = new Clipper {StrictlySimple = true};
            c.AddPaths(polys, PolyType.Subject);
            c.Execute(ClipType.Union, result, fillType, fillType);
            return result;
        }

        //------------------------------------------------------------------------------

        private static double DistanceFromLineSqrd(Point pt, Point ln1, Point ln2)
        {
            //The equation of a line in general form (Ax + By + C = 0)
            //given 2 points (x¹,y¹) & (x²,y²) is ...
            //(y¹ - y²)x + (x² - x¹)y + (y² - y¹)x¹ - (x² - x¹)y¹ = 0
            //A = (y¹ - y²); B = (x² - x¹); C = (y² - y¹)x¹ - (x² - x¹)y¹
            //perpendicular distance of point (x³,y³) = (Ax³ + By³ + C)/Sqrt(A² + B²)
            //see http://en.wikipedia.org/wiki/Perpendicular_distance
            var A = ln1.Y - ln2.Y;
            var B = ln2.X - ln1.X;
            var C = A*ln1.X + B*ln1.Y;
            C = A*pt.X + B*pt.Y - C;
            return (C*C)/(A*A + B*B);
        }

        //---------------------------------------------------------------------------

        private static bool SlopesNearCollinear(Point pt1,
            Point pt2, Point pt3, double distSqrd)
        {
            //this function is more accurate when the point that's GEOMETRICALLY 
            //between the other 2 points is the one that's tested for distance.  
            //nb: with 'spikes', either pt1 or pt3 is geometrically between the other pts                    
            if (Math.Abs(pt1.X - pt2.X) > Math.Abs(pt1.Y - pt2.Y))
            {
                if ((pt1.X > pt2.X) == (pt1.X < pt3.X))
                    return DistanceFromLineSqrd(pt1, pt2, pt3) < distSqrd;
                else if ((pt2.X > pt1.X) == (pt2.X < pt3.X))
                    return DistanceFromLineSqrd(pt2, pt1, pt3) < distSqrd;
                else
                    return DistanceFromLineSqrd(pt3, pt1, pt2) < distSqrd;
            }
            else
            {
                if ((pt1.Y > pt2.Y) == (pt1.Y < pt3.Y))
                    return DistanceFromLineSqrd(pt1, pt2, pt3) < distSqrd;
                else if ((pt2.Y > pt1.Y) == (pt2.Y < pt3.Y))
                    return DistanceFromLineSqrd(pt2, pt1, pt3) < distSqrd;
                else
                    return DistanceFromLineSqrd(pt3, pt1, pt2) < distSqrd;
            }
        }

        //------------------------------------------------------------------------------

        private static bool PointsAreClose(Point pt1, Point pt2, double distSqrd)
        {
            var dx = pt1.X - pt2.X;
            var dy = pt1.Y - pt2.Y;
            return ((dx*dx) + (dy*dy) <= distSqrd);
        }

        //------------------------------------------------------------------------------

        private static OutPt ExcludeOp(OutPt op)
        {
            var result = op.Prev;
            result.Next = op.Next;
            op.Next.Prev = result;
            result.Idx = 0;
            return result;
        }

        //------------------------------------------------------------------------------

        public static List<Point> CleanPolygon(List<Point> path, double distance = 1.415)
        {
            //distance = proximity in units/pixels below which vertices will be stripped. 
            //Default ~= sqrt(2) so when adjacent vertices or semi-adjacent vertices have 
            //both x & y coords within 1 unit, then the second vertex will be stripped.

            var cnt = path.Count;

            if (cnt == 0) return new List<Point>();

            var outPts = new OutPt[cnt];
            for (var i = 0; i < cnt; ++i) outPts[i] = new OutPt();

            for (var i = 0; i < cnt; ++i)
            {
                outPts[i].Pt = path[i];
                outPts[i].Next = outPts[(i + 1)%cnt];
                outPts[i].Next.Prev = outPts[i];
                outPts[i].Idx = 0;
            }

            var distSqrd = distance*distance;
            var op = outPts[0];
            while (op.Idx == 0 && op.Next != op.Prev)
            {
                if (PointsAreClose(op.Pt, op.Prev.Pt, distSqrd))
                {
                    op = ExcludeOp(op);
                    cnt--;
                }
                else if (PointsAreClose(op.Prev.Pt, op.Next.Pt, distSqrd))
                {
                    ExcludeOp(op.Next);
                    op = ExcludeOp(op);
                    cnt -= 2;
                }
                else if (SlopesNearCollinear(op.Prev.Pt, op.Pt, op.Next.Pt, distSqrd))
                {
                    op = ExcludeOp(op);
                    cnt--;
                }
                else
                {
                    op.Idx = 1;
                    op = op.Next;
                }
            }

            if (cnt < 3) cnt = 0;
            var result = new List<Point>(cnt);
            for (var i = 0; i < cnt; ++i)
            {
                result.Add(op.Pt);
                op = op.Next;
            }
            return result;
        }

        //------------------------------------------------------------------------------

        public static List<List<Point>> CleanPolygons(List<List<Point>> polys,
            double distance = 1.415)
        {
            var result = new List<List<Point>>(polys.Count);
            result.AddRange(polys.Select(t => CleanPolygon(t, distance)));
            return result;
        }

        //------------------------------------------------------------------------------

        protected  static List<List<Point>> Minkowski(List<Point> pattern, List<Point> path, bool IsSum, bool IsClosed)
        {
            var delta = (IsClosed ? 1 : 0);
            var polyCnt = pattern.Count;
            var pathCnt = path.Count;
            var result = new List<List<Point>>(pathCnt);

            var mul = (IsSum ? 1.0 : -1.0);

            for (var i = 0; i < pathCnt; i++)
            {
                var p = new List<Point>(polyCnt);
                p.AddRange(pattern.Select(ip => new Point(path[i].X + mul * ip.X, path[i].Y + mul * ip.Y)));
                result.Add(p);
            }

            var quads = new List<List<Point>>((pathCnt + delta)*(polyCnt + 1));

            for (var i = 0; i < pathCnt - 1 + delta; i++)
            {
                for (var j = 0; j < polyCnt; j++)
                {
                    var quad = new List<Point>
                    {
                        result[i%pathCnt][j%polyCnt],
                        result[(i + 1)%pathCnt][j%polyCnt],
                        result[(i + 1)%pathCnt][(j + 1)%polyCnt],
                        result[i%pathCnt][(j + 1)%polyCnt]
                    };
                    if (!quad.Orientation())
                        quad.Reverse();

                    quads.Add(quad);
                }
            }
            return quads;
        }

        //------------------------------------------------------------------------------

        public static List<List<Point>> MinkowskiSum(List<Point> pattern, List<Point> path, bool pathIsClosed)
        {
            var paths = Minkowski(pattern, path, true, pathIsClosed);
            var c = new Clipper();
            c.AddPaths(paths, PolyType.Subject);
            c.Execute(ClipType.Union, paths, PolyFillType.NonZero, PolyFillType.NonZero);
            return paths;
        }

        //------------------------------------------------------------------------------

        private static List<Point> TranslatePath(List<Point> path, Point delta)
        {
            var outPath = new List<Point>(path.Count);
            outPath.AddRange(path.Select(t => new Point(t.X + delta.X, t.Y + delta.Y)));
            return outPath;
        }

        //------------------------------------------------------------------------------

        public static List<List<Point>> MinkowskiSum(List<Point> pattern, List<List<Point>> paths, bool pathIsClosed)
        {
            var solution = new List<List<Point>>();
            var c = new Clipper();
            foreach (var pathx in paths)
            {
                var tmp = Minkowski(pattern, pathx, true, pathIsClosed);
                c.AddPaths(tmp, PolyType.Subject);
                if (pathIsClosed)
                {
                    var path = TranslatePath(pathx, pattern[0]);
                    c.AddPath(path, PolyType.Clip);
                }
            }
            c.Execute(ClipType.Union, solution,
                PolyFillType.NonZero, PolyFillType.NonZero);
            return solution;
        }

        //------------------------------------------------------------------------------

        public static List<List<Point>> MinkowskiDiff(List<Point> poly1, List<Point> poly2)
        {
            var paths = Minkowski(poly1, poly2, false, true);
            var c = new Clipper();
            c.AddPaths(paths, PolyType.Subject);
            c.Execute(ClipType.Union, paths, PolyFillType.NonZero, PolyFillType.NonZero);
            return paths;
        }
    } //end Clipper
}
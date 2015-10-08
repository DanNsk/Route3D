using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using netDxf;
using Route3D.Helpers;
using Route3D.Helpers.WPF;
using Route3D.Properties;

namespace Route3D
{
#pragma warning disable 1998

    public class MainViewModel : DispatchedObservable
    {
        private const string OPEN_FILE_FILTER = "3D model files (*.3ds;*.obj;*.lwo;*.stl)|*.3ds;*.obj;*.objz;*.lwo;*.stl";
        private const string OPEN_FLAT_FILE_FILTER = "2D Model files (*.dxf)|*.dxf";

        private const string SAVE_FILE_FILTER = "GCODE File (*.gcode)|*.gcode";
        public const double EPSILON = 1e-3;
        public const double DRILL_STEP = 25.4/8/2;
        private static readonly Size3D WORK_SIZE = new Size3D(240.0, 220.0, 90.0);
        private readonly IFileDialogService fileDialogService;
        public readonly ObservableCollection<Point3D> GridLinePoints = new ObservableCollection<Point3D>(GeometryHelper.GenerateGridLines(WORK_SIZE));
        private readonly ModelImporter modelImporter;
        // ReSharper disable once NotAccessedField.Local
        private readonly HelixViewport3D viewport;
        private Model3DGroup currentModel;
        private List<List<Point3D>> currentPaths;
        private Model3DGroup currentPathsModel;
        private string title;

        public MainViewModel(IFileDialogService fileDialogService, HelixViewport3D viewport)
        {
            this.fileDialogService = fileDialogService;
            this.viewport = viewport;
            modelImporter = new ModelImporter();
        }

        public List<List<Point3D>> CurrentPaths
        {
            get { return currentPaths; }
            set
            {
                

                Dispatch(() =>
                {
                    currentPaths = value;
                    OnPropertyChanged();

                    var res = new Model3DGroup();

                    if (currentPaths != null)
                    {
                        var rand = new Random();

                        foreach (var path in currentPaths)
                        {
                            var mb = new MeshBuilder();
                            mb.AddTube(path, 1, 8, true);
                            res.Children.Add(new GeometryModel3D {Geometry = mb.ToMesh(true), Material = MaterialHelper.CreateMaterial(GeometryHelper.GoodColors[rand.Next(GeometryHelper.GoodColors.Count - 1)]), Transform = new TranslateTransform3D(0, 0, /*scnt++*/0)});
                        }
                    }

                    CurrentPathsModel = res;
                });

            }
        }

        public Model3DGroup CurrentPathsModel
        {
            get { return currentPathsModel; }
            set
            {
                currentPathsModel = value;
                OnPropertyChanged();
            }
        }

        public Model3DGroup CurrentModel
        {
            get { return currentModel; }
            set
            {
                currentModel = value;

                OnPropertyChanged();

                CurrentPaths = null;
                CurrentPaths = GenerateSlicePaths(CurrentModel, 1, DRILL_STEP);
            }
        }

        public string CurrentModelPath
        {
            get { return Settings.Default.FileName; }
            set
            {
                Settings.Default["FileName"] = value;
                Settings.Default.Save();
                OnPropertyChanged();

                CurrentModel = ImportFile(value + "");
                Title = Path.GetFileName(value + "");
            }
        }

        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                OnPropertyChanged();
            }
        }

        private void FileExit()
        {
            Application.Current.Shutdown();
        }

        private async Task FileOpen()
        {
            var path = fileDialogService.OpenFileDialog("models", null, OPEN_FILE_FILTER, ".stl");


            if (!string.IsNullOrEmpty(path))
            {
                CurrentModelPath = path;
            }
        }

        private async Task FlatFileOpen()
        {
            var path = fileDialogService.OpenFileDialog("models", null, OPEN_FLAT_FILE_FILTER, ".dxf");


            if (!string.IsNullOrEmpty(path))
            {
                var doc = DxfDocument.Load(path);

                var cp = new List<List<Point3D>>();

                var dist = 0.5;

                foreach (var line in doc.Lines)
                {
                    cp.Add(new List<Point3D> { new Point3D(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z), new Point3D(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z) });
                }

                foreach (var line in doc.Arcs)
                {

                    var dang = Math.Abs(line.StartAngle - line.EndAngle)*Math.PI/180.0;

                    var seg = Math.Max((int)Math.Ceiling(line.Radius * dang / dist), 1);
                    
                    var pl = line.ToPolyline(seg);

                    cp.Add(pl.PoligonalVertexes(seg, EPSILON, EPSILON).Select(poligonalVertex => new Point3D(poligonalVertex.X, poligonalVertex.Y, line.Center.Z)).ToList());
                }

                foreach (var line in doc.Circles)
                {
                    

                    var seg = Math.Max((int)Math.Ceiling(line.Radius * Math.PI / dist), 1);

                    var pl = line.ToPolyline(seg);

                    cp.Add(pl.PoligonalVertexes(seg, EPSILON, EPSILON).Select(poligonalVertex => new Point3D(poligonalVertex.X, poligonalVertex.Y, line.Center.Z)).ToList());
                }

                foreach (var line in doc.Polylines)
                {
                    cp.Add(line.Vertexes.Select(poligonalVertex => new Point3D(poligonalVertex.Location.X, poligonalVertex.Location.Y, poligonalVertex.Location.Z)).ToList());
                }

                foreach (var line in doc.LwPolylines)
                {
                    if (line.Vertexes.Count < 2)
                        continue;

                    var fdist = new Point(line.Vertexes[0].Location.X, line.Vertexes[0].Location.Y).DistanceTo(new Point(line.Vertexes.Last().Location.X, line.Vertexes.Last().Location.Y));

                    cp.Add(line.PoligonalVertexes(Math.Max((int)Math.Ceiling(fdist / dist), 2), EPSILON, EPSILON).Select(poligonalVertex => new Point3D(poligonalVertex.X, poligonalVertex.Y, line.Elevation)).ToList());
                }


                cp.JoinNearPoints(EPSILON);

                CurrentPaths = cp;

            }
        }

        private async Task FileSave()
        {
            var path = fileDialogService.SaveFileDialog("models", null, SAVE_FILE_FILTER, ".gcode");

            if (!string.IsNullOrEmpty(path))
            {
                ExportGCODE(path);
            }
        }

        private bool FileSaveCanExecute()
        {
            return currentPaths != null && currentPaths.Count > 0;
        }

        private void ExportGCODE(string path)
        {
            using (var file = File.CreateText(path))
            {
                file.WriteLine("G21");

                double safez = 0;
                
                Dispatch(() => { safez = CurrentModel.Bounds.Location.Z + CurrentModel.Bounds.SizeZ + 10; });
                var zsp = 70;
                var xysp = 200;


                foreach (var pathp in CurrentPaths.Where(x => x.Count > 0))
                {
                    file.WriteLine("G00 Z{0:F2}", safez);
                    var first = pathp[0];
                    file.WriteLine("G00 X{0:F2} Y{1:F2}", first.X, first.Y);

                    file.WriteLine("G01 Z{0:F2} F{1:F2}", first.Z, zsp);

                    for (var i = 1; i <= pathp.Count; i++)
                    {
                        file.WriteLine("G01 X{0:F2} Y{1:F2} F{2:F2}", pathp[i%pathp.Count].X, pathp[i%pathp.Count].Y, xysp);

                        if (pathp[i%pathp.Count] == first)
                            break;
                    }
                }

                file.WriteLine("G00 Z{0:F2}", safez);
            }
        }

        private Model3DGroup ImportFile(string model3DPath)
        {
            var res = !string.IsNullOrEmpty(model3DPath) ? modelImporter.Load(model3DPath, Dispatcher, false) : null;

            if (res != null)
            {
                Dispatch(() => { res = res.JoinModelsToOne(MaterialHelper.CreateMaterial(Colors.Blue)); });
            }

            return res;
        }

        private List<List<Point3D>> GenerateSlicePaths(Model3DGroup obj, double vs, double hs)
        {
            var bounds = new Rect3D();
            MeshGeometry3D geom = null;

            Dispatch(() => {
                bounds = obj.Bounds;
                geom = obj.Children.OfType<GeometryModel3D>().First().Geometry as MeshGeometry3D;
            });

            var res = new List<List<Point3D>>();

            List<List<Point3D>> xpaths = null;

            for (var i = bounds.Location.Z + bounds.Size.Z; i >= bounds.Location.Z; i -= vs)
            {
                
                IList<Tuple<int, int>> des = null;
                List<Point3D> points = null;
                Dispatch(() => {
                    var modl = geom.Slice(new Point3D(0, 0, i - vs), new Vector3D(0, 0, vs), EPSILON);
                    points = modl.Positions.ToList();
                    des = modl.FindBottomContours(EPSILON);
                });

                


                if (des.Count == 0)
                    continue;

                var paths = new List<List<int>>();
                var dict = des.Union(des.Select(x => Tuple.Create(x.Item2, x.Item1))).GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.Select(y => y.Item2).ToList());


                while (dict.Count > 0)
                {
                    var fp = dict.Where(x => x.Value.Count() == 1).Union(dict)
                        .Select(x => Tuple.Create(x.Key, x.Value.First())).FirstOrDefault();

                    var fpath = new List<int>();

                    paths.Add(fpath);

                    var ind = fp.Item1;


                    while (dict.ContainsKey(ind))
                    {
                        fpath.Add(ind);

                        var d = dict;

                        var tmp = ind;
                        ind = d[ind].First();

                        if (d.ContainsKey(tmp))
                        {
                            d[tmp].Remove(ind);

                            if (d[tmp].Count == 0)
                                d.Remove(tmp);
                        }

                        if (d.ContainsKey(ind))
                        {
                            d[ind].Remove(tmp);

                            if (d[ind].Count == 0)
                                d.Remove(ind);
                        }


                        fpath.Add(ind);
                    }
                }


                

                var paths1 = paths.FixMergeIndexPaths(points, EPSILON).Select(x => x.Select(y => points[y]).ToList()).ToList();

                var hstep = 4;

                xpaths = xpaths.ClipPaths(paths1, ClipType.Union, i, EPSILON).RemoveSmallPolygons(1, hstep*3, hstep).FixPointPaths();


                var co = new ClipperOffset();

                co.AddPaths(xpaths.ChangePointUnits(), JoinType.Round, EndType.ClosedPolygon);


                var xpathsres = new List<List<Point3D>>();


                var j = 1;
                do
                {
                    int nonFixed;
                    var xpathsn = co.Execute(hs*j).ChangePointUnits(i - bounds.Location.Z).FixBounds(bounds, hs*3, out nonFixed);
                    if (nonFixed == 0)
                        break;
                    j++;
                    xpathsres = xpathsres.Union(xpathsn).ToList();
                } while (true);


                xpathsres.JoinNearPoints(EPSILON);

                res.AddRange(xpathsres);
            }

            return res;
        }
    }
#pragma warning restore 1998
}
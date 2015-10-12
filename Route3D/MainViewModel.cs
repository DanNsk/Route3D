using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using netDxf;
using Route3D.Geometry;
using Route3D.Geometry.D2;
using Route3D.Geometry.D3;
using Route3D.Helpers;
using Route3D.Helpers.WPF;
using Route3D.Properties;

namespace Route3D
{
#pragma warning disable 1998

    public class MainViewModel : DispatchedObservable
    {
        
        public const double EPSILON = 1e-3;
        public const double DRILL_STEP = 25.4/8/2 * 0.85;
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
            var path = fileDialogService.OpenFileDialog("models", null, Point3DImporterExporterFactory.OpenFileDialogFilter, Point3DImporterExporterFactory.OpenFileDialogDefault);


            if (!string.IsNullOrEmpty(path))
            {
                CurrentModelPath = path;

            }
        }

        private async Task FlatFileOpen()
        {
            var path = fileDialogService.OpenFileDialog("models", null, PointImporterExporterFactory.OpenFileDialogFilter, PointImporterExporterFactory.OpenFileDialogDefault);


            if (!string.IsNullOrEmpty(path))
            {
                var cpx = PointImporterExporterFactory.Import(path);

                if (cpx != null)
                {
                    var ctx = cpx.Bounds;

                    if (ctx != null)
                        cpx.MoveBy(new Point(-(ctx.Item1.X + ctx.Item2.X) / 2, -(ctx.Item1.Y + ctx.Item2.Y) / 2));
                    
                    var cp = cpx.FlattenHierarchy();

                    var co = new ClipperOffset();

                    co.AddPaths(cp.Select(x => x.Select(y => y).ToList()).ToList(), JoinType.Round, EndType.ClosedPolygon);


                    CurrentPaths = co.Execute(DRILL_STEP, EPSILON).Select((x, i) => x.Select(y => new Point3D(y.X, y.Y, 0)).ToList()).ToList();
                }

            }
        }

        private async Task FileSave()
        {
            if (CurrentModel != null)
            {
                var path = fileDialogService.SaveFileDialog("models", null, Point3DImporterExporterFactory.SaveFileDialogFilter, Point3DImporterExporterFactory.SaveFileDialogDefault);

                if (!string.IsNullOrEmpty(path))
                {
                    Point3DImporterExporterFactory.Export(path, new Point3DPath(null, CurrentPaths.Select(x => new Point3DPath(x.Select(y => new Point3D(y.X, y.Y, y.Z)), null))));
                }
            }
            else
            {
                var path = fileDialogService.SaveFileDialog("models", null, PointImporterExporterFactory.SaveFileDialogFilter, PointImporterExporterFactory.SaveFileDialogDefault);

                if (!string.IsNullOrEmpty(path))
                {
                    PointImporterExporterFactory.Export(path, new PointPath(null, CurrentPaths.Select(x => new PointPath(x.Select(y => new Point(y.X, y.Y)), null))));
                }
            }
        }

        private bool FileSaveCanExecute()
        {
            return currentPaths != null && currentPaths.Count > 0;
        }


        private Model3DGroup ImportFile(string model3DPath)
        {
            var res = !string.IsNullOrEmpty(model3DPath) ? modelImporter.Load(model3DPath, Dispatcher) : null;

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
                    // ReSharper disable once AccessToModifiedClosure
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
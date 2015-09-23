using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.XPath;
using HelixToolkit.Wpf;
using Route3D.Helpers;
using Route3D.Properties;

namespace Route3D
{
    public class MainViewModel : Observable
    {
        private const string OPEN_FILE_FILTER = "3D model files (*.3ds;*.obj;*.lwo;*.stl)|*.3ds;*.obj;*.objz;*.lwo;*.stl";
        private static readonly Size3D WORK_SIZE = new Size3D(240.0, 220.0, 90.0);
        public readonly ObservableCollection<Point3D> GridLinePoints = new ObservableCollection<Point3D>(GeometryHelper.GenerateGridLines(WORK_SIZE));


        private readonly IFileDialogService fileDialogService;
        // ReSharper disable once NotAccessedField.Local
        private readonly HelixViewport3D viewport;
        private readonly ModelImporter modelImporter;


        private Model3D currentModel;
        private string title;




        public MainViewModel(IFileDialogService fileDialogService, HelixViewport3D viewport)
        {
            this.fileDialogService = fileDialogService;
            this.viewport = viewport;
            this.modelImporter = new ModelImporter();

            CurrentModelPath = CurrentModelPath;
            CurrentModel = ImportFile(CurrentModelPath);

        }


        private void FileExit()
        {
            Application.Current.Shutdown();
        }

        private void FileOpen()
        {
            string path = this.fileDialogService.OpenFileDialog("models", null, OPEN_FILE_FILTER, ".stl");


            if (!string.IsNullOrEmpty(path))
            {
                this.CurrentModelPath = path;
                this.CurrentModel = ImportFile(CurrentModelPath);
            }
        }



        public Model3D CurrentModel
        {
            get { return currentModel; }
            set
            {
                currentModel = value;
                OnPropertyChanged();
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

        public const double EPSILON = 1e-3; 

        private Model3DGroup ImportFile(string model3DPath)
        {
            var res =  !string.IsNullOrEmpty(model3DPath) ? modelImporter.Load(model3DPath, null, true) : null;

            if (res != null)
            {
                var model3DGroup = new Model3DGroup();

                foreach (var geom in res.Children.OfType<GeometryModel3D>())
                {
                    if (geom.Geometry is MeshGeometry3D)
                        model3DGroup.Children.Add(geom);
                }
                res = model3DGroup;

                var bounds = res.Bounds;

                model3DGroup = new Model3DGroup();

                var rand = new Random();


                foreach (var geom in res.Children.OfType<GeometryModel3D>())
                {
                    var step = Math.Sign(bounds.Size.Z)*1;

                    List<List<Point3D>> xpaths = null;

                    for (double i = bounds.Location.Z + bounds.Size.Z; i >= bounds.Location.Z; i -= step)
                    {

                        var modl = MeshGeometryHelper.Cut(MeshGeometryHelper.Cut((MeshGeometry3D)geom.Geometry, new Point3D(0, 0, i - step), new Vector3D(0, 0, 1)), new Point3D(0, 0, i), new Vector3D(0, 0, -1));

                        modl.JoinNearIndices(EPSILON);

                        var des = modl.FindBottomContours(EPSILON);


                        if (des.Count == 0)
                            continue;

                        var paths = new List<List<int>>();
                        var dict = des.Union(des.Select(x=>Tuple.Create(x.Item2, x.Item1))).GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.Select(y => y.Item2).ToList());


                        

                        while (dict.Count > 0)
                        {
                            var fp = dict.Where(x => x.Value.Count() == 1).Union(dict)
                                .Select(x => Tuple.Create(x.Key, x.Value.First())).FirstOrDefault();

                            var fpath = new List<int>();

                            paths.Add(fpath);

                            var ind = fp.Item1;


                            while (dict.ContainsKey(ind))
                            {
                                fpath.Add(ind);//modl.Positions[ind]);

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


                                fpath.Add(ind);//modl.Positions[ind]);
                            }

                        }



                        

                        List<List<Point3D>> paths1 = paths.FixMergeIndexPaths(modl.Positions, EPSILON).Select(x=>x.Select(y=>modl.Positions[y]).ToList()).ToList();

                        xpaths = xpaths.ClipPaths(paths1, ClipType.Union, i, EPSILON).FixPointPaths();

                        var mb = new MeshBuilder();


                        foreach (var path in xpaths)
                        {

                            mb.AddTube(path, 1, 8, true);
                        }


                        model3DGroup.Children.Add(new GeometryModel3D { Geometry = mb.ToMesh(true), Material = MaterialHelper.CreateMaterial(GeometryHelper.GoodColors[rand.Next(GeometryHelper.GoodColors.Count - 1)]) });


                      
                    }

                                   

                }


                res = model3DGroup;
            }


            
            return res;
        }
    }
}
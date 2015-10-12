using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Media3D;
using Route3D.Geometry.D2;

namespace Route3D.Geometry.D3
{
    public class Point3DImporterExporterFactory
    {
        private static readonly IDictionary<string, Type> importers = new Dictionary<string, Type> {
            { ".stl", typeof(StlImporter) } ,
            { ".3ds", typeof(StlImporter) } ,
            { ".obj", typeof(StlImporter) } ,
            { ".lwo", typeof(StlImporter) } ,
        };

        private static readonly IDictionary<string, Type> exporters = new Dictionary<string, Type> { 
            { ".gcode", typeof(GCode3DExporter) }, 
        };


        public static string OpenFileDialogFilter
        {
            get { return string.Format("3D Filter ({0})|{1}", "*" + string.Join(", *", importers.Keys), "*" + string.Join(";*", importers.Keys)); }
        }

        public static string OpenFileDialogDefault
        {
            get { return importers.Keys.First(); }
        }

        public static string SaveFileDialogFilter
        {
            get
            {
                return exporters.Keys.Aggregate("", (current, k) => current + string.Format("|3D (*{0})|*{0}", k)).Substring(1);
            }
        }

        public static string SaveFileDialogDefault
        {
            get { return exporters.Keys.First(); }
        }


        public static HierarchyItem<Point3D> Import(string path)
        {
            Type tp;
            if (importers.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out tp))
            {
                return ((IHierarchyItemImporter<Point3D>)Activator.CreateInstance(tp)).Import(path);
            }

            return null;
        }

        public static void Export(string path, HierarchyItem<Point3D> points)
        {
            Type tp;
            if (exporters.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out tp))
            {
                ((IHierarchyItemExporter<Point3D>)Activator.CreateInstance(tp)).Export(path, points);
            }
        }



    }
}

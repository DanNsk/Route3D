using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Route3D.Geometry.D2
{
    public class PointImporterExporterFactory
    {
        private static readonly IDictionary<string, Type> importers = new Dictionary<string, Type> {
            { ".dxf", typeof(DXFImporterExporter) } 
        };
        
        private static readonly IDictionary<string, Type> exporters = new Dictionary<string, Type> { 
            { ".gcode", typeof(GCodeExporter) }, 
            { ".dxf", typeof(DXFImporterExporter) }
        };


        public static string OpenFileDialogFilter
        {
            get { return string.Format("2D ({0})|{0}", "*" + string.Join(", *", importers.Keys)); }
        }

        public static string OpenFileDialogDefault
        {
            get { return importers.Keys.First(); }
        }

        public static string SaveFileDialogFilter
        {
            get
            {
                return exporters.Keys.Aggregate("", (current, k) => current + string.Format("|2D (*{0})|*{0}", k)).Substring(1);
            }
        }

        public static string SaveFileDialogDefault
        {
            get { return exporters.Keys.First(); }
        }


        public static HierarchyItem<Point> Import(string path)
        {
            Type tp;
            if (importers.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out tp))
            {
                return ((IHierarchyItemImporter<Point>)Activator.CreateInstance(tp)).Import(path);
            }

            return null;
        }

        public static void Export(string path, HierarchyItem<Point> points)
        {
            Type tp;
            if (exporters.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out tp))
            {
                ((IHierarchyItemExporter<Point>)Activator.CreateInstance(tp)).Export(path, points);
            }
        }



    }
}

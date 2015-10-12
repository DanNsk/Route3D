using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Route3D.Geometry.D3
{
    public class StlImporter : IHierarchyItemImporter<Point3D>
    {
        public HierarchyItem<Point3D> Import(string path)
        {
            var root = new Point3DPath();

            const double dist = 0.5;


            root.MergeLevelCorrectChildren(dist);

            return root;
        }
    }
}

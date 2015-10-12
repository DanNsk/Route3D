namespace Route3D.Geometry
{
    public interface IHierarchyItemExporter<T>
    {
        void Export(string path, HierarchyItem<T> exp);
    }
}
namespace Route3D.Geometry
{
    public interface IHierarchyItemImporter<T>
    {
        HierarchyItem<T> Import(string path);
    }
}
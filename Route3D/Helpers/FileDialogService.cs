using Microsoft.Win32;

namespace Route3D.Helpers
{
    public class FileDialogService : IFileDialogService
    {
        public string OpenFileDialog(string initialDirectory, string defaultPath, string filter, string defaultExtension)
        {
            var d = new OpenFileDialog
            {
                InitialDirectory = initialDirectory,
                FileName = defaultPath,
                Filter = filter,
                DefaultExt = defaultExtension
            };
            return d.ShowDialog() != true ? null : d.FileName;
        }

        public string SaveFileDialog(string initialDirectory, string defaultPath, string filter, string defaultExtension)
        {
            var d = new SaveFileDialog
            {
                InitialDirectory = initialDirectory,
                FileName = defaultPath,
                Filter = filter,
                DefaultExt = defaultExtension
            };
            return d.ShowDialog() != true ? null : d.FileName;

        }
    }
}
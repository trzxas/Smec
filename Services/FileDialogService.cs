using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;


namespace Emulation.Services
{
    public class FileDialogService : IFileDialogService
    {
        public string? OpenFileDialog(string filter)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Filter = filter
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }
            return null;
        }

        public string? SaveFileDialog(string defaultExt, string filter)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new()
            {
                DefaultExt = defaultExt,
                Filter = filter
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                return saveFileDialog.FileName;
            }
            return null;
        }
    }
}

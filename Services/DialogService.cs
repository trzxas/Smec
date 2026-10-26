using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;


namespace Emulation.Services
{
    public class DialogService : IDialogService
    {
        public System.Windows.Media.Color? OpenColorDialog()
        {
            // Создаем экземпляр WinForms диалога
            using var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Конвертируем цвет из System.Drawing.Color в System.Windows.Media.Color
                var wpfColor = System.Windows.Media.Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                return wpfColor;
            }
            return null;
        }

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

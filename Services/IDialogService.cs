using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Smec.Services
{
    public interface IDialogService
    {
        private const string strFilter= "Все файлы (*.*)|*.*";

        // Возвращает путь к файлу или null, если пользователь отменил выбор
        string? OpenFileDialog(string filter = strFilter);
        string? SaveFileDialog(string defaultExt = ".*", string filter = strFilter);

        System.Windows.Media.Color? OpenColorDialog();
    }
}

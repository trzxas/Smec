using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Services.DataServices
{
    public interface IDataService<T> where T : class
    {
        string FormatName { get; }        // Например: "XML файлы"
        string DefaultExtension { get; }  // Например: ".xml"

        // Строка фильтра для диалога
        string FileFilter => $"{FormatName}(*{DefaultExtension})|*{DefaultExtension}|Все файлы(*.*)|*.*";

        // Флаги возможностей
        bool CanImport { get; }
        bool CanExport { get; }

        // Возвращает объект модели из файла синхронно
        T? LoadFromFile(string filePath);
        
        // Сохраняет модель в файле синхронно
        void SaveToFile(string filePath, T data);

        // Возвращает объект модели из файла асинхронно
        Task<T?> LoadFromFileAsync(string filePath);

        // Сохраняет модель в файле асинхронно
        Task SaveToFileAsync(string filePath, T data);
    }
}

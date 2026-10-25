using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Emulation.Services.DataServices
{
    public class JsonDataService<T> : IDataService<T> where T : class
    {
        public string FormatName => "JSON файлы";
        public string DefaultExtension => ".json";

        public bool CanImport => true;

        public bool CanExport => true;

        private static readonly JsonSerializerOptions s_writeOptions = new()
        {
            WriteIndented = true, // Робить JSON красивим (з відступами)
            Converters = { new CompactPointConverter() } // ТЕПЕРЬ LOCATION ТОЖЕ БУДЕТ В ОДНУ СТРОКУ!
        };

        private static readonly JsonSerializerOptions s_readOptions = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true // Ігнорування регістру ключів
        };


        public T? LoadFromFile(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // Синхронно читаємо через потік (економно за пам'яттю для великих файлів)
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<T>(fileStream, s_readOptions);
        }

        public void SaveToFile(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // Синхронно записуємо через потік
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(fileStream, data, s_writeOptions);
        }

        public async Task<T?> LoadFromFileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // Справжня асинхронність введення-виведення (useAsync: true)
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            // Використовуємо рідний асинхронний метод System.Text.Json
            return await JsonSerializer.DeserializeAsync<T>(fileStream, s_readOptions);
        }

        public async Task SaveToFileAsync(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // Справжня асинхронність введення-виведення (useAsync: true)
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

            // Використовуємо рідний асинхронний метод System.Text.Json
            await JsonSerializer.SerializeAsync(fileStream, data, s_writeOptions);
        }
    }
}


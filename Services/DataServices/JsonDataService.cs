using Smec.Services.DataServices;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Smec.Services.DataServices
{
    public class JsonDataService<T> : IDataService<T> where T : class
    {
        public string FormatName => "JSON файлы";
        public string DefaultExtension => ".json";

        public bool CanImport => true;

        public bool CanExport => true;

        private static readonly JsonSerializerOptions s_jsonOptions = CreateJsonOptions();

        // Опции
        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                // Общие параметры
                ReferenceHandler = ReferenceHandler.Preserve,
                TypeInfoResolver = new JsonPolymorphicNodeResolver(),

                // Параметры для чтения
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,

                // Параметры для записи
                WriteIndented = true
            };

            // Безопасно добавляем конвертер до заморозки объекта
            options.Converters.Add(new JsonCompactPointConverter());

            // Явно блокируем объект для изменений, делая его thread-safe
            options.MakeReadOnly();

            return options;
        }


        public T? LoadFromFile(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // Синхронно читаємо через потік (економно за пам'яттю для великих файлів)
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<T>(fileStream, s_jsonOptions);
        }

        public void SaveToFile(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // Синхронно записуємо через потік
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(fileStream, data, s_jsonOptions);
        }

        public async Task<T?> LoadFromFileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // Справжня асинхронність введення-виведення (useAsync: true)
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            // Використовуємо рідний асинхронний метод System.Text.Json
            return await JsonSerializer.DeserializeAsync<T>(fileStream, s_jsonOptions);
        }

        public async Task SaveToFileAsync(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // Справжня асинхронність введення-виведення (useAsync: true)
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

            // Використовуємо рідний асинхронний метод System.Text.Json
            await JsonSerializer.SerializeAsync(fileStream, data, s_jsonOptions);
        }
    }
}


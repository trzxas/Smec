using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Emulation.Services.DataServices
{
    public class BsonDataService<T> : IDataService<T> where T : class 
    {
        public string FormatName => "BSON файлы";
        public string DefaultExtension => ".bson";

        public bool CanImport => true;

        public bool CanExport => true;

        public T? LoadFromFile(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // Синхронно читаємо і десеріалізуємо (помилки летять у ViewModel)
            byte[] bsonBytes = File.ReadAllBytes(filePath);
            return BsonSerializer.Deserialize<T>(bsonBytes);
        }

        public  void SaveToFile(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // Синхронно серіалізуємо і записуємо
            byte[] bsonBytes = data.ToBson();
            File.WriteAllBytes(filePath, bsonBytes);
        }

        public async Task<T?> LoadFromFileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // 1. Асинхронно зчитуємо всі байти з диска (звільняє UI потік під час I/O)
            byte[] bsonBytes = await File.ReadAllBytesAsync(filePath);

            // 2. Важку десеріалізацію великого масиву байтів виносимо у фоновий потік
            return await Task.Run(() => BsonSerializer.Deserialize<T>(bsonBytes));
        }

        public async Task SaveToFileAsync(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // 1. Перетворення об'єкта в байти виносимо у фоновий потік
            byte[] bsonBytes = await Task.Run(() => data.ToBson());

            // 2. Асинхронно записуємо масив байтів на диск
            await File.WriteAllBytesAsync(filePath, bsonBytes);
        }
    }

}

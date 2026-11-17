using Smec.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Smec.Services.DataServices
{
    public class XmlDataService<T> : IDataService<T> where T : class
    {
        // Свойства только для чтения
        public string FormatName => "XML файлы";
        public string DefaultExtension => ".xml";

        public bool CanImport => true;

        public bool CanExport => true;

        private static readonly DataContractSerializerSettings dcsSettings = new()
        {
            // Этот флаг заставляет XML-сериализатор автоматически разрешать циклы
            PreserveObjectReferences = true
        };
        private static readonly DataContractSerializer dcSerializer = new(typeof(T), dcsSettings);

        private static readonly XmlWriterSettings xmlWriteSetting = new()
        {
            Encoding = Encoding.UTF8,
            Indent = true
        };

        private static readonly XmlReaderSettings xmlReaderSetting = new()
        { 
  
        };

        public T? LoadFromFile(string filePath)
        {
            // Just in case
            ArgumentNullException.ThrowIfNull(filePath);

            //var serializer = new XmlSerializer(typeof(T));
            //// Явно указываем кодировку UTF8 для корректного чтения любых языков
            //using var reader = new StreamReader(filePath,   Encoding.UTF8);
            //return serializer.Deserialize(reader) as T;

            using var xmlReader = XmlReader.Create(filePath, xmlReaderSetting);
            return dcSerializer.ReadObject(xmlReader) as T;
        }

        public async Task<T?> LoadFromFileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            // Виносимо десеріалізацію у фоновий потік, щоб звільнити UI WPF
            return await Task.Run(() =>
            {
                //var serializer = new XmlSerializer(typeof(T));
                //using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: false);
                //using var streamReader = new StreamReader(fileStream, Encoding.UTF8);
                //return serializer.Deserialize(streamReader) as T;

                using var xmlReader = XmlReader.Create(filePath, xmlReaderSetting);
                return dcSerializer.ReadObject(xmlReader) as T;
            });

        }

        public void SaveToFile(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            //var serializer = new XmlSerializer(typeof(T));
            //using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            //serializer.Serialize(writer, data);

            using var xmlWriter = XmlWriter.Create(filePath, xmlWriteSetting);
            dcSerializer.WriteObject(xmlWriter, data);

        }

        public async Task SaveToFileAsync(string filePath, T data)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(filePath);

            // Виносимо серіалізацію у фоновий потік. Це повністю захистить UI від підвисань.
            await Task.Run(() =>
            {
                //var serializer = new XmlSerializer(typeof(T));
                //var settings = new XmlWriterSettings
                //{
                //    Encoding = Encoding.UTF8,
                //    Indent = true
                //};

                // useAsync: false всередині Task.Run, бо XmlSerializer все одно працює синхронно
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: false);
                
                //using var xmlWriter = XmlWriter.Create(fileStream, xmlWriteSetting);
                //serializer.Serialize(xmlWriter, data);

                using var xmlWriter = XmlWriter.Create(fileStream, xmlWriteSetting);
                dcSerializer.WriteObject(xmlWriter,data);

                xmlWriter.Flush(); // Синхронний Flush фонового потоку
            });
        }
    }
}

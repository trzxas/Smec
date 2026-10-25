using Emulation.Models;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Emulation.ViewModels
{
    // Этот класс для сохранения и загрузки всего проекта
    public class ProjectFile
    {
        // Сама модель (данные)
        public SysModel Model { get; set; } = new();

        // Сообщаем XML игнорировать  этот свойство
        [XmlIgnore]
        public Dictionary<string, string> ViewSettings { get; set; } = [];

        // Так как  XmlSerializer не умеет работать с Dictionary то используем класс для прокси-массива.
        // JSON и BSON его проигнорируют.
        [BsonIgnore]
        [JsonIgnore]
        [XmlArray("ViewSettings")]
        [XmlArrayItem("Setting")]
        public XmlKeyValuePair[] ViewSettingsXmlProxy
        {
            get => [.. ViewSettings.Select(k => new XmlKeyValuePair { Key = k.Key, Value = k.Value })];
            set => ViewSettings = value?.ToDictionary(x => x.Key, x => x.Value) ?? [];
        }

    }

    // Вспомогательный класс для прокси-массива
    public class XmlKeyValuePair
    {
        [XmlAttribute] public string Key { get; set; } = string.Empty;
        [XmlAttribute] public string Value { get; set; } = string.Empty;
    }
}

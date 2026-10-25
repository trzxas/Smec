using Emulation;
using Emulation.Models;
using Emulation.Services.DataServices;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Emulation.Models
{
    public class Node : NamedBase
    {
        //[JsonConverter(typeof(CompactPointConverter))]
        public System.Windows.Point Location { get; set { field = value; OnPropertyChanged(); } }

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        public OwnerTrackingCollection<NodePort, Node> Ports { get; private set; }

        // ================================================================= 
        // ОПТИМИЗИРОВАННЫЙ МАССИВ ИЗ 4-Х КОЛЛЕКЦИЙ ДЛЯ XAML
        // ================================================================= 
        [XmlIgnore]
        [BsonIgnore]
        [JsonIgnore]
        private readonly ObservableCollection<NodePort>[] _groupedPorts =
        [
        [], // Index 0: Left
        [], // Index 1: Right
        [], // Index 2: Top
        [], // Index 3: Bottom
        [], // Index 4: Center
    ];

        // Удобные публичные геттеры для биндинга в ItemsSource вашего XAML
        [XmlIgnore][BsonIgnore][JsonIgnore] public ObservableCollection<NodePort> LeftPorts => _groupedPorts[(int)SidePosition.Left];
        [XmlIgnore][BsonIgnore][JsonIgnore] public ObservableCollection<NodePort> RightPorts => _groupedPorts[(int)SidePosition.Right];
        [XmlIgnore][BsonIgnore][JsonIgnore] public ObservableCollection<NodePort> TopPorts => _groupedPorts[(int)SidePosition.Top];
        [XmlIgnore][BsonIgnore][JsonIgnore] public ObservableCollection<NodePort> BottomPorts => _groupedPorts[(int)SidePosition.Bottom];
        [XmlIgnore][BsonIgnore][JsonIgnore] public ObservableCollection<NodePort> CenterPorts => _groupedPorts[(int)SidePosition.Center];

        //// ширина правой стороны портов
        //public double RightSideWidth
        //{
        //    get;
        //    set
        //    {
        //        //if (Math.Abs(field - value) < 0.01) return;
        //        field = value;
        //        OnPropertyChanged();
        //    }
        //}

        // Конструктор
        public Node()
        {
            Ports = new OwnerTrackingCollection<NodePort, Node>(this);

            // Подписываемся на автоматическое распределение портов по сторонам
            Ports.CollectionChanged += Ports_CollectionChanged;
        }

        // Автоматический распределитель портов по 5-м массивам
        private void Ports_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var group in _groupedPorts) group.Clear();
                return;
            }

            // Удаление портов из их сторон при очистке/удалении ноды
            if (e.OldItems != null)
            {
                foreach (NodePort port in e.OldItems)
                {
                    _groupedPorts[(int)port.Side].Remove(port);
                }
            }

            // Добавление портов на нужную сторону (работает при импорте и в рантайме)
            if (e.NewItems != null)
            {
                foreach (NodePort port in e.NewItems)
                {
                    //int index = GetSideIndex(port.Side);
                    if (!_groupedPorts[(int)port.Side].Contains(port))
                    {
                        _groupedPorts[(int)port.Side].Add(port);
                    }
                }
            }
        }


        // Сериализаторы просто сохраняют и считывают этот плоский список.
        [BsonElement("Ports")]
        [JsonPropertyName("Ports")]
        [XmlArray("Ports")]
        [XmlArrayItem("NodePort")]
        public List<NodePort> SerializedPorts
        {
            get
            {
                // 1. EXPORT (сериализация)
                if (Ports != null && Ports.Count > 0)
                {
                    field.Clear();
                    field.AddRange(Ports);
                }

                // 2. IMPORT (десериализация)
                return field;
            }
            set => field = value ?? [];
        } = [];


        // Вызывается каскадно из SysModel.RestoreRelations()
        public void RestorePortsAndOwners()
        {
            if (SerializedPorts != null && SerializedPorts.Count > 0)
            {
                Ports.Clear(); // Это автоматически вызовет Reset и очистит _groupedPorts через событие!
                foreach (var port in SerializedPorts)
                {
                    if (port == null) continue;
                    port.Owner = this; // Восстанавливаем связь с текущей нодой
                    Ports.Add(port);   // Наполняем рабочую коллекцию (порты сами разлетятся по 4 массивам)
                }
                SerializedPorts.Clear(); // Чистим буфер
            }
        }
    }
}


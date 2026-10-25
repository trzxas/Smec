using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Xml.Linq;
using System.Xml.Serialization;
using Point = System.Windows.Point;

namespace Emulation.Models
{
    [XmlRoot("ModelConfiguration")]
    public class SysModel : Base
    {
        public string? Descripton { get; set { field = value; OnPropertyChanged(); } }


        [JsonInclude] // Принуждаем Json десериализовать свойство, так как по умолчанию private set он игнорирует
        public ObservableCollection<Node> Nodes { get; private set; } = [];

        public SysModel()
        {
            // Первичная подписка для коллекции по умолчанию
            Nodes.CollectionChanged += Nodes_CollectionChanged;
        }


        private void Nodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (Node node in e.OldItems)
                    node.PropertyChanged -= Node_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (Node node in e.NewItems)
                    node.PropertyChanged += Node_PropertyChanged;
            }

            OnPropertyChanged(nameof(Nodes));
        }

        private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Сигнал из глубины дерева дошел до SysModel! Извещаем ViewModel
            OnPropertyChanged(nameof(Nodes));
        }

        // НАШ ЕДИНЫЙ КОНТРОЛИРУЕМЫЙ МЕТОД ВОССТАНОВЛЕНИЯ СВЯЗЕЙ
        public void RestoreRelations()
        {
            if (Nodes == null) return;

            foreach (var node in Nodes)
            {
                // Явно приказываем каждой ноде восстановить порты и проставить Owner
                node.RestorePortsAndOwners();
            }
        }


        public void FillRandom(int nodeCount=5, int maxPort=12)
        {
            Descripton = $"System created at {DateTime.Now}";

            Random rnd = new();
            for (int i = 0; i < nodeCount; i++)
            {
                // 1. Сначала создаем сам узел
                var node = new Node()
                {
                    Location = new Point(rnd.NextDouble() * 700, rnd.NextDouble() * 400)
                };

                int jmax = rnd.Next(1,maxPort);
                // 2. Теперь, когда переменная node существует, передаем её как владельца
                for (int j = 0; j < jmax; j++)
                {
                    node?.Ports?.Add(new NodePort {Name=$"{i}/{j}", Side= (SidePosition)rnd.Next(4)});
                }

                node.Name = $"D{i}";

                // 3. Добавляем готовый узел в общую коллекцию
                Nodes.Add(node);
            }
        }

       
    }
}

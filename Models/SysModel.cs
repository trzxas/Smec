using MongoDB.Bson.Serialization.Attributes;
using Smec.Services.DataServices;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Smec.Models
{
    [XmlRoot("ModelConfiguration")]
    [DataContract]
    [BsonSerializer(typeof(BsonCustomSerializer))]
    public class SysModel : ObservableObject
    {
        [DataMember]
        public string? Descripton { get; set => SetProperty(ref field, value); }

        [DataMember(Order = 1)]
        [JsonInclude] // Принуждаем Json десериализовать свойство, так как по умолчанию private set он игнорирует
        [BsonElement(Order = 1)] // Главный
        public ObservableCollection<Node> Nodes { get; private set; } = [];

        [DataMember(Order = 2)]
        [JsonInclude] // Принуждаем Json десериализовать свойство, так как по умолчанию private set он игнорирует
        [BsonElement(Order = 2)] // Зависимый от портов Node
        public ObservableCollection<Link> Links { get; private set; } = [];


        public SysModel()
        {
            // Первичная подписка для коллекции по умолчанию
            Nodes.CollectionChanged += Nodes_CollectionChanged;
            Links.CollectionChanged += Links_CollectionChanged;
        }


        private void Nodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (Node node in e.OldItems)
                {
                    node.PropertyChanged -= Node_PropertyChanged;

                    // Удаляем все связи, подключённые к удаляемому узлу, и снимаем логические подписки
                    for (int i = Links.Count - 1; i >= 0; i--)
                    {
                        var link = Links[i];
                        if (link.PortA?.Owner == node || link.PortB?.Owner == node)
                        {
                            link.RemoveLogicalConnection();
                            Links.RemoveAt(i);
                        }
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (Node newNode in e.NewItems)
                {
                    newNode.AssignNextDesignator(Nodes);
                    newNode.PropertyChanged += Node_PropertyChanged;
                }
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

        private void Links_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (Link link in e.OldItems)
                    link.PropertyChanged -= Link_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (Link link in e.NewItems)
                    link.PropertyChanged += Link_PropertyChanged;
            }

            OnPropertyChanged(nameof(Links));
        }

        private void Link_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Сигнал из глубины дерева дошел до SysModel! Извещаем ViewModel
            OnPropertyChanged(nameof(Links));
        }


        public void SeedModel()
        {
            Random rnd = new();

            Descripton = $"Seeded model created at {DateTime.Now}";

            var node1 = Node.CreateDefaultInstance<L2OR>();
            node1.Name = node1.Designator = "D1";
            node1.Location = new GridPoint(GetRandomInt(100), GetRandomInt(50));
            Nodes.Add(node1);

            var node2 = Node.CreateDefaultInstance<L2OR>();
            node2.Name = node2.Designator= "D2";
            node2.Location = new GridPoint(GetRandomInt(100), GetRandomInt(50));
            Nodes.Add(node2);

            var node3 = Node.CreateDefaultInstance<L3OR>();
            node3.Name = node3.Designator = "D3";
            node3.Location = new GridPoint(GetRandomInt(100), GetRandomInt(50));
            Nodes.Add(node3);

            var node4 = Node.CreateDefaultInstance<L4OR>();
            node4.Name = node4.Designator = "D4";
            node4.Location = new GridPoint(GetRandomInt(100), GetRandomInt(50));
            Nodes.Add(node4);

            //var node = new OutlineNode() { Name = "CPU", Location = new Point(rnd.NextDouble() * 700, rnd.NextDouble() * 400) };
            //node.Ports.Add(new NodePort() { Name = "L1", Side = SidePosition.Left, Decorator = PortDecorator.Slash });
            //node.Ports.Add(new NodePort() { Name = "L2", Side = SidePosition.Left, Decorator = PortDecorator.BackSlash });
            //node.Ports.Add(new NodePort() { Name = "L3", Side = SidePosition.Left, Decorator = PortDecorator.Arrow });
            //node.Ports.Add(new NodePort() { Name = "R1", Side = SidePosition.Right });
            //node.Ports.Add(new NodePort() { Name = "R2", Side = SidePosition.Right, Decorator = PortDecorator.Circle });
            //node.Ports.Add(new NodePort() { Name = "T1", Side = SidePosition.Top });
            //node.Ports.Add(new NodePort() { Name = "T2", Side = SidePosition.Top, Decorator = PortDecorator.Arrow });
            //node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.Top, Decorator = PortDecorator.Slash });
            //node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.Top });
            //node.Ports.Add(new NodePort() { Name = "B1", Side = SidePosition.Bottom });
            //node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.Bottom, Decorator = PortDecorator.Circle });

            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.LeftTop });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.LeftTop });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.RightTop });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.RightTop });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.LeftBottom });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.LeftBottom });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.RightBottom });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.RightBottom });

            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.TopLeft });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.TopLeft });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.TopRight });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.TopRight });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.BottomLeft });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.BottomLeft });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.BottomRight });
            ////node.Ports.Add(new NodePort() { Name = "", Side = SidePosition.BottomRight });

            //node.Ports.Add(new NodePort() { Name = "LTC", Side = SidePosition.LeftTopCorner });
            //node.Ports.Add(new NodePort() { Name = "RTC", Side = SidePosition.RightTopCorner });
            //node.Ports.Add(new NodePort() { Name = "LBC", Side = SidePosition.LeftBottomCorner });
            //node.Ports.Add(new NodePort() { Name = "RBC", Side = SidePosition.RightBottomCorner });
            //Nodes.Add(node);


            Links.Add(new Link() { Name = "Link1", PortA = Nodes[0].Ports[2], PortB = Nodes[1].Ports[0] });
            Links.Add(new Link() { Name = "Link2", PortA = Nodes[1].Ports[2], PortB = Nodes[2].Ports[0] });
            Links.Add(new Link() { Name = "Link3", PortA = Nodes[1].Ports[3], PortB = Nodes[3].Ports[0] });
            Links.Add(new Link() { Name = "Link4", PortA = Nodes[0].Ports[3], PortB = Nodes[3].Ports[3] , Activity=false });
        }

        private int GetRandomInt(int max)
        {
            Random rnd = new();
            int res = rnd.Next(max);
            int i = 4 - res % 4;
            return res+i;
        }

    }
}

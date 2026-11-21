using MongoDB.Bson.Serialization.Attributes;
using Smec;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using static System.Windows.Forms.AxHost;

namespace Smec.Models
{
    public enum SidePosition
    {
        Center,                                         // по центру
        Left, Top, Right, Bottom,                       // по сторонам: влево, вверх, вправо, вниз

        //LeftTop, RightTop, RightBottom, LeftBottom

        //LeftTop, TopRight, RightBottom, BottomLeft,     // по углам: влево, вверх, вправо, вниз
        //TopLeft, RightTop, BottomRight, LeftBottom,     // по углам: вверх,вправо, вниз, влево

        LeftTopCorner, RightTopCorner, RightBottomCorner, LeftBottomCorner, // выводы под углом 45 градусов
    }


    public enum PortMode
    {
        In, Out, InOut
    }

    public enum PortState
    {
        Undefined, Low, WeakLow, WeakHigh, High, 

    }

    public enum PortDecorator
    {
        None, Circle, Slash, BackSlash, Arrow
    }

   

    public partial class NodePort : NamedBase, IOwnable<Node>, ICloneable
    {
        private string _nodeId = string.Empty;
        /// <summary>
        /// Уникальный ID компонента (Node.Id), к которому подключён этот конец провода.
        /// </summary>
        [DataMember]
        public string NodeId
        {
            get => Owner?.Id ?? _nodeId;
            set => _nodeId = value ?? string.Empty;
        }

        /// <summary>
        /// Номер или имя пина внутри символа (совпадает с PinDefinition.Number, например: "1", "2", "Y").
        /// </summary>
        [DataMember]
        public string PinNumber { get; set; } = string.Empty;

        /// <summary>
        /// Удобная проверка, подключён ли порт к реальному узлу (или висит в воздухе).
        /// </summary>
        [IgnoreDataMember]
        public bool IsAttached => (Owner!=null) && !string.IsNullOrEmpty(PinNumber);

        //TODO Можно сделать универсальный [IgnoreAll] для [BsonIgnore][XmlIgnore][JsonIgnore]
        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public Node? Owner { get; set; }

       
        // Координата порта в узлах сетки (а не в пикселях!)
        [BsonIgnore, XmlIgnore, JsonIgnore, IgnoreDataMember]
        public GridPoint Location
        {
            get;
            set => SetProperty(ref field, value);
        }

        [XmlAttribute]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SidePosition Side { get; set; }

        [XmlAttribute]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PortMode Mode
        {
            get;
            set => SetProperty(ref field, value);
        }

        [XmlAttribute]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PortDecorator Decorator { get; set; }

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public PortState State
        {
            get;
            set
            {
                if (field == value) return;
                SetProperty(ref field, value);
                _stateChanged?.Invoke(this, field);
            }
        }

        [IgnoreDataMember]
        public bool IsDriving { get; set; }

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public ObjectStatus? HighlightStatus
        {
            get;
            set => SetProperty(ref field, value);
        }


        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        private EventHandler<PortState>? _stateChanged;

        public event EventHandler<PortState> StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }

        /// <summary>
        /// Очищает всех подписчиков на изменение состояния (вызывается в PostSimulate)
        /// </summary>
        public void RemoveAllInvocation()
        {
            _stateChanged = null;
        }

        public NodePort ClonePort()
        {
            return new NodePort
            {
                Name = this.Name,
                Side = this.Side,
                Mode = this.Mode,
                Decorator = this.Decorator,
            };
        }

        // Реализация интерфейса ICloneable
        public object Clone() => ClonePort();


    }
}

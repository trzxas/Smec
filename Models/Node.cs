using MongoDB.Bson.Serialization.Attributes;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Smec.Models
{

    /// <summary>
    /// Целочисленная координата в узлах сетки (без привязки к пикселям и WPF).
    /// </summary>
    public readonly record struct GridPoint(int X, int Y);

    /// <summary>
    /// Модель элемента (компонента) на принципиальной схеме.
    /// </summary>

    [KnownType(nameof(GetKnownTypes))] // Указываем статический метод, который вернет список типов
    public partial class Node : NamedBase, ICloneable
    {

        // ======================Основные свойства===========================================

        /// <summary>
        /// Уникальный идентификатор конкретного экземпляра на схеме (для связей, Undo/Redo и поиска).
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Ссылка на тип символа в библиотеке (например: "lib_passives:R_IEC" или "lib_logic:OR_2").
        /// </summary>
        public virtual string SymbolId { get; set; } = string.Empty;

        /// <summary>
        /// Позиция точки привязки (Origin) элемента в шагах сетки.
        /// </summary>
        public GridPoint Location { get; set => SetProperty(ref field, value); }

        /// <summary>
        /// Угол поворота элемента в градусах (0, 90, 180, 270).
        /// </summary>
        public int Rotation { get; set => SetProperty(ref field, value);  }

        /// <summary>
        /// Зеркальное отражение по горизонтали (по оси X).
        /// </summary>
        public bool MirrorX { get; set => SetProperty(ref field, value); }

        /// <summary>
        /// Зеркальное отражение по вертикали (по оси Y).
        /// </summary>
        public bool MirrorY { get; set => SetProperty(ref field, value); }

        /// <summary>
        /// Порядок отрисовки (слой перекрытия элементов на 2D-холсте).
        /// </summary>
        public int ZIndex { get; set => SetProperty(ref field, value); }


        // Каждый наследник (ResistorNode, CapacitorNode и т.д.) переопределяет только букву
        protected virtual string DefaultPrefix => String.Empty;

        /// <summary>
        /// Позиционное обозначение на схеме (например: "R1", "U3", "C12").
        /// </summary>
        private string? _designator;
        public string Designator
        {
            // Если префикс задан — формируем "R?", "C?"; если префикс пустой (как у GND) — оставляем пустую строку ""
            get => _designator ??= string.IsNullOrWhiteSpace(DefaultPrefix) ? string.Empty : $"{DefaultPrefix}?";

            // Если передали пустую строку (стерли в UI), сбрасываем в дефолт (или в пустую строку при отсутствии префикса)
            set
            {
                string defaultVal = string.IsNullOrWhiteSpace(DefaultPrefix) ? string.Empty : $"{DefaultPrefix}?";
                SetProperty(ref _designator, string.IsNullOrWhiteSpace(value) ? defaultVal : value);
            }
        }


        // Удобное свойство для проверки, присвоен ли узлу реальный номер или он еще дефолтный
        public bool IsDefaultDesignator => Designator == $"{DefaultPrefix}?";



        /// <summary>
        /// Номинал или значение элемента (например: "10k", "100n", "5V", "GND").
        /// </summary>
        public string Value { get; set => SetProperty(ref field, value); } = string.Empty;

        /// <summary>
        /// Дополнительные параметры (например, для SPICE-моделей, частоты генератора или пользовательских полей).
        /// </summary>
        //TODO Пока никак не использовался!
        public Dictionary<string, string> Properties { get; set; } = [];



        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public OwnerTrackingCollection<NodePort, Node> Ports { get; private set; }

        // =================================================================


        [XmlIgnore]
        [BsonIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public virtual object? CustomTemplate => null;

        [XmlIgnore]
        [BsonIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public virtual string? CustomBodyTemplate => null;




        // Метод автоматически находит все классы, унаследованные от Node
        private static IEnumerable<Type> GetKnownTypes()
        {
            return Assembly.GetExecutingAssembly()
                           .GetTypes()
                           .Where(t => t.IsSubclassOf(typeof(Node)));
        }





        // Конструктор
        public Node()
        {
            Ports = new OwnerTrackingCollection<NodePort, Node>(this);

            // Это пока ни где не используется!
            // Подписываемся на автоматическое распределение портов по сторонам
            Ports.CollectionChanged += Ports_CollectionChanged;

            for (int i = 0; i < _groupedPorts.Length; i++)
            {
                _groupedPorts[i] = []; // либо = new ObservableCollection<NodePort>();
            }

            // Создаем группы портов
            var types = Enum.GetValues<SidePosition>();
            GroupedPorts = new PortGroup[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                GroupedPorts[i] = new PortGroup(types[i]);

            }



        }

        // Автоматический распределитель портов по соответствующим массивам
        private void Ports_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var group in _groupedPorts) group.Clear();
                foreach (var group in GroupedPorts) group.Ports.Clear();
                return;
            }

            // Удаление портов из их сторон при очистке/удалении ноды
            if (e.OldItems != null)
            {
                foreach (NodePort port in e.OldItems)
                {
                    int index = (int)port.Side;

                    _groupedPorts[index].Remove(port);
                    GroupedPorts[index].Ports.Remove(port);
                }
            }

            // Добавление портов в нужную Группу (работает при импорте и в рантайме)
            if (e.NewItems != null)
            {
                int enumLength = Enum.GetValues<SidePosition>().Length;
                foreach (NodePort port in e.NewItems)
                {
                    int index = (int)port.Side % enumLength; // защита от неправильного индекса при импорте

                    // Если дубликаты исключены на уровне бизнес-логики, 
                    // условия "if (!Contains)" можно полностью удалить для скорости
                    if (!_groupedPorts[index].Contains(port))
                    {
                        _groupedPorts[index].Add(port);
                    }

                    GroupedPorts[index].Ports.Add(port);

                }
            }
        }

        /// <summary>
        /// Автоматически назначает первый свободный порядковый номер (например, D1, R2, G1),
        /// если у элемента ещё не задан конкретный Designator.
        /// </summary>
        public void AssignNextDesignator(IEnumerable<Node> existingNodes)
        {
            // 1. Элементы без префикса (GND, шины питания) не нумеруются
            if (string.IsNullOrWhiteSpace(DefaultPrefix))
            {
                Designator = string.Empty;
                return;
            }

            // 2. Если номер уже назначен (например, схема загружена из файла) — не трогаем его
            if (!string.IsNullOrWhiteSpace(_designator) && !_designator.EndsWith('?'))
                return;

            // 3. Собираем все занятые номера для данного префикса (D1, D2, ...)
            var usedNumbers = new HashSet<int>();
            foreach (var node in existingNodes)
            {
                if (node == this || string.IsNullOrWhiteSpace(node.Designator))
                    continue;

                if (node.Designator.StartsWith(DefaultPrefix, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(node.Designator.AsSpan(DefaultPrefix.Length), out int num) &&
                    num > 0)
                {
                    usedNumbers.Add(num);
                }
            }

            // 4. Находим минимальный свободный номер, начиная с 1
            int nextNumber = 1;
            while (usedNumbers.Contains(nextNumber))
            {
                nextNumber++;
            }

            Designator = $"{DefaultPrefix}{nextNumber}";
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


        // Виртуальный метод настройки портов (по умолчанию пустой, наследники переопределяют)
        protected virtual void CreatePorts() { }

        // Единый статический фабричный метод по Type для ЛЮБЫХ наследников Node
        // Создаёт экземпляр РОВНО ОДИН РАЗ и сразу строит порты
        public static Node? CreateDefault(Type type)
        {
            if (Activator.CreateInstance(type) is Node instance)
            {
                instance.CreatePorts();
                return instance;
            }
            return null;
        }

        // 2. Обобщённый метод <T> 
        public static T CreateDefaultInstance<T>() where T : Node, new()
        {
            return (T)CreateDefault(typeof(T))!;
        }

        // 3. Метод экземпляра  (переиспользует статический метод)
        protected internal virtual Node CreateDefault()
        {
            return CreateDefault(GetType())!;
        }


        // 1. Создаем пустой экземпляр реального типа (L2OR, L3OR и др.) без портов по умолчанию
        protected virtual Node CreateEmptyInstance()
        {
            return (Node)Activator.CreateInstance(GetType())!;
        }

        // 2. Виртуальный метод для копирования специфических свойств (если появятся у наследников)
        protected virtual void CopyPropertiesTo(Node target)
        {
            target.Name = $"{Name}_copy";
            target.Rotation = Rotation;
            target.MirrorX = MirrorX;
            target.MirrorY = MirrorY;
            target.Location = new GridPoint(Location.X + 2, Location.Y + 2);
            target.Designator = string.Empty;
            target.Value = Value;
            target.Properties = new Dictionary<string, string>(Properties);

            target.Ports.Clear(); // На всякий случай, хотя Node создается без портов 
            foreach (var port in Ports)
            {
                target.Ports.Add(port.ClonePort());
            }
        }

        // 3. Единый метод клонирования
        public object Clone()
        {
            Node clone = CreateEmptyInstance();
            CopyPropertiesTo(clone);
            return clone;
        }


        // Инициализация состояния перед симуляцией
        public virtual void InitState() { }

        // Обновление при симуляции
        public virtual void UpdateState(TimeSpan elapsed) { }


        #region Массив Групп портов ДЛЯ XAML тестовый.  Не используем пока!
        // ================================================================= 
        [XmlIgnore]
        [IgnoreDataMember]
        [BsonIgnore]
        [JsonIgnore]
        public PortGroup[] GroupedPorts { get; }

        //[IgnoreAll]
        //private PortGroup[] _groupedPorts;

        [XmlIgnore]
        [BsonIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        private static readonly int SideTypesCount = Enum.GetValues<SidePosition>().Length;


        [XmlIgnore]
        [IgnoreDataMember]
        [BsonIgnore]
        [JsonIgnore]
        public readonly ObservableCollection<NodePort>[] _groupedPorts = new ObservableCollection<NodePort>[SideTypesCount];

        #endregion


    }

    public class PortGroup(SidePosition typeSide)
    {
        const double PortOffset = 6.0; // длина пина
        public SidePosition Side { get; } = typeSide;
        public ObservableCollection<NodePort> Ports { get; } = [];

        public double X
        {
            get
            {
                switch (Side)
                {
                    case SidePosition.Left:
                    //case SidePosition.LeftTop:
                    //case SidePosition.LeftBottom:
                    case SidePosition.LeftTopCorner:
                    case SidePosition.LeftBottomCorner:
                        return -PortOffset;

                    case SidePosition.Right:
                    //case SidePosition.RightTop:
                    //case SidePosition.RightBottom:
                    case SidePosition.RightTopCorner:
                    case SidePosition.RightBottomCorner:
                        return PortOffset;

                    default:
                        return 0;
                }
            }
        }
        public double Y
        {
            get
            {
                switch (Side)
                {
                    case SidePosition.Top:
                    //case SidePosition.TopLeft:
                    //case SidePosition.TopRight:
                    case SidePosition.LeftTopCorner:
                    case SidePosition.RightTopCorner:
                        return -PortOffset;

                    case SidePosition.Bottom:
                    //case SidePosition.BottomLeft:
                    //case SidePosition.BottomRight:
                    case SidePosition.LeftBottomCorner:
                    case SidePosition.RightBottomCorner:
                        return PortOffset;

                    default:
                        return 0;
                }
            }
        }
    }

}


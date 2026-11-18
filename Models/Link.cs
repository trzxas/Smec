using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Smec.Models
{
    public enum ObjectStatus
    {
        Normal, Warning, Error
    }

    public enum LinkRoutingMode
    {
        Direct,          // Прямая линия
        Orthogonal,      // Строго 90° по сетке
        Diagonal45       // Сглаженная трассировка 90° + 45° (идеально для триггеров и шин)
    }

    [DataContract]
    public class Link : NamedBase
    {

        public Link()
        {
            SegmentOffsets.CollectionChanged += OnSegmentOffsetsChanged;
        }

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Имя электрической цепи (например, "VCC", "GND", "NET_1"). Заполняется при трассировке/симуляции.
        /// </summary>
        public string NetId { get; set; } = string.Empty;

        /// <summary>
        /// Упорядоченный список вершин провода в единицах сетки (от начала до конца со всеми изгибами).
        /// </summary>
        public List<GridPoint> Points { get; set; } = [];

        [DataMember]
        public NodePort PortA { get; set; } = new();

        [DataMember]
        public NodePort PortB { get; set; } = new();

        [DataMember]
        public bool Activity
        {
            get;
            set => SetProperty(ref field, value);
        } = true;

        [DataMember]
        public LinkRoutingMode RoutingMode
        {
            get;
            set => SetProperty(ref field, value);
        } = LinkRoutingMode.Orthogonal;


        [DataMember]
        // Массив ручных смещений при редактировании Линка
        public ObservableCollection<double> SegmentOffsets
        {
            get;
            set
            {
                field?.CollectionChanged -= OnSegmentOffsetsChanged;
                field = value ?? [];
                field.CollectionChanged += OnSegmentOffsetsChanged;
                OnPropertyChanged();
            }
        } = [];

        private void OnSegmentOffsetsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(SegmentOffsets));


        public double GetSegmentOffset(int index) =>
        index >= 0 && index < SegmentOffsets.Count ? SegmentOffsets[index] : 0;

        public void SetSegmentOffset(int index, double value)
        {
            if (index < 0) return;

            while (SegmentOffsets.Count <= index)
                SegmentOffsets.Add(0);

            if (Math.Abs(SegmentOffsets[index] - value) > 0.001)
                SegmentOffsets[index] = value;
        }

        public void ResetSegmentOffsets()
        {
            for (int i = 0; i < SegmentOffsets.Count; i++)
            {
                if (Math.Abs(SegmentOffsets[i]) > 0.001)
                    SegmentOffsets[i] = 0;
            }
        }

        //// Ручное смещение центрального  сегмента (в клетках сетки, с шагом 0.5)
        //public double ChannelOffset
        //{
        //    get;
        //    set => SetProperty(ref field, value);
        //} = 0;


        //public double ChannelOffsetY
        //{
        //    get;
        //    set => SetProperty(ref field, value);
        //} = 0;


        //public double ChannelOffset2
        //{
        //    get;
        //    set => SetProperty(ref field, value);
        //} = 0;

        [IgnoreDataMember]
        public ObjectStatus LinkStatus { get; private set; } = ObjectStatus.Normal;

        [IgnoreDataMember]
        private bool _isConnected;


        /// <summary>
        /// Текущий логический уровень сигнала на проводе (используется для цвета линии в XAML)
        /// </summary>
        [IgnoreDataMember]
        public PortState State
        {
            get
            {
                if (!_isConnected || !Activity || PortA == null || PortB == null) return PortState.Undefined;

                if (CanDrive(PortA) && PortA.State != PortState.Undefined) return PortA.State;
                if (CanDrive(PortB) && PortB.State != PortState.Undefined) return PortB.State;

                return PortState.Undefined;
            }

        }

        public void AddLogicalConnection()
        {
            if (_isConnected || !Activity || PortA == null || PortB == null) return;

            // 1. Подписываемся на изменение уровней (StateChanged)
            PortA.StateChanged -= OnPortAStateChanged;
            PortB.StateChanged -= OnPortBStateChanged;

            PortA.StateChanged += OnPortAStateChanged;
            PortB.StateChanged += OnPortBStateChanged;

            // 2. ОБЯЗАТЕЛЬНО подписываемся на PropertyChanged, чтобы ловить смену Mode (In <-> Out)
            PortA.PropertyChanged -= OnPortPropertyChanged;
            PortB.PropertyChanged -= OnPortPropertyChanged;

            PortA.PropertyChanged += OnPortPropertyChanged;
            PortB.PropertyChanged += OnPortPropertyChanged;

            _isConnected = true;

            // Синхронизируем начальное состояние
            SyncStates();
        }

        public void RemoveLogicalConnection()
        {
            if (!_isConnected) return;

            if (PortA != null)
            {
                PortA.StateChanged -= OnPortAStateChanged;
                PortA.PropertyChanged -= OnPortPropertyChanged;
            }

            if (PortB != null)
            {
                PortB.StateChanged -= OnPortBStateChanged;
                PortB.PropertyChanged -= OnPortPropertyChanged;
            }

            _isConnected = false;

            // Возвращаем визуальный вид провода в исходное состояние
            LinkStatus = ObjectStatus.Normal;
            OnPropertyChanged(nameof(LinkStatus));
            OnPropertyChanged(nameof(State));
        }

        /// <summary>
        /// Синхронизирует состояния портов с учетом их ТЕКУЩИХ режимов (In / Out / InOut)
        /// </summary>
        private void SyncStates()
        {
            if (PortA == null || PortB == null) return;

            if (!IsConflict())
            {
                if (CanDrive(PortA) && CanReceive(PortB))
                {
                    PortB.State = PortA.State;
                }
                else if (CanDrive(PortB) && CanReceive(PortA))
                {
                    PortA.State = PortB.State;
                }
            }

            UpdateVisualProperties();
        }

        private void OnPortAStateChanged(object? sender, PortState newState)
        {
            if (!Activity || PortA == null || PortB == null) return;

            // Передаем сигнал от A к B, ТОЛЬКО если A сейчас может выдавать, а B может принимать (и нет КЗ)
            if (CanDrive(PortA) && CanReceive(PortB) && !IsConflict())
            {
                PortB.State = newState;
            }
            UpdateVisualProperties();
        }

        private void OnPortBStateChanged(object? sender, PortState newState)
        {
            if (!Activity || PortA == null || PortB == null) return;

            // Передаем сигнал от B к A, ТОЛЬКО если B сейчас может выдавать, а A может принимать (и нет КЗ)
            if (CanDrive(PortB) && CanReceive(PortA) && !IsConflict())
            {
                PortA.State = newState;
            }
            UpdateVisualProperties();
        }

        private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodePort.Mode) && _isConnected && Activity)
            {
                SyncStates();
            }
        }



        // Может ли порт сейчас выдавать сигнал в линию
        private static bool CanDrive(NodePort port)
        {
            //return port.Mode == PortMode.Out
            //                || (port.Mode == PortMode.InOut && port.State != PortState.Undefined);
            return true;
        }

        // Может ли порт сейчас принимать сигнал из линии
        private static bool CanReceive(NodePort port)
        {
            return true;

            //return port.Mode == PortMode.In
            //                || port.Mode == PortMode.InOut;
        }

        private bool IsConflict()
        {
            if (PortA == null || PortB == null) return false;

            bool aActive = PortA.Mode == PortMode.Out;//  CanDrive(PortA) && PortA.State != PortState.Undefined;
            bool bActive = PortB.Mode == PortMode.Out;//  CanDrive(PortB) && PortB.State != PortState.Undefined;

            // КЗ, если оба в режиме передачи с определенным уровнем, и уровни не совпадают
            return aActive && bActive && PortA.State != PortB.State;
        }

        private void UpdateVisualProperties()
        {
            LinkStatus = (Activity && IsConflict()) ? ObjectStatus.Error : ObjectStatus.Normal;
            OnPropertyChanged(nameof(LinkStatus));
            OnPropertyChanged(nameof(State));
        }
    }

}

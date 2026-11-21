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
        Diagonal       // Сглаженная трассировка 90° + 45° (идеально для триггеров и шин)
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



        [IgnoreDataMember]
        public ObjectStatus LinkStatus { get; private set; } = ObjectStatus.Normal;

        [IgnoreDataMember]
        private bool _isConnected;

       

        [IgnoreDataMember]
        public PortState State { get; private set; } = PortState.Undefined;

        /// <summary>
        /// Вызывается из ElectricalNet.Evaluate() на каждом такте симуляции,
        /// чтобы обновить цвет и статус провода.
        /// </summary>
        public void SetSimulatedState(PortState state, ObjectStatus status)
        {
            if (State != state)
            {
                State = state;
                OnPropertyChanged(nameof(State));
            }

            if (LinkStatus != status)
            {
                LinkStatus = status;
                OnPropertyChanged(nameof(LinkStatus));
            }
        }

        /// <summary>
        /// Сбрасывает визуальное состояние провода при остановке симуляции или отключении связи.
        /// </summary>
        public void ResetSimulatedState() =>
            SetSimulatedState(PortState.Undefined, ObjectStatus.Normal);

    }

}

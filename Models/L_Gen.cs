using MongoDB.Bson.Serialization.Attributes;
using Smec.Helpers;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Smec.Models
{
    [DataContract]
    public class L_Gen : Node
    {
        public override string SymbolId => "Sourcer";
        protected override string DefaultPrefix => "G";

        private TimeSpan _lastUpdated = TimeSpan.Zero;
        private TimeSpan _cachedPeriod = TimeSpan.FromMilliseconds(1000);

        public L_Gen()
        {
            Description = "Генератор логических сигналов положительной полярности";
            CategoryName = "Sourcer";
            EnsureDefaultProperties();
        }

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public double Frequency
        {
            get => GetDoubleProperty("Frequency", 1.0);
            set
            {
                double clamped = Math.Clamp(value, 0.01, 1_000_000.0);
                SetDoubleProperty("Frequency", clamped);
                _cachedPeriod = TimeSpan.FromSeconds(1.0 / clamped);
                OnPropertyChanged(nameof(Period));
            }
        }

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public TimeSpan Period
        {
            get => _cachedPeriod;
            set
            {
                if (value.TotalSeconds > 0)
                    Frequency = 1.0 / value.TotalSeconds;
            }
        }

        protected override void CreatePorts()
        {
            Ports.Add(new NodePort
            {
                PinNumber = "1",
                Mode = PortMode.Out,
                Side = SidePosition.Right
            });
        }

        public override void EnsureDefaultProperties() => Frequency = Frequency;
        protected override string FormatDisplayValue() => EngineeringUnitHelper.Format(Frequency, "Hz");


        public override void Init()
        {
            base.Init();
            _lastUpdated = TimeSpan.Zero;
            _cachedPeriod = TimeSpan.FromSeconds(1.0 / Frequency);
           
            if (Ports.Count > 0)
                Ports[0].State = PortState.Low;
        }

        protected override void OnValueEdited(string newValue)
        {
            if (EngineeringUnitHelper.TryParse(newValue, out double hz) && hz > 0)
                Frequency = hz;
        }

        public override void UpdateState(TimeSpan elapsed)
        {
            if (elapsed - _lastUpdated >= _cachedPeriod)
            {
                var outPort = Ports[0];
                outPort.State = outPort.State == PortState.Low ? PortState.High : PortState.Low;
                _lastUpdated = elapsed;
            }
        }
    }
}

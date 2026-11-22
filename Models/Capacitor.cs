using MongoDB.Bson.Serialization.Attributes;
using Smec.Helpers;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Smec.Models
{
    [DataContract]
    public class Capacitor : TwoTerminalNode
    {
        public override string CategoryName => nameof(Capacitor);
        public override string SymbolId => "C_IEC";
        protected override string DefaultPrefix => "C";

        public Capacitor()
        {
            EnsureDefaultProperties();
        }

        public override void EnsureDefaultProperties() => Capacitance = Capacitance;
        protected override string FormatDisplayValue() => EngineeringUnitHelper.Format(Capacitance, "F");

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public double Capacitance
        {
            get => GetDoubleProperty("Capacitance", 10e-6); // 10 µF по умолчанию
            set
            {
                SetDoubleProperty("Capacitance", Math.Max(1e-12, value));
                OnPropertyChanged(nameof(PulseDurationMs));
            }
        }


        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public double PulseDurationMs => Math.Clamp(Capacitance * 15_000.0 * 1000.0, 20.0, 5000.0);


        protected override void OnValueEdited(string newValue)
        {
            if (EngineeringUnitHelper.TryParse(newValue, out double farads) && farads > 0)
                Capacitance = farads;
        }

        private PortState _lastInput = PortState.Undefined;
        private TimeSpan _pulseEnd = TimeSpan.Zero;

        protected override PortState ComputeOutputState(PortState inputState, TimeSpan elapsed)
        {
            PortState normalizedInput = inputState switch
            {
                PortState.High or PortState.WeakHigh => PortState.High,
                PortState.Low or PortState.WeakLow => PortState.Low,
                _ => PortState.Undefined
            };

            if (normalizedInput != _lastInput && _lastInput != PortState.Undefined && normalizedInput != PortState.Undefined)
                _pulseEnd = elapsed + TimeSpan.FromMilliseconds(PulseDurationMs);

            _lastInput = normalizedInput;

            return elapsed <= _pulseEnd ? normalizedInput : PortState.Undefined;
        }

        protected override void OnInputLost()
        {
            _lastInput = PortState.Undefined;
            _pulseEnd = TimeSpan.Zero;
        }
    }

}


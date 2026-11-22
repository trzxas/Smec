using MongoDB.Bson.Serialization.Attributes;
using Smec.Helpers;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Smec.Models
{
    [DataContract]
    public class Inductor : TwoTerminalNode
    {
        public override string CategoryName => nameof(Inductor);
        public override string SymbolId => "L_IEC";
        protected override string DefaultPrefix => "L";

        public Inductor()
        {
            EnsureDefaultProperties();
        }

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public double Inductance
        {
            get => GetDoubleProperty("Inductance", 0.1); // 100 mH по умолчанию
            set
            {
                SetDoubleProperty("Inductance", Math.Max(1e-9, value));
                OnPropertyChanged(nameof(DelayMs));
            }
        }

        public override void EnsureDefaultProperties() => Inductance = Inductance;
        protected override string FormatDisplayValue() => EngineeringUnitHelper.Format(Inductance, "H");

        [BsonIgnore]
        [XmlIgnore]
        [JsonIgnore]
        [IgnoreDataMember]
        public double DelayMs => Math.Clamp(Inductance * 1200.0, 20.0, 5000.0);

        protected override void OnValueEdited(string newValue)
        {
            if (EngineeringUnitHelper.TryParse(newValue, out double henries) && henries > 0)
                Inductance = henries;
        }

        private PortState _pending = PortState.Undefined;
        private PortState _established = PortState.Undefined;
        private TimeSpan _changeStart = TimeSpan.Zero;

        protected override PortState ComputeOutputState(PortState inputState, TimeSpan elapsed)
        {
            if (inputState != _pending)
            {
                _pending = inputState;
                _changeStart = elapsed;
            }

            if (elapsed - _changeStart >= TimeSpan.FromMilliseconds(DelayMs))
                _established = _pending;

            return _established;
        }

        protected override void OnInputLost()
        {
            _pending = PortState.Undefined;
            _established = PortState.Undefined;
            _changeStart = TimeSpan.Zero;
        }
    }

}


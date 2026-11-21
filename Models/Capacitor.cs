using System.Runtime.Serialization;

namespace Smec.Models
{
    public class Capacitor : TwoTerminalNode
    {
        public override string CategoryName => nameof(Capacitor);
        public override string SymbolId => "C_IEC";
        protected override string DefaultPrefix => "C";

        [DataMember]
        public double PulseDurationMs { get; set; } = 150;

        private PortState _lastInput = PortState.Undefined;
        private TimeSpan _pulseEnd = TimeSpan.Zero;

        protected override PortState ComputeOutputState(PortState inputState, TimeSpan elapsed)
        {
            // Приводим слабый вход к логическому уровню для корректного определения фронта
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

        protected override void OnInputLost() => _lastInput = PortState.Undefined;
    }

}


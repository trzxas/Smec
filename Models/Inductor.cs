using System.Runtime.Serialization;

namespace Smec.Models
{
    public class Inductor : TwoTerminalNode
    {
        public override string CategoryName => nameof(Inductor);
        public override string SymbolId => "L_IEC";
        protected override string DefaultPrefix => "L";

        [DataMember]
        public double DelayMs { get; set; } = 120;

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
        }
    }

}


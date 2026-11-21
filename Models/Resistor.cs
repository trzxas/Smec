using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class Resistor : TwoTerminalNode
    {
        public override string CategoryName => nameof(Resistor);
        public override string SymbolId => "R_IEC";
        protected override string DefaultPrefix => "R";

        protected override PortState ComputeOutputState(PortState inputState, TimeSpan elapsed) => inputState switch
        {
            PortState.High or PortState.WeakHigh => PortState.WeakHigh,
            PortState.Low or PortState.WeakLow => PortState.WeakLow,
            _ => PortState.Undefined
        };
    }
}

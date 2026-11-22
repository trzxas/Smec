using Smec.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Smec.Models
{
    [DataContract]
    public class Resistor : TwoTerminalNode
    {
        public override string CategoryName => nameof(Resistor);
        public override string SymbolId => "R_IEC";
        protected override string DefaultPrefix => "R";

        public Resistor()
        {
            EnsureDefaultProperties();
        }

        public override void EnsureDefaultProperties() => Resistance = Resistance;
        protected override string FormatDisplayValue() => EngineeringUnitHelper.Format(Resistance, "Ω");


        public double Resistance
        {
            get => GetDoubleProperty("Resistance", 1000.0); // 1 кОм в Омах
            set => SetDoubleProperty("Resistance", Math.Max(0.1, value));
        }

        protected override void OnValueEdited(string newValue)
        {
            if (EngineeringUnitHelper.TryParse(newValue, out double ohms) && ohms > 0)
                Resistance = ohms;
        }

        protected override PortState ComputeOutputState(PortState inputState, TimeSpan elapsed) => inputState switch
        {
            PortState.High or PortState.WeakHigh => PortState.WeakHigh,
            PortState.Low or PortState.WeakLow => PortState.WeakLow,
            _ => PortState.Undefined
        };

        
    }
}

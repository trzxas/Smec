using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class Resistor : Node
    {

        public override string CategoryName => nameof(Resistor);
        public override string SymbolId=> "R_IEC";
        protected override string DefaultPrefix => "R";


        protected override void CreatePorts()
        {
            Ports.Add(new NodePort() { Mode = PortMode.InOut,PinNumber="1", Side = SidePosition.Left });
            Ports.Add(new NodePort() { Mode = PortMode.InOut,PinNumber="2", Side = SidePosition.Right });
        }
        
        
    }
}

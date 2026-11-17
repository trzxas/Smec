using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class Inductor : Node
    {

        public override string CategoryName => nameof(Inductor);
        public override string SymbolId=> "L_IEC";
        protected override string DefaultPrefix => "L";


        protected override void CreatePorts()
        {
            Ports.Add(new NodePort() { Mode = PortMode.InOut,PinNumber="1", Side = SidePosition.Left });
            Ports.Add(new NodePort() { Mode = PortMode.InOut,PinNumber="2", Side = SidePosition.Right });
        }
        
        
    }
}

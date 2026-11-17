namespace Smec.Models
{
    public class Capacitor : Node
    {

        public override string CategoryName => nameof(Capacitor);
        public override string SymbolId => "C_IEC";
        protected override string DefaultPrefix => "C";

        protected override void CreatePorts()
        {
            Ports.Add(new NodePort() { Mode = PortMode.InOut, PinNumber = "1", Side = SidePosition.Left });
            Ports.Add(new NodePort() { Mode = PortMode.InOut, PinNumber = "2", Side = SidePosition.Right });
        }


    }
}


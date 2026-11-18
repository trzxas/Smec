using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class Resistor : Node
    {

        public override string CategoryName => nameof(Resistor);
        public override string SymbolId => "R_IEC";
        protected override string DefaultPrefix => "R";


        protected override void CreatePorts()
        {
            Ports.Add(new NodePort() { Mode = PortMode.InOut, PinNumber = "1", Side = SidePosition.Left });
            Ports.Add(new NodePort() { Mode = PortMode.InOut, PinNumber = "2", Side = SidePosition.Right });
        }

        private int _drivenPortIndex = -1; // -1: не передаёт, 0: питает Ports[0], 1: питает Ports[1]

        public override void UpdateState(TimeSpan elapsed)
        {
            // 1. Стираем сигнал, который сам резистор выдал на прошлом такте (убираем эффект защёлки)
            if (_drivenPortIndex == 0) Ports[0].State = PortState.Undefined;
            else if (_drivenPortIndex == 1) Ports[1].State = PortState.Undefined;
            _drivenPortIndex = -1;

            // 2. Заново транслируем актуальный сигнал в свободную сторону
            if (Ports[0].State != PortState.Undefined && Ports[1].State == PortState.Undefined)
            {
                Ports[1].State = Ports[0].State;
                _drivenPortIndex = 1;
            }
            else if (Ports[1].State != PortState.Undefined && Ports[0].State == PortState.Undefined)
            {
                Ports[0].State = Ports[1].State;
                _drivenPortIndex = 0;
            }
        }


    }
}

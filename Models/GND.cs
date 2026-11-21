using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class GND : Node
    {

        public override string CategoryName => nameof(GND);
        public override string SymbolId => "GND";


        protected override void CreatePorts()
        {
            Ports.Add(new NodePort
            {
                PinNumber = "1",
                Mode = PortMode.Out,
                Side = SidePosition.Top
            });
        }

        public override void Init()
        {
            // Устанавливаем начальное состояние единственного порта в Low
            Ports[0]?.State = PortState.Low;

        }

        // Всегда выдаем низкий уровень
        public override void UpdateState(TimeSpan elapsed)
        {
            Ports[0]?.State = PortState.Low;
        }


    }
}

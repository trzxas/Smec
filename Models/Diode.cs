using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class Diode : Node
    {

        public override string CategoryName => nameof(Diode);
        public override string SymbolId=> "Diode_IEC";
        protected override string DefaultPrefix => "VD";


        protected override void CreatePorts()
        {
            Ports.Add(new NodePort() { Mode = PortMode.InOut,PinNumber="1", Side = SidePosition.Top });
            Ports.Add(new NodePort() { Mode = PortMode.InOut,PinNumber="2", Side = SidePosition.Bottom });
        }

        /// <summary>
        /// Проверяет, открыт ли диод (на аноде высокий уровень, на катоде — низкий).
        /// </summary>
        protected bool IsForwardBiased() =>
            Ports.Count >= 2 && Ports[0].State == PortState.Pos && Ports[1].State == PortState.Low;

        public override void UpdateState(TimeSpan elapsed)
        {
            // Здесь может быть общая логика передачи сигнала через диод (если используется в симуляторе)
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class Diode : TwoTerminalNode
    {

        public override string CategoryName => nameof(Diode);
        public override string SymbolId=> "Diode_IEC";
        protected override string DefaultPrefix => "VD";


        protected override void CreatePorts()
        {
            Ports.Add(new NodePort { Mode = PortMode.InOut, PinNumber = "1", Side = SidePosition.Left });    // Анод
            Ports.Add(new NodePort { Mode = PortMode.InOut, PinNumber = "2", Side = SidePosition.Right }); // Катод
        }

        // 1. Когда один вывод свободен: пропускаем High с анода (0) на катод (1), или Low с катода (1) на анод (0)
        protected override PortState ComputeDirectionalOutput(int sourceIndex, PortState inputState, TimeSpan elapsed) =>
            (sourceIndex, inputState) switch
            {
                (0, PortState.High or PortState.WeakHigh) => inputState, // Прямой ток: Анод -> Катод
                (1, PortState.Low or PortState.WeakLow) => inputState,   // Обратная подтяжка: Катод -> Анод
                _ => PortState.Undefined                                 // Заперт!
            };

        // 2. Когда оба вывода подключены (например, WeakHigh на аноде и Low на катоде)
        protected override (int targetIndex, PortState state) ComputeLoadedOutput(PortState ext0, PortState ext1, TimeSpan elapsed)
        {
            if (ext0 == PortState.WeakHigh && ext1 is PortState.Low or PortState.WeakLow)
                return (0, ext1); // Катод притягивает слабо подтянутый анод к земле

            if (ext1 == PortState.WeakLow && ext0 is PortState.High or PortState.WeakHigh)
                return (1, ext0); // Анод поднимает слабо подтянутый катод

            return (-1, PortState.Undefined);
        }

    }
}


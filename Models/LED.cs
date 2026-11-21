using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public class LED : Diode
    {

        public override string CategoryName => nameof(LED);
        public override string SymbolId=> "LED_IEC";

        public override void UpdateState(TimeSpan elapsed)
        {
            base.UpdateState(elapsed);

            // Анод (Ports) подтянут к High/WeakHigh, а на катоде (Ports) есть Low/WeakLow (или диод сам проводит ток в цепь)
            bool anodeHigh = Ports[0].State is PortState.High or PortState.WeakHigh;
            bool cathodeLowOrDriven = Ports[1].State is PortState.Low or PortState.WeakLow || Ports[1].IsDriving;

            IsActive = anodeHigh && cathodeLowOrDriven;
        }

        public override void Reset()
        {
            base.Reset();
        }

    }
}

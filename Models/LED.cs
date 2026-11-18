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
            IsActive = IsForwardBiased();
        }

        public override void Reset()
        {
            base.Reset();
        }

    }
}

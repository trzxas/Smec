using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Smec.Models
{

    // --- Семейство ИЛИ (OR) ---
    public abstract class L_OR : LogicGateNode
    {
        protected override string FamilyPrefix => "OR";

        public override void UpdateState(TimeSpan elapsed) => SetOutputs(InputPorts.Any(IsLogicHigh));
    }

    public class L2OR : L_OR { protected override int InputCount => 2; }
    public class L3OR : L_OR { protected override int InputCount => 3; }
    public class L4OR : L_OR { protected override int InputCount => 4; }
    public class L8OR : L_OR { protected override int InputCount => 8; }

    // --- Семейство И (AND) ---
    public abstract class L_AND : LogicGateNode
    {
        protected override string FamilyPrefix => "AND";

        public override void UpdateState(TimeSpan elapsed) => SetOutputs(InputPorts.All(IsLogicHigh));
    }

    public class L2AND : L_AND { protected override int InputCount => 2; }
    public class L3AND : L_AND { protected override int InputCount => 3; }
    public class L4AND : L_AND { protected override int InputCount => 4; }
    public class L8AND : L_AND { protected override int InputCount => 8; }

    // --- Семейство Исключающее ИЛИ (XOR) ---
    public abstract class L_XOR : LogicGateNode
    {
        protected override string FamilyPrefix => "XOR";

        public override void UpdateState(TimeSpan elapsed) => SetOutputs(InputPorts.Count(IsLogicHigh) % 2 == 1);
    }

    public class L2XOR : L_XOR { protected override int InputCount => 2; }
    public class L3XOR : L_XOR { protected override int InputCount => 3; }
    public class L4XOR : L_XOR { protected override int InputCount => 4; }
    public class L8XOR : L_XOR { protected override int InputCount => 8; }

}

namespace Smec.Models
{
    public class ElectricalNet
    {
        public List<NodePort> Ports { get; } = [];
        public List<Link> Links { get; } = [];

        public void Evaluate()
        {
            bool hasStrongHigh = false, hasStrongLow = false;
            bool hasWeakHigh = false, hasWeakLow = false;

            // 1. Читаем ТОЛЬКО активные источники сигнала в этой линии
            foreach (var p in Ports)
            {
                bool isSource = p.Mode == PortMode.Out || (p.Mode == PortMode.InOut && p.IsDriving);
                if (!isSource) continue;

                switch (p.State)
                {
                    case PortState.High: hasStrongHigh = true; break;
                    case PortState.Low: hasStrongLow = true; break;
                    case PortState.WeakHigh: hasWeakHigh = true; break;
                    case PortState.WeakLow: hasWeakLow = true; break;
                }
            }

            // 2. Разрешаем приоритет уровней и проверяем КЗ
            bool conflict = hasStrongHigh && hasStrongLow;
            PortState netState = PortState.Undefined;

            if (!conflict)
            {
                if (hasStrongHigh) netState = PortState.High;
                else if (hasStrongLow) netState = PortState.Low;
                else if (hasWeakHigh && hasWeakLow) netState = PortState.Undefined;
                else if (hasWeakHigh) netState = PortState.WeakHigh;
                else if (hasWeakLow) netState = PortState.WeakLow;
            }

            // 3. Раздаём вычисленный потенциал на все принимающие порты и все провода этой линии
            if (!conflict)
            {
                foreach (var p in Ports)
                {
                    bool isSource = p.Mode == PortMode.Out || (p.Mode == PortMode.InOut && p.IsDriving);
                    if (!isSource && p.State != netState)
                        p.State = netState;
                }
            }

            ObjectStatus status = conflict ? ObjectStatus.Error : ObjectStatus.Normal;
            foreach (var link in Links)
            {
                link.SetSimulatedState(netState, status);
            }
        }
    }
}


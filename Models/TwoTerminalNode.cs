namespace Smec.Models
{
    public abstract class TwoTerminalNode : Node
    {
        private int _drivenPortIndex = -1;
        private PortState _lastDrivenState = PortState.Undefined;

        protected override void CreatePorts()
        {
            Ports.Add(new NodePort { Mode = PortMode.InOut, PinNumber = "1", Side = SidePosition.Left });
            Ports.Add(new NodePort { Mode = PortMode.InOut, PinNumber = "2", Side = SidePosition.Right });
        }

        public override void UpdateState(TimeSpan elapsed)
        {
            base.UpdateState(elapsed);

            var p0 = Ports.First();
            var p1 = Ports.Last();

            // Внешние состояния на выводах 1 и 2 (игнорируем собственный выход с прошлого такта)
            PortState ext0 = (_drivenPortIndex == 0 && p0.State == _lastDrivenState) ? PortState.Undefined : p0.State;
            PortState ext1 = (_drivenPortIndex == 1 && p1.State == _lastDrivenState) ? PortState.Undefined : p1.State;

            bool isPort0Free = ext0 == PortState.Undefined;
            bool isPort1Free = ext1 == PortState.Undefined;

            int sourceIndex = -1;
            if (!isPort0Free && isPort1Free) sourceIndex = 0;
            else if (!isPort1Free && isPort0Free) sourceIndex = 1;

            int nextDrivenIndex = -1;
            PortState nextDrivenState = PortState.Undefined;

            if (sourceIndex != -1)
            {
                PortState input = sourceIndex == 0 ? ext0 : ext1;
                PortState output = ComputeDirectionalOutput(sourceIndex, input, elapsed);
                if (output != PortState.Undefined)
                {
                    nextDrivenIndex = 1 - sourceIndex;
                    nextDrivenState = output;
                }
            }
            else
            {
                (nextDrivenIndex, nextDrivenState) = ComputeLoadedOutput(ext0, ext1, elapsed);
                if (nextDrivenIndex == -1)
                    OnInputLost();
            }

            // Сбрасываем прошлый выходной пин, если направление изменилось или элемент перестал выдавать сигнал
            if (_drivenPortIndex != -1 && _drivenPortIndex != nextDrivenIndex && Ports[_drivenPortIndex].State == _lastDrivenState)
            {
                Ports[_drivenPortIndex].State = PortState.Undefined;
            }

            // Помечаем для ElectricalNet, какой из двух портов прямо сейчас является источником сигнала
            p0.IsDriving = (nextDrivenIndex == 0);
            p1.IsDriving = (nextDrivenIndex == 1);

            if (nextDrivenIndex != -1)
            {
                Ports[nextDrivenIndex].State = nextDrivenState;
            }

            _drivenPortIndex = nextDrivenIndex;
            _lastDrivenState = nextDrivenState;
        }

        public override void Reset()
        {
            base.Reset();
            _drivenPortIndex = -1;
            _lastDrivenState = PortState.Undefined;
            OnInputLost();
        }

        protected virtual PortState ComputeOutputState(PortState inputState, TimeSpan elapsed) => PortState.Undefined;

        protected virtual PortState ComputeDirectionalOutput(int sourceIndex, PortState inputState, TimeSpan elapsed) =>
            ComputeOutputState(inputState, elapsed);

        protected virtual (int targetIndex, PortState state) ComputeLoadedOutput(PortState ext0, PortState ext1, TimeSpan elapsed) =>
            (-1, PortState.Undefined);

        protected virtual void OnInputLost() { }
    }
}

using System.Runtime.Serialization;

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

        /// <summary>
        /// Длительность импульса перезаряда в миллисекундах (время, в течение которого конденсатор пропускает перепад уровня).
        /// </summary>
        [DataMember]
        public double PulseDurationMs { get; set; } = 150;

        private PortState _lastInputState = PortState.Undefined;
        private TimeSpan _pulseEndTime = TimeSpan.Zero;
        private int _drivenPortIndex = -1;

        public override void UpdateState(TimeSpan elapsed)
        {
            base.UpdateState(elapsed);

            // 1. Стираем собственное воздействие с прошлого такта
            if (_drivenPortIndex == 0) Ports[0].State = PortState.Undefined;
            else if (_drivenPortIndex == 1) Ports[1].State = PortState.Undefined;
            _drivenPortIndex = -1;

            // 2. Определяем, с какой стороны подаётся активный входной сигнал
            int sourceIndex = -1;
            if (Ports[0].State is PortState.Pos or PortState.Low && Ports[1].State == PortState.Undefined)
                sourceIndex = 0;
            else if (Ports[1].State is PortState.Pos or PortState.Low && Ports[0].State == PortState.Undefined)
                sourceIndex = 1;

            if (sourceIndex == -1)
            {
                _lastInputState = PortState.Undefined;
                return;
            }

            int targetIndex = 1 - sourceIndex;
            PortState currentInput = Ports[sourceIndex].State;

            // 3. Если на входе произошёл перепад (смена уровня) — запускаем импульс перезаряда
            if (currentInput != _lastInputState && _lastInputState != PortState.Undefined)
            {
                _pulseEndTime = elapsed + TimeSpan.FromMilliseconds(PulseDurationMs);
            }
            _lastInputState = currentInput;

            // 4. Пока конденсатор перезаряжается — пропускаем импульс на выход; после заряда — блокируем постоянный ток
            if (elapsed <= _pulseEndTime)
            {
                Ports[targetIndex].State = currentInput;
                _drivenPortIndex = targetIndex;
            }
        }
    }
}


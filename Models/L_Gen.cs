using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace Smec.Models
{
    public class L_Gen : Node
    {
        public override string SymbolId => "Sourcer";
        protected override string DefaultPrefix => "G";

        private TimeSpan _period = TimeSpan.FromMilliseconds(100);

        public TimeSpan Period
        {
            get => _period;
            set
            {
                if (_period == value) return;
                _period = value;
                UpdateFrequencyText();
                OnPropertyChanged(nameof(Period));
            }
        }

        private void UpdateFrequencyText()
        {
            if (_period.TotalSeconds <= 0)
            {
                Value = "0 Гц";
                return;
            }

            double freqHz = 1.0 / _period.TotalSeconds;

            Value = freqHz switch
            {
                >= 1_000_000 => $"{freqHz / 1_000_000:0.##} МГц",
                >= 1_000 => $"{freqHz / 1_000:0.##} кГц",
                _ => $"{freqHz:0.##} Гц"
            };

        }

        TimeSpan _lastUpdated = TimeSpan.Zero;
        public L_Gen()
        {
            Description = "Генератор логических сигналов положительной полярности";
            CategoryName = "Sourcer";
            UpdateFrequencyText();
        }

        protected override void CreatePorts()
        {
            Ports.Add(new NodePort
            {
                PinNumber = "1",
                Mode = PortMode.Out,
                Side = SidePosition.Right
            });
        }

        protected override void CopyPropertiesTo(Node target)
        {
            ((L_Gen)target).Period = this.Period;
            base.CopyPropertiesTo(target);
        }

        public override void Init()
        {
            _lastUpdated = TimeSpan.Zero;
            Ports[0].State = PortState.Low;
            base.Init();
        }

        public override void UpdateState(TimeSpan elapsed)
        {
            if (elapsed - _lastUpdated >= Period)
            {
                var outPort = Ports[0];
                outPort.State = outPort.State == PortState.Low ? PortState.High : PortState.Low;
                _lastUpdated = elapsed;
            }
            
        }

    }
}

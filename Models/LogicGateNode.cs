using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Models
{
    public abstract class LogicGateNode : Node
    {
        protected abstract string FamilyPrefix { get; } // "OR", "AND", "XOR"
        protected virtual int InputCount => 2;

        // Автоматически формирует "OR2", "AND3", "XOR4" для любого наследника!
        public override string SymbolId => $"{FamilyPrefix}{InputCount}";
        public override string CategoryName => $"{FamilyPrefix}{InputCount}";

        protected override string DefaultPrefix => "D";

        // Единое создание портов с нумерацией PinNumber для всех логических элементов
        protected override void CreatePorts()
        {
            int pinNum = 1;

            for (int i = 0; i < InputCount; i++)
            {
                Ports.Add(new NodePort
                {
                    PinNumber = (pinNum++).ToString(),
                    Mode = PortMode.In,
                });
            }

            Ports.Add(new NodePort
            {
                PinNumber = (pinNum++).ToString(),
                Mode = PortMode.Out,
                
            });

            Ports.Add(new NodePort
            {
                PinNumber = (pinNum++).ToString(),
                Mode = PortMode.Out,
                Decorator = PortDecorator.Circle
            });
        }

        /// <summary>
        /// Устанавливает логические уровни на прямом (out[0]) и инвертированном (out[1]) выходах.
        /// </summary>
        protected void SetOutputs(bool result)
        {
            PortState directState = result ? PortState.Pos : PortState.Low;
            PortState invertedState = result ? PortState.Low : PortState.Pos;

            int outIndex = 0;
            foreach (var port in Ports)
            {
                if (port.Mode != PortMode.Out)
                    continue;

                if (outIndex == 0)
                    port.State = directState;
                else if (outIndex == 1)
                {
                    port.State = invertedState;
                    break; // Оба выхода обновлены, дальше цикл не нужен
                }

                outIndex++;
            }
        }
    }

}

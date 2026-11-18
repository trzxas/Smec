namespace Smec.ViewModels
{


    //  ======Общее правило для всех будущих символов в SymbolLibrary:==========
    //  Чтобы абсолютно все компоненты(резисторы, конденсаторы, генераторы, логика) стыковались прямыми линиями без сдвига в полклетки,
    //  придерживайтесь единого правила:
    //• Границы корпусов только по целым координатам(-1, 0, 1, 2).
    //• Все горизонтальные выводы(Pins)  на полуцелых координатах Y(-0.5, +0.5, +1.5).

    public class SymbolLibrary
    {
        private readonly Dictionary<string, SymbolDefinition> _symbols = [];

        public SymbolLibrary()
        {
            RegisterBuiltInSymbols();
        }

        public SymbolDefinition GetSymbol(string symbolId)
        {
            if (_symbols.TryGetValue(symbolId, out var def))
                return def;

            // Заглушка (квадратик со знаком вопроса), если в файле встретился неизвестный символ
            return new SymbolDefinition
            {
                SymbolId = symbolId,
                BodyPath = "M -1,-1 L 1,-1 L 1,1 L -1,1 Z",
                FunctionText = "?",
            };
        }

        public IEnumerable<SymbolDefinition> GetAll() => _symbols.Values;

        private void RegisterBuiltInSymbols()
        {
            // 1. РЕЗИСТОР (прямоугольник 2.0 x 0.8 + два вывода длиной 1.0)
            Add(new SymbolDefinition
            {
                SymbolId = "R_IEC",
                DisplayName = "Резистор",
                BodyPath = "M -1,-0.4 L 1,-0.4 L 1,0.4 L -1,0.4 Z",
                Width = 4.0,
                Height = 1.0,
                Pins =
                [
                    new PinDefinition { Number = "1", X = -2, Y = 0, Orientation = 0, Length = 1.0 },
                    new PinDefinition { Number = "2", X =  2, Y = 0, Orientation = 180, Length = 1.0 }
                ]
            });

            // 2. КОНДЕНСАТОР (две обкладки с зазором 0.3 + два вывода)
            Add(new SymbolDefinition
            {
                SymbolId = "C_IEC",
                DisplayName = "Конденсатор",
                BodyPath = "M -0.15,-0.8 L -0.15,0.8 M 0.15,-0.8 L 0.15,0.8",
                Width = 2.0,
                Height = 1.6,
                Pins =
                [
                    new PinDefinition { Number = "1", X = -1, Y = 0, Orientation = 0, Length = 0.85 },
                    new PinDefinition { Number = "2", X =  1, Y = 0, Orientation = 180, Length = 0.85 }
                ]
            });

            // 3. Индуктивность
            Add(new SymbolDefinition
            {
                SymbolId = "L_IEC",
                DisplayName = "Катушка индуктивности",
                IsFilled = false, // Обязательно false — это открытая дугообразная линия, её нельзя заливать белым!

                // 4 полуокружности (радиус 0.25 клетки каждая) от X = -1 до X = +1 на оси Y = 0
                BodyPath = "M -1,0 A 0.25,0.25 0 1 1 -0.5,0 A 0.25,0.25 0 1 1 0,0 A 0.25,0.25 0 1 1 0.5,0 A 0.25,0.25 0 1 1 1,0",

                Pins =
                        [
                            // Левый вывод (горячий конец в точке -1.5, 0)
                            new PinDefinition
                            {
                                Number = "1",
                                Name = "",
                                X = -1.5,
                                Y = 0,
                                Length = 0.5,
                                Orientation = 0 // Смотрит вправо к началу первой дуги (-1.0, 0)
                            },
                            // Правый вывод (горячий конец в точке +1.5, 0)
                            new PinDefinition
                            {
                                Number = "2",
                                Name = "",
                                X = 1.5,
                                Y = 0,
                                Length = 0.5,
                                Orientation = 180 // Смотрит влево к концу четвёртой дуги (+1.0, 0)
                            }
                        ]
            });


            // ЗЕМЛЯ / GND (1 вывод сверху)
            Add(new SymbolDefinition
            {
                SymbolId = "GND",
                DisplayName = "GND",
                IsFilled = false,
                BodyPath = "M -0.6,0 L 0.6,0 M -0.4,0.25 L 0.4,0.25 M -0.2,0.5 L 0.2,0.5",
                Pins =
                        [
                            new() {
                                Number = "1",
                                Name = "",
                                X = 0,
                                Y = -0.5,
                                Length = 0.5,
                                Orientation = 90
                            }
                        ]
            });

            // 4. Sourcer (Источник логического сигнала / генератор: корпус с выводом справа)
            Add(new SymbolDefinition
            {
                SymbolId = "Sourcer",
                DisplayName = "Sourcer",
                IsFilled = true,
                // Корпус 1.5 x 1 клетка по целым линиям сетки (X от -1 до 0.5, Y от -1 до 0)
                BodyPath = "M -1,-1 L 0.5,-1 L 0.5,0 L -1,0 Z",
                FunctionText = "G",
                Pins =
                        [
                            new ()
                            {
                                Number = "1",
                                Name = "",
                                X = 1.0,
                                Y = -0.5,       // Та же полуцелая координата Y, что и у входов D1!
                                Length = 0.5,
                                Orientation = 180
                            }
                        ]
            });

            // Регистрируем целые семейства элементов одной строкой!
            for (int i = 2; i <= 4; i++)
            {
                RegisterLogicGateFamily("OR", "≥1", countInputs: i);
                RegisterLogicGateFamily("AND", "&", countInputs: i);
                RegisterLogicGateFamily("XOR", "=1", countInputs: i);
            }
            RegisterLogicGateFamily("OR", "≥1", countInputs: 8);
            RegisterLogicGateFamily("AND", "&", countInputs: 8);
            RegisterLogicGateFamily("XOR", "=1", countInputs: 8);

            RegisterSemiconductors();
        }

        public void Add(SymbolDefinition def) => _symbols[def.SymbolId] = def;

        /// <summary>
        /// Универсальная процедура регистрации семейства логических элементов (2, 3, 4 входа + прямой и инверсный выходы).
        /// </summary>
        private void RegisterLogicGateFamily(string familyPrefix, string functionSign, int countInputs)
        {

            var pins = new List<PinDefinition>();
            int pinNum = 1;

            // Верхняя и нижняя границы корпуса всегда на ЦЕЛЫХ линиях сетки!
            int topY = -(countInputs / 2);
            int bottomY = topY + countInputs; // Высота корпуса = ровно inputs целых клеток!

            // Если нужно центрирование по вертикали для входных пинов слева (шаг 1 клетка)
            // не используем!
            //double startY = -(countInputs - 1) / 2.0;

            // 1. Входные пины слева идут прижатые к верху (Y = topY + 0.5, topY + 1.5, ...)
            for (int i = 0; i < countInputs; i++)
            {
                pins.Add(new PinDefinition
                {
                    Number = (pinNum++).ToString(),
                    Name = "",
                    X = -1.5,
                    Y = topY + i + 0.5,
                    Length = 0.5,
                    Orientation = 0
                });
            }

            // 2. Вычисляем индекс среднего входа (для 2 -> 0, для 3 -> 1, для 4 -> 1, для 8 -> 3)
            // Центрирование по вертикали, если нужно прижать к верху, нужно просто установить midIndex =0
            int midIndex = (countInputs - 1) / 2;

            // 2. Прямой выход справа сверху
            pins.Add(new PinDefinition
            {
                Number = (pinNum++).ToString(),
                Name = "",
                X = 1.5,
                Y = topY + midIndex + 0.5,
                Length = 0.5,
                Orientation = 180
            });

            // 3. Инверсный выход справа снизу
            pins.Add(new PinDefinition
            {
                Number = (pinNum++).ToString(),
                Name = "",
                X = 1.5,
                Y = topY + midIndex + 1.5,
                Length = 0.5,
                Orientation = 180
            });

            // Высота прямоугольника подстраивается под число входов
            double halfH = Math.Max(1.0, countInputs * 0.5);

            Add(new SymbolDefinition
            {
                SymbolId = $"{familyPrefix}{countInputs}",  // "OR2", "AND3", "XOR4"...
                DisplayName = $"{familyPrefix}{countInputs}",
                FunctionText = functionSign,                // "≥1", "&", "=1" 
                BodyPath = FormattableString.Invariant($"M -1,{topY} L 1,{topY} L 1,{bottomY} L -1,{bottomY} Z"), // корректируем . и , в разделителе дробной части halfH
                Pins = pins
            });

        }

        private void RegisterSemiconductors()
        {
            // 1. Диод (IEC 60617-05)
            Add(new SymbolDefinition
            {
                SymbolId = "Diode_IEC",
                DisplayName = "Диод",
                IsFilled = false, // Открытый контур с линией катода и сквозной осью
                                  // Треугольник анода (-0.5..0.5), вертикальная черта катода (0.5) и сквозная линия (-0.5..0.5)
                BodyPath = "M -0.5,-0.5 L 0.5,0 L -0.5,0.5 Z M 0.5,-0.5 L 0.5,0.5 M -0.5,0 L 0.5,0",
                Pins =
                        [
                            new PinDefinition { Number = "1", Name = "A", X = -1.5, Y = 0, Length = 1.0, Orientation = 0 },
                            new PinDefinition { Number = "2", Name = "K", X = 1.5,  Y = 0, Length = 1.0, Orientation = 180 }
                        ]
            });

            // 2. Светодиод LED (IEC 60617-05: диод + две стрелки излучения вверх-вправо)
            Add(new SymbolDefinition
            {
                SymbolId = "LED_IEC",
                DisplayName = "Светодиод",
                IsFilled = false,
                BodyPath = "M -0.5,-0.5 L 0.5,0 L -0.5,0.5 Z M 0.5,-0.5 L 0.5,0.5 M -0.5,0 L 0.5,0 " +
                           // Первая стрелка излучения
                           "M 0.1,-0.6 L 0.5,-1.0 M 0.3,-1.0 L 0.5,-1.0 L 0.5,-0.8 " +
                           // Вторая стрелка излучения
                           "M 0.45,-0.45 L 0.85,-0.85 M 0.65,-0.85 L 0.85,-0.85 L 0.85,-0.65",
                Pins =
                        [
                            new PinDefinition { Number = "1", Name = "A", X = -1.5, Y = 0, Length = 1.0, Orientation = 0 },
                            new PinDefinition { Number = "2", Name = "K", X = 1.5,  Y = 0, Length = 1.0, Orientation = 180 }
                        ]
            });

            // 3. Биполярный транзистор NPN (IEC 60617-05)
            Add(new SymbolDefinition
            {
                SymbolId = "NPN_IEC",
                DisplayName = "Транзистор NPN",
                IsFilled = false,
                // Окружность корпуса (радиус 1.0, центр 0.2, 0), вертикальная база (-0.2), линии коллектора и эмиттера + стрелка наружу
                BodyPath = "M -0.8,0 A 1.0,1.0 0 1 1 1.2,0 A 1.0,1.0 0 1 1 -0.8,0 " + // Окружность корпуса
                           "M -0.2,-0.55 L -0.2,0.55 " +                                // Вертикальный электрод базы
                           "M -0.2,-0.25 L 0.5,-0.85 L 0.5,-1.0 " +                     // Вывод коллектора до границы
                           "M -0.2,0.25 L 0.5,0.85 L 0.5,1.0 " +                        // Вывод эмиттера до границы
                           "M 0.22,0.75 L 0.5,0.85 L 0.42,0.55",                        // Стрелка NPN (от базы к эмиттеру)
                Pins =
                         [
                             new PinDefinition { Number = "1", Name = "B", X = -1.5, Y = 0,    Length = 1.3, Orientation = 0 },   // База слева
                             new PinDefinition { Number = "2", Name = "C", X = 0.5,  Y = -1.5, Length = 0.5, Orientation = 90 },  // Коллектор сверху
                             new PinDefinition { Number = "3", Name = "E", X = 0.5,  Y = 1.5,  Length = 0.5, Orientation = 270 }  // Эмиттер снизу
                         ]
            });

            // 4. Биполярный транзистор PNP (IEC 60617-05)
            Add(new SymbolDefinition
            {
                SymbolId = "PNP_IEC",
                DisplayName = "Транзистор PNP",
                IsFilled = false,
                BodyPath = "M -0.8,0 A 1.0,1.0 0 1 1 1.2,0 A 1.0,1.0 0 1 1 -0.8,0 " +
                           "M -0.2,-0.55 L -0.2,0.55 " +
                           "M -0.2,-0.25 L 0.5,-0.85 L 0.5,-1.0 " +
                           "M -0.2,0.25 L 0.5,0.85 L 0.5,1.0 " +
                           "M 0.15,0.42 L -0.05,0.38 L 0.03,0.65",                     // Стрелка PNP (от эмиттера к базе)
                Pins =
                        [
                            new PinDefinition { Number = "1", Name = "B", X = -1.5, Y = 0,    Length = 1.3, Orientation = 0 },
                            new PinDefinition { Number = "2", Name = "C", X = 0.5,  Y = -1.5, Length = 0.5, Orientation = 90 },
                            new PinDefinition { Number = "3", Name = "E", X = 0.5,  Y = 1.5,  Length = 0.5, Orientation = 270 }
                        ]
            });
        }


    }
}




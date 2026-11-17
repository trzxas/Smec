using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.ViewModels
{
    
    public class SymbolDefinition
    {
        public string SymbolId { get; set; } = string.Empty;   // Уникальный ключ: "R_IEC", "C_IEC", "OR_2"
        public string DisplayName { get; set; } = string.Empty; // Название для панели элементов: "Резистор"
        public string FunctionText { get; set; } = string.Empty; // Текст функции: "≥1", "&", "=1"
        public bool IsPlaceholder { get; set; } = false;

        public bool IsFilled { get; set; } = true; // внутренняя заливка элемента

        /// <summary>
        /// Векторный контур корпуса в единицах сетки (1.0 = 1 клетка), формат SVG Path.
        /// Например, прямоугольник 2x0.8 вокруг (0,0): "M -1,-0.4 L 1,-0.4 L 1,0.4 L -1,0.4 Z"
        /// </summary>
        public string BodyPath { get; set; } = string.Empty;

        /// <summary>
        /// Габариты зоны захвата мышкой (HitBox) в единицах сетки.
        /// </summary>
        public double Width { get; set; } = 2.0;
        public double Height { get; set; } = 1.0;

        /// <summary>
        /// Список выводов (пинов) элемента в локальных координатах от (0, 0).
        /// </summary>
        public List<PinDefinition> Pins { get; set; } = [];
    }

    public class PinDefinition
    {
        public string Number { get; set; } = string.Empty; // "1", "2"
        public string Name { get; set; } = string.Empty;   // "A", "B", "Y"
        public double X { get; set; }                      // Точка подключения провода (в клетках сетки)
        public double Y { get; set; }
        public int Orientation { get; set; }               // Куда смотрит линия к корпусу: 0=вправо, 90=вниз, 180=влево, 270=вверх
        public double Length { get; set; } = 1.0;          // Длина вывода в клетках сетки
    }
}

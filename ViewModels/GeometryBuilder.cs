using System.Windows.Media;

namespace Smec.ViewModels
{
    public static class GeometryBuilder
    {
        public static Geometry Build(SymbolDefinition def, double gridSize)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.BodyPath))
                return Geometry.Empty;

            // 1. Парсим SVG-путь из SymbolDefinition (где координаты указаны в клетках сетки)
            var baseGeometry = Geometry.Parse(def.BodyPath);

            // 2. Клонируем, так как результат Geometry.Parse в WPF по умолчанию заморожен (IsFrozen = true)
            var scaledGeometry = baseGeometry.Clone();

            // 3. Масштабируем из единиц сетки в экранные пиксели
            scaledGeometry.Transform = new ScaleTransform(gridSize, gridSize);

            // 4. Замораживаем для максимальной скорости отрисовки и потокобезопасности в WPF
            scaledGeometry.Freeze();

            return scaledGeometry;
        }
    }
}

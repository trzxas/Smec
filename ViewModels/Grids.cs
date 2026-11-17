using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace Smec.ViewModels
{
    public enum GridStyleMode
    {
        Lines,  // Линии
        Dots,   // Точки
        None    // Без сетки
    }

    public enum GridPreset
    {
        Grid24_12, // Крупная 24px, мелкая 12px
        Grid48_12, // Крупная 48px, мелкая 12px
        Grid50_10  // Крупная 50px, мелкая 10px
    }

    public partial class SysViewModel
    {
        // Настройки пользователя
        public SolidColorBrush CurrentGridBrush { get; set { SetProperty(ref field, value); } } = new(Color.FromRgb(0xe0, 0xe0, 0xe0));
        public SolidColorBrush CurrentGridBackground { get; set { SetProperty(ref field, value); } } = new(Color.FromRgb(255, 255, 240));

        public GridStyleMode CurrentGridStyle
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                    UpdateGridBrush();
                }
            }
        } = GridStyleMode.Lines;


        public GridPreset CurrentGridPreset
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                    UpdateGridBrush();
                }
            }
        } = GridPreset.Grid48_12;


        public Brush WorkspaceGridBrush
        {
            get;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        } = Brushes.White;

        // Вызывам этот метод в конструкторе SysViewModel, а также при смене цветов CurrentGridBackground / CurrentGridBrush
        public void UpdateGridBrush()
        {
            // 1. Определяем размер крупной ячейки и шаг примагничивания (GridSize)
            (double majorSize, double minorSize) = CurrentGridPreset switch
            {
                GridPreset.Grid24_12 => (24.0, 12.0),
                GridPreset.Grid48_12 => (48.0, 12.0),
                GridPreset.Grid50_10 => (50.0, 10.0),
                _ => (24.0, 12.0)
            };

            // TODO: привязать шаг прилипания к текущей сетке
            // Синхронизируем шаг прилипания узлов и Ctrl+Стрелок
            // GridSize = minorSize;

            // Если выбрано "Без сетки" — просто возвращаем сплошной цвет фона
            if (CurrentGridStyle == GridStyleMode.None)
            {
                WorkspaceGridBrush = CurrentGridBackground;
                return;
            }

            // Формирование кисти
            var drawingGroup = new DrawingGroup();

            // Слой 1: Фон
            drawingGroup.Children.Add(new GeometryDrawing(
                CurrentGridBackground,
                null,
                new RectangleGeometry(new Rect(0, 0, majorSize, majorSize))));

            // Слой 2: Линии или Точки
            if (CurrentGridStyle == GridStyleMode.Lines)
            {
                // Мелкие линии (шаг minorStep)
                var minorGroup = new GeometryGroup();
                for (double pos = minorSize; pos < majorSize; pos += minorSize)
                {
                    minorGroup.Children.Add(new LineGeometry(new Point(pos, 0), new Point(pos, majorSize)));
                    minorGroup.Children.Add(new LineGeometry(new Point(0, pos), new Point(majorSize, pos)));
                }
                var minorPen = new Pen(CurrentGridBrush, 0.2);
                minorPen.Freeze();
                drawingGroup.Children.Add(new GeometryDrawing(null, minorPen, minorGroup));

                // Крупные опорные линии (границы ячейки majorSize)
                var majorGroup = new GeometryGroup();
                majorGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(majorSize, 0)));
                majorGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, majorSize)));
                var majorPen = new Pen(CurrentGridBrush, 0.6);
                majorPen.Freeze();
                drawingGroup.Children.Add(new GeometryDrawing(null, majorPen, majorGroup));
            }
            else if (CurrentGridStyle == GridStyleMode.Dots)
            {
                // Точки в узлах мелкой сетки (шаг minorSize)
                var dotsGroup = new GeometryGroup();
                for (double x = 0; x < majorSize; x += minorSize)
                {
                    for (double y = 0; y < majorSize; y += minorSize)
                    {
                        // На пересечении крупных линий ставим точку чуть крупнее (0.8), остальные — 0.45
                        double radius = (x == 0 && y == 0) ? 0.8 : 0.45;
                        dotsGroup.Children.Add(new EllipseGeometry(new Point(x, y), radius, radius));
                    }
                }
                drawingGroup.Children.Add(new GeometryDrawing(CurrentGridBrush, null, dotsGroup));
            }

            var brush = new DrawingBrush(drawingGroup)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, majorSize, majorSize),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };
            brush.Freeze(); // Замораживаем кисть для максимальной производительности рендеринга

            WorkspaceGridBrush = brush;
        }

        private void SetGridTheme(object? theme)
        {
            switch (theme?.ToString())
            {
                case "Light":
                    CurrentGridBackground = new(Color.FromRgb(255, 255, 240));
                    CurrentGridBrush = new(Color.FromRgb(0xe0, 0xe0, 0xe0));
                    break;
                case "Blueprint": // Чертежная синяя
                    CurrentGridBackground = new SolidColorBrush(Color.FromRgb(250, 250, 255));
                    CurrentGridBrush = new SolidColorBrush(Color.FromRgb(160, 200, 250));
                    break;
                case "Dark": // Темная тема
                    CurrentGridBackground = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                    CurrentGridBrush = new SolidColorBrush(Color.FromRgb(90, 90, 90));
                    break;
            }
            UpdateGridBrush();
        }

    }
}

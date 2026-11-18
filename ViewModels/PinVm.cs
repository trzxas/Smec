using Smec.Models;
using System.ComponentModel;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Smec.ViewModels
{
    public class PinVm : ObservableObject, IDisposable
    {
        public NodePort? PortModel { get; }
        public PinDefinition Definition { get; }

        // Координаты линии вывода в пикселях:
        // (X1, Y1) — точка входа в корпус детали
        // (X2, Y2) — "горячий" конец пина (совпадает с StrokeEndLineCap="Round")
        public double X1 { get; private set; }
        public double Y1 { get; private set; }
        public double X2 { get; private set; }
        public double Y2 { get; private set; }

        // Координаты имени пина внутри корпуса (например, "A", "B", "Y")
        public double NameX { get; private set; }
        public double NameY { get; private set; }
        public string Name => Definition.Name;
        public bool HasName => !string.IsNullOrWhiteSpace(Definition.Name) && Definition.Name != "~";

        // 1. Исправленный State: берёт актуальное значение из PortModel (если привязан)
        private PortState _state = PortState.Undefined;
        public PortState State
        {
            get => PortModel?.State ?? _state;
            set => SetProperty(ref _state, value);
        }

        // 2. Исправленный HighlightStatus: по умолчанию null, чтобы не срабатывал триггер Value="Normal"
        private ObjectStatus? _highlightStatus = null;
        public ObjectStatus? HighlightStatus
        {
            get => _highlightStatus;
            set => SetProperty(ref _highlightStatus, value);
        }

        public PortDecorator Decorator => PortModel?.Decorator ?? PortDecorator.None;

        // Универсальная векторная фигура декоратора (Circle, Slash, BackSlash, Arrow)
        public Geometry? DecoratorGeometry { get; private set; }

        public PinVm(PinDefinition definition, double gridSize, NodePort? portModel = null)
        {
            Definition = definition;
            PortModel = portModel;

            if (PortModel is INotifyPropertyChanged notifyPort)
                notifyPort.PropertyChanged += OnPortModelPropertyChanged;

            UpdateGeometry(gridSize);
        }

        private void OnPortModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodePort.State))
                OnPropertyChanged(nameof(State));
        }

        public void UpdateGeometry(double gridSize)
        {
            double rad = Definition.Orientation * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            // (X2, Y2) — горячий конец пина (точка подключения провода)
            X2 = Definition.X * gridSize;
            Y2 = Definition.Y * gridSize;

            // (X1, Y1) — точка входа пина в корпус детали
            X1 = (Definition.X + cos * Definition.Length) * gridSize;
            Y1 = (Definition.Y + sin * Definition.Length) * gridSize;

            // 3. Позиция имени пина считается от X1, Y1 (внутрь корпуса!)
            double margin = 0.25 * gridSize;
            NameX = X1 + cos * margin;
            NameY = Y1 + sin * margin - 6;

            // Построение геометрии декоратора у границы корпуса (X1, Y1)
            DecoratorGeometry = BuildDecoratorGeometry(gridSize, cos, sin);

            OnPropertyChanged(nameof(X1));
            OnPropertyChanged(nameof(Y1));
            OnPropertyChanged(nameof(X2));
            OnPropertyChanged(nameof(Y2));
            OnPropertyChanged(nameof(NameX));
            OnPropertyChanged(nameof(NameY));
            OnPropertyChanged(nameof(DecoratorGeometry));

           
        }

        private Geometry? BuildDecoratorGeometry(double gridSize, double cos, double sin)
        {
            if (Decorator == PortDecorator.None)
                return null;

            // Нормаль (перпендикуляр к направлению пина)
            double nx = -sin;
            double ny = cos;
            double r = gridSize * 0.16; // Радиус кружка / полуразмер штриха

            // Центр декоратора сдвинут от стенки корпуса (X1, Y1) на радиус r вдоль линии пина
            double cx = X1/* - cos * r*/;
            double cy = Y1/* - sin * r*/;

            switch (Decorator)
            {
                case PortDecorator.Circle:
                    var ellipse = new EllipseGeometry(new Point(cx, cy), r, r);
                    ellipse.Freeze();
                    return ellipse;

                case PortDecorator.Slash:
                    {
                        var g = new LineGeometry(
                            new Point(cx - cos * r - nx * r, cy - sin * r - ny * r),
                            new Point(cx + cos * r + nx * r, cy + sin * r + ny * r));
                        g.Freeze();
                        return g;
                    }

                case PortDecorator.BackSlash:
                    {
                        var g = new LineGeometry(
                            new Point(cx - cos * r + nx * r, cy - sin * r + ny * r),
                            new Point(cx + cos * r - nx * r, cy + sin * r - ny * r));
                        g.Freeze();
                        return g;
                    }

                case PortDecorator.Arrow:
                    {
                        // Стрелка входа/выхода (например, для тактового входа CLK)
                        var fig = new PathFigure { StartPoint = new Point(X1 - nx * r, Y1 - ny * r), IsClosed = false };
                        fig.Segments.Add(new LineSegment(new Point(X1 + cos * (r * 1.4), Y1 + sin * (r * 1.4)), true));
                        fig.Segments.Add(new LineSegment(new Point(X1 + nx * r, Y1 + ny * r), true));
                        var pg = new PathGeometry([fig]);
                        pg.Freeze();
                        return pg;
                    }

                default:
                    return null;
            }
        }



        // 4. Отписка от событий модели порта при удалении узла
        public void Dispose()
        {
            if (PortModel is INotifyPropertyChanged notifyPort)
                notifyPort.PropertyChanged -= OnPortModelPropertyChanged;
        }
    }
}

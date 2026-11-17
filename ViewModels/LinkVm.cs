using Smec.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Smec.ViewModels
{
    public class LinkVm : ObservableObject, IDisposable
    {
        public Link Model { get; }
        private readonly NodeVm _nodeA;
        private readonly NodeVm _nodeB;
        private readonly GridSettings _gridSettings;

        // Коллекция экранных точек для привязки к <Polyline Points="{Binding ScreenPoints}" />
        public PointCollection ScreenPoints { get; private set; } = new();


        // Прокси-свойства из модели для XAML DataTrigger в LinkStyle
        public string Name => Model.Name;
        public PortState State => Model.State;
        public ObjectStatus LinkStatus => Model.LinkStatus;

        public bool Activity => Model.Activity;

        // Состояние выделения в UI (привязано к последнему DataTrigger в LinkStyle)
        public bool IsSelected
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    OnPropertyChanged(nameof(HasDraggableSegment));
            }
        }

        // Режим соединения
        public LinkRoutingMode RoutingMode
        {
            get => Model.RoutingMode;
            set
            {
                if (Model.RoutingMode != value)
                {
                    Model.ChannelOffset = 0;
                    Model.RoutingMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _baseMidX;
        private double _baseMidY;


        public bool HasDraggableSegment =>
            IsSelected && Model.RoutingMode == LinkRoutingMode.Orthogonal && GetControlledSegmentIndex() >= 0;

        public bool IsHorizontalSegment
        {
            get
            {
                int idx = GetControlledSegmentIndex();
                return idx >= 0 && Math.Abs(ScreenPoints[idx].Y - ScreenPoints[idx + 1].Y) < 0.5;
            }
        }

        public double HandleX
        {
            get
            {
                int idx = GetControlledSegmentIndex();
                return idx >= 0 ? (ScreenPoints[idx].X + ScreenPoints[idx + 1].X) / 2.0 : 0;
            }
        }

        public double HandleY
        {
            get
            {
                int idx = GetControlledSegmentIndex();
                return idx >= 0 ? (ScreenPoints[idx].Y + ScreenPoints[idx + 1].Y) / 2.0 : 0;
            }
        }

        public LinkVm(Link model, NodeVm nodeA, NodeVm nodeB, GridSettings gridSettings)
        {
            Model = model;
            _nodeA = nodeA;
            _nodeB = nodeB;
            _gridSettings = gridSettings;

            // 1. Подписываемся на движение, поворот или зеркалирование обоих подключённых узлов
            _nodeA.TransformChanged += OnEndpointTransformChanged;
            _nodeB.TransformChanged += OnEndpointTransformChanged;

            // 2. Подписываемся на изменение свойств самой модели (State, Activity, LinkStatus, Name)
            if (Model is INotifyPropertyChanged notifyModel)
                notifyModel.PropertyChanged += OnModelPropertyChanged;

            // 3. Подписываемся на изменение шага сетки
            _gridSettings.PropertyChanged += OnGridSettingsChanged;

            // Первичное построение точек линии
            RebuildPoints();
        }

        /// <summary>
        /// Сдвигает центральный сегмент в позицию курсора с привязкой к полушагу сетки (0.5 клетки).
        /// </summary>
        public void MoveSegmentTo(Point targetPos)
        {
            double grid = _gridSettings.GridSize;
            double rawOffsetCells = IsHorizontalSegment
                ? (targetPos.Y - _baseMidY) / grid
                : (targetPos.X - _baseMidX) / grid;

            Model.ChannelOffset = Math.Round(rawOffsetCells * 2.0) / 2.0;
        }

        private void OnEndpointTransformChanged() => RebuildPoints();

        private void OnGridSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GridSettings.GridSize))
                RebuildPoints();
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Link.RoutingMode) or nameof(Link.ChannelOffset))
            {
                OnPropertyChanged(e.PropertyName);
                RebuildPoints();
                return;
            }

            // Пробрасываем уведомление в UI, чтобы сработали соответствующие DataTrigger в LinkStyle
            if (e.PropertyName is nameof(Link.State)
                or nameof(Link.Activity)
                or nameof(Link.LinkStatus)
                or nameof(Link.Name))
            {
                OnPropertyChanged(e.PropertyName);
            }
        }

        #region Построение линков в разных режимах
        
        /// <summary>
        /// Пересчитывает экранные координаты провода от PortA до PortB
        /// с учётом выбранного режима трассировки (Direct / Orthogonal / Diagonal45) и ручных изгибов.
        /// </summary>

        public void RebuildPoints()
        {
            double grid = _gridSettings.GridSize;

            Point startPt = _nodeA.GetPinScreenPosition(Model.PortA.PinNumber);
            Point endPt = _nodeB.GetPinScreenPosition(Model.PortB.PinNumber);

            Model.PortA.Location = new GridPoint((int)Math.Round(startPt.X / grid), (int)Math.Round(startPt.Y / grid));
            Model.PortB.Location = new GridPoint((int)Math.Round(endPt.X / grid), (int)Math.Round(endPt.Y / grid));

            PointCollection pts;

            if (Model.Points.Count > 0 || Model.RoutingMode == LinkRoutingMode.Direct)
            {
                pts = new PointCollection(Model.Points.Count + 2) { startPt };
                foreach (var bend in Model.Points)
                {
                    pts.Add(new Point(bend.X * grid, bend.Y * grid));
                }
                pts.Add(endPt);
            }
            else
            {
                Vector dirA = GetPinExitDirection(_nodeA, Model.PortA.PinNumber);
                Vector dirB = GetPinExitDirection(_nodeB, Model.PortB.PinNumber);

                var route = Model.RoutingMode == LinkRoutingMode.Diagonal45
                    ? BuildDiagonal45Route(startPt, dirA, endPt, dirB)
                    : BuildOrthogonalRoute(startPt, dirA, endPt, dirB);

                pts = [.. route];
            }

            pts.Freeze();
            ScreenPoints = pts;

            OnPropertyChanged(nameof(ScreenPoints));
            OnPropertyChanged(nameof(HasDraggableSegment));
            OnPropertyChanged(nameof(IsHorizontalSegment));
            OnPropertyChanged(nameof(HandleX));
            OnPropertyChanged(nameof(HandleY));
        }

        
        /// <summary>
        /// Трассировка со скосами и вертикальным выносом за габариты корпусов (как на классических схемах триггеров).
        /// </summary>
        private List<Point> BuildDiagonal45Route(Point p1,Vector dir1,Point p2,Vector dir2)
        {
            double grid = _gridSettings.GridSize;
            double stub = grid * 0.5;      // Горизонтальный вылет от кончика пина (половина клетки)
            double clearance = grid * 0.5; // Дополнительный вертикальный отступ от верхней/нижней грани корпуса

            if (Math.Abs(p1.X - p2.X) < 0.5 || Math.Abs(p1.Y - p2.Y) < 0.5)
                return [p1, p2];

            Point s1 = new(p1.X + dir1.X * stub, p1.Y + dir1.Y * stub);
            Point s2 = new(p2.X + dir2.X * stub, p2.Y + dir2.Y * stub);

            bool isHorizontal1 = Math.Abs(dir1.X) > Math.Abs(dir1.Y);
            bool isHorizontal2 = Math.Abs(dir2.X) > Math.Abs(dir2.Y);

            // Если это перекрёстная связь между верхним и нижним корпусами (как в RS-триггере)
            if (isHorizontal1 && isHorizontal2 && _nodeA != _nodeB)
            {
                double topA = _nodeA.PixelY - (_nodeA.BodyGeometry.Bounds.IsEmpty ? _nodeA.HalfHeight : _nodeA.BodyGeometry.Bounds.Height / 2.0);
                double bottomA = _nodeA.PixelY + (_nodeA.BodyGeometry.Bounds.IsEmpty ? _nodeA.HalfHeight : _nodeA.BodyGeometry.Bounds.Height / 2.0);
                double topB = _nodeB.PixelY - (_nodeB.BodyGeometry.Bounds.IsEmpty ? _nodeB.HalfHeight : _nodeB.BodyGeometry.Bounds.Height / 2.0);
                double bottomB = _nodeB.PixelY + (_nodeB.BodyGeometry.Bounds.IsEmpty ? _nodeB.HalfHeight : _nodeB.BodyGeometry.Bounds.Height / 2.0);

                // Верхний элемент (_nodeA) -> Нижний элемент (_nodeB)
                if (bottomA + clearance * 2 < topB)
                {
                    double bendY1 = bottomA + clearance; // Опускаемся ниже низа верхнего корпуса
                    double bendY2 = topB - clearance;    // Останавливаемся выше верха нижнего корпуса

                    return SimplifyPoints([
                        p1,
                s1,
                new Point(s1.X, bendY1),
                new Point(s2.X, bendY2),
                s2,
                p2
                    ]);
                }

                // Нижний элемент (_nodeA) -> Верхний элемент (_nodeB)
                if (bottomB + clearance * 2 < topA)
                {
                    double bendY1 = topA - clearance;    // Поднимаемся выше верха нижнего корпуса
                    double bendY2 = bottomB + clearance; // Останавливаемся ниже низа верхнего корпуса

                    return SimplifyPoints([
                        p1,
                s1,
                new Point(s1.X, bendY1),
                new Point(s2.X, bendY2),
                s2,
                p2
                    ]);
                }
            }

            // Стандартный скос 45° для всех остальных случаев (прямые каскады без обхода)
            double dx = Math.Abs(s2.X - s1.X);
            double dy = Math.Abs(s2.Y - s1.Y);
            double signX = Math.Sign(s2.X - s1.X);
            double signY = Math.Sign(s2.Y - s1.Y);

            if (dx >= dy)
            {
                double straightX = (dx - dy) / 2.0;
                return SimplifyPoints([
                    p1,
            s1,
            new Point(s1.X + signX * straightX, s1.Y),
            new Point(s2.X - signX * straightX, s2.Y),
            s2,
            p2
                ]);
            }
            else
            {
                double straightY = (dy - dx) / 2.0;
                return SimplifyPoints([
                    p1,
            s1,
            new Point(s1.X, s1.Y + signY * straightY),
            new Point(s2.X, s2.Y - signY * straightY),
            s2,
            p2
                ]);
            }
        }


        /// <summary>
        /// Ортогональная трассировка строго под 90° по сетке с обходом корпусов.
        /// </summary>
        private List<Point> BuildOrthogonalRoute(Point p1, Vector dir1, Point p2, Vector dir2)
        {
            double grid = _gridSettings.GridSize;
            double stub = (_nodeA == _nodeB) ? grid * 0.5 : grid;

            // Если пины лежат на одной прямой (вертикальной или горизонтальной) и пользователь ещё не двигал канал вручную — соединяем напрямую
            if (Math.Abs(Model.ChannelOffset) < 0.1)
            {
                if (Math.Abs(p1.X - p2.X) < 0.5 && dir1.X == 0 && dir2.X == 0)
                    return [p1, p2];

                if (Math.Abs(p1.Y - p2.Y) < 0.5 && dir1.Y == 0 && dir2.Y == 0 &&
                    ((dir1.X > 0 && p2.X >= p1.X) || (dir1.X < 0 && p2.X <= p1.X)))
                    return [p1, p2];
            }

            Point s1 = new(p1.X + dir1.X * stub, p1.Y + dir1.Y * stub);
            Point s2 = new(p2.X + dir2.X * stub, p2.Y + dir2.Y * stub);

            bool isHorizontal1 = Math.Abs(dir1.X) > Math.Abs(dir1.Y);
            bool isHorizontal2 = Math.Abs(dir2.X) > Math.Abs(dir2.Y);

            // 1. Оба пина горизонтальные (средний сегмент вертикальный)
            if (isHorizontal1 && isHorizontal2)
            {
                bool facingEachOther = (dir1.X > 0 && dir2.X < 0 && s1.X <= s2.X) ||
                                       (dir1.X < 0 && dir2.X > 0 && s1.X >= s2.X);

                if (facingEachOther)
                {
                    double dx = Math.Abs(p2.X - p1.X);
                    double baseMidX = SnapToGrid((p1.X + p2.X) / 2.0, grid);

                    double autoOffset = 0;
                    if (dx >= grid * 3)
                    {
                        int laneStep = ((int)Math.Round(Math.Abs(p1.Y + p2.Y) / (grid * 2.0)) % 3) + 1;
                        double direction = p1.Y < p2.Y ? -1.0 : 1.0;
                        autoOffset = direction * laneStep * (grid * 0.5);
                    }

                    _baseMidX = baseMidX + autoOffset;

                    double minX = Math.Min(p1.X, p2.X) + grid;
                    double maxX = Math.Max(p1.X, p2.X) - grid;
                    double midX = minX <= maxX
                        ? Math.Clamp(_baseMidX + Model.ChannelOffset * grid, minX, maxX)
                        : _baseMidX;

                    return SimplifyPoints([p1, new Point(midX, p1.Y), new Point(midX, p2.Y), p2]);
                }

                double topA = _nodeA.PixelY - (_nodeA.BodyGeometry.Bounds.IsEmpty ? _nodeA.HalfHeight : _nodeA.BodyGeometry.Bounds.Height / 2.0);
                double bottomA = _nodeA.PixelY + (_nodeA.BodyGeometry.Bounds.IsEmpty ? _nodeA.HalfHeight : _nodeA.BodyGeometry.Bounds.Height / 2.0);
                double topB = _nodeB.PixelY - (_nodeB.BodyGeometry.Bounds.IsEmpty ? _nodeB.HalfHeight : _nodeB.BodyGeometry.Bounds.Height / 2.0);
                double bottomB = _nodeB.PixelY + (_nodeB.BodyGeometry.Bounds.IsEmpty ? _nodeB.HalfHeight : _nodeB.BodyGeometry.Bounds.Height / 2.0);

                double bypassY;

                if (_nodeA != _nodeB && bottomA + grid <= topB)
                {
                    double offset = (topB - bottomA) >= grid * 1.5 ? -grid * 0.5 : 0;
                    bypassY = SnapToGrid((bottomA + topB) / 2.0 + offset, grid);
                }
                else if (_nodeA != _nodeB && bottomB + grid <= topA)
                {
                    double offset = (topA - bottomB) >= grid * 1.5 ? grid * 0.5 : 0;
                    bypassY = SnapToGrid((bottomB + topA) / 2.0 + offset, grid);
                }
                else
                {
                    double routeAboveY = SnapToGrid(Math.Min(topA, topB) - grid * 0.5, grid);
                    double routeBelowY = SnapToGrid(Math.Max(bottomA, bottomB) + grid * 0.5, grid);
                    double distAbove = Math.Abs(p1.Y - routeAboveY) + Math.Abs(p2.Y - routeAboveY);
                    double distBelow = Math.Abs(p1.Y - routeBelowY) + Math.Abs(p2.Y - routeBelowY);
                    bypassY = distAbove <= distBelow ? routeAboveY : routeBelowY;
                }

                _baseMidY = bypassY;
                double finalBypassY = bypassY + Model.ChannelOffset * grid;

                return SimplifyPoints([p1, s1, new Point(s1.X, finalBypassY), new Point(s2.X, finalBypassY), s2, p2]);
            }

            // 2. Один пин горизонтальный, второй вертикальный (GND / питание)
            if (isHorizontal1 && !isHorizontal2)
            {
                bool pinFacingWire = (p2.Y >= p1.Y && dir2.Y < 0) || (p2.Y <= p1.Y && dir2.Y > 0);
                if (pinFacingWire && ((dir1.X > 0 && s2.X >= s1.X) || (dir1.X < 0 && s2.X <= s1.X)) && Math.Abs(Model.ChannelOffset) < 0.1)
                    return SimplifyPoints([p1, new Point(p2.X, p1.Y), p2]);

                double topA = _nodeA.PixelY - (_nodeA.BodyGeometry.Bounds.IsEmpty ? _nodeA.HalfHeight : _nodeA.BodyGeometry.Bounds.Height / 2.0);
                double bottomA = _nodeA.PixelY + (_nodeA.BodyGeometry.Bounds.IsEmpty ? _nodeA.HalfHeight : _nodeA.BodyGeometry.Bounds.Height / 2.0);

                double turnY;
                if (p2.Y < topA)
                    turnY = Math.Min(s2.Y, topA - grid * 0.5);
                else if (p2.Y > bottomA)
                    turnY = Math.Max(s2.Y, bottomA + grid * 0.5);
                else
                    turnY = (dir2.Y < 0)
                        ? Math.Max(s2.Y, bottomA + grid * 0.5)
                        : Math.Min(s2.Y, topA - grid * 0.5);

                _baseMidX = s1.X;
                double rawVertX = s1.X + Model.ChannelOffset * grid;

                // Не позволяем вертикали заходить за кончик пина внутрь корпуса _nodeA
                double vertX = dir1.X < 0
                    ? Math.Min(rawVertX, p1.X - grid * 0.5)
                    : Math.Max(rawVertX, p1.X + grid * 0.5);

                return SimplifyPoints([p1, new Point(vertX, p1.Y), new Point(vertX, turnY), new Point(s2.X, turnY), p2]);
            }

            if (!isHorizontal1 && isHorizontal2)
            {
                bool pinFacingWire = (p1.Y >= p2.Y && dir1.Y < 0) || (p1.Y <= p2.Y && dir1.Y > 0);
                if (pinFacingWire && ((dir2.X > 0 && s1.X >= s2.X) || (dir2.X < 0 && s1.X <= s2.X)) && Math.Abs(Model.ChannelOffset) < 0.1)
                    return SimplifyPoints([p1, new Point(p1.X, p2.Y), p2]);

                double topB = _nodeB.PixelY - (_nodeB.BodyGeometry.Bounds.IsEmpty ? _nodeB.HalfHeight : _nodeB.BodyGeometry.Bounds.Height / 2.0);
                double bottomB = _nodeB.PixelY + (_nodeB.BodyGeometry.Bounds.IsEmpty ? _nodeB.HalfHeight : _nodeB.BodyGeometry.Bounds.Height / 2.0);

                double turnY;
                if (p1.Y < topB)
                    turnY = Math.Min(s1.Y, topB - grid * 0.5);
                else if (p1.Y > bottomB)
                    turnY = Math.Max(s1.Y, bottomB + grid * 0.5);
                else
                    turnY = (dir1.Y < 0)
                        ? Math.Max(s1.Y, bottomB + grid * 0.5)
                        : Math.Min(s1.Y, topB - grid * 0.5);

                _baseMidX = s2.X;
                double rawVertX = s2.X + Model.ChannelOffset * grid;

                // Не позволяем вертикали заходить за кончик пина внутрь корпуса _nodeB
                double vertX = dir2.X < 0
                    ? Math.Min(rawVertX, p2.X - grid * 0.5)
                    : Math.Max(rawVertX, p2.X + grid * 0.5);

                return SimplifyPoints([p1, new Point(s1.X, turnY), new Point(vertX, turnY), new Point(vertX, p2.Y), p2]);
            }



            // 3. Оба пина вертикальные (средний сегмент горизонтальный)
            _baseMidY = SnapToGrid((p1.Y + p2.Y) / 2.0, grid);

            double minY = Math.Min(p1.Y, p2.Y) + grid;
            double maxY = Math.Max(p1.Y, p2.Y) - grid;
            double midY = minY <= maxY
                ? Math.Clamp(_baseMidY + Model.ChannelOffset * grid, minY, maxY)
                : _baseMidY;

            return SimplifyPoints([p1, new Point(p1.X, midY), new Point(p2.X, midY), p2]);
        }



        private static Vector GetPinExitDirection(NodeVm node, string pinNumber)
        {
            var pin = node.Pins.FirstOrDefault(p => p.PortModel?.PinNumber == pinNumber || p.Definition.Number == pinNumber);
            if (pin == null)
                return new Vector(1, 0);

            Vector localDir = new(pin.X2 - pin.X1, pin.Y2 - pin.Y1);
            if (localDir.Length > 0)
                localDir.Normalize();
            else
                localDir = new Vector(1, 0);

            Point rotated = node.GraphicsTransform.Transform(new Point(localDir.X, localDir.Y));
            return new Vector(Math.Round(rotated.X), Math.Round(rotated.Y));
        }

        private static double SnapToGrid(double value, double grid) => Math.Round(value / grid) * grid;


        // Индекс сегмента, которым управляет ChannelOffset
        private int GetControlledSegmentIndex()
        {
            if (ScreenPoints.Count == 3)
            {
                // Для Г-образного угла (3 точки) выбираем именно вертикальный сегмент (где X1 == X2)
                return Math.Abs(ScreenPoints[0].X - ScreenPoints[1].X) < 0.5 ? 0 : 1;
            }

            if (ScreenPoints.Count == 4)
            {
                bool isHor1 = Math.Abs(GetPinExitDirection(_nodeA, Model.PortA.PinNumber).X) > 0.5;
                bool isHor2 = Math.Abs(GetPinExitDirection(_nodeB, Model.PortB.PinNumber).X) > 0.5;
                if (isHor1 == isHor2)
                    return 1;
                return isHor1 ? 1 : 2;
            }

            if (ScreenPoints.Count >= 5)
            {
                bool isHor1 = Math.Abs(GetPinExitDirection(_nodeA, Model.PortA.PinNumber).X) > 0.5;
                return isHor1 ? 1 : ScreenPoints.Count - 3;
            }

            return -1;
        }


        /// <summary>
        /// Удаляет дубликаты и промежуточные точки, лежащие на одной прямой (0°, 45°, 90°).
        /// </summary>
        private static List<Point> SimplifyPoints(List<Point> raw)
        {
            if (raw.Count <= 2) return raw;

            var result = new List<Point> { raw[0] };
            for (int i = 1; i < raw.Count - 1; i++)
            {
                Point prev = result[^1];
                Point curr = raw[i];
                Point next = raw[i + 1];

                if ((curr - prev).Length < 0.5)
                    continue;

                // Векторное произведение: если близко к 0 — все 3 точки лежат на одной прямой под любым углом
                double cross = Math.Abs((curr.X - prev.X) * (next.Y - curr.Y) - (curr.Y - prev.Y) * (next.X - curr.X));
                if (cross >= 0.5)
                    result.Add(curr);
            }

            if ((raw[^1] - result[^1]).Length >= 0.5)
                result.Add(raw[^1]);

            return result;
        }
        #endregion

        /// <summary>
        /// Освобождает все подписки при удалении провода или очистке схемы.
        /// </summary>
        public void Dispose()
        {
            _nodeA.TransformChanged -= OnEndpointTransformChanged;
            _nodeB.TransformChanged -= OnEndpointTransformChanged;
            _gridSettings.PropertyChanged -= OnGridSettingsChanged;

            if (Model is INotifyPropertyChanged notifyModel)
                notifyModel.PropertyChanged -= OnModelPropertyChanged;
        }

    }
}



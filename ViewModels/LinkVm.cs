using Smec.Models;
using System.Collections.ObjectModel;
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
        public PointCollection ScreenPoints { get; private set; } = [];


        // Прокси-свойства из модели для XAML DataTrigger в LinkStyle
        public string Name => Model.Name;
        public PortState State => Model.State;
        public ObjectStatus LinkStatus => Model.LinkStatus;

        public bool Activity => Model.Activity;


        // Базовые координаты сегментов (точки отсчёта для ручного сдвига)
        private double _baseHandle1X;
        private double _baseHandle1Y;
        private double _baseHandle2Y;
        private double _baseHandle3X;

        public ObservableCollection<LinkHandleVm> Handles { get; } = [];

        /// <summary>
        /// Возвращает true, если у провода есть ручные сдвиги сегментов.
        /// </summary>
        public bool HasSegmentOffsets => Model.SegmentOffsets.Any(o => Math.Abs(o) > 0.001);

        /// <summary>
        /// Сбрасывает все ручные сдвиги сегментов к автоматической трассе.
        /// </summary>
        public void ResetSegmentOffsets()
        {
            if (!HasSegmentOffsets)
                return;

            Model.ResetSegmentOffsets();
            OnPropertyChanged(nameof(HasSegmentOffsets));
        }


        // Состояние выделения в UI (привязано к последнему DataTrigger в LinkStyle)
        public bool IsSelected
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                {
                    UpdateHandles();
                }
            }
        }

        // Режим соединенеия
        public LinkRoutingMode RoutingMode
        {
            get => Model.RoutingMode;
            set
            {
                if (Model.RoutingMode != value)
                {
                    Model.Points.Clear();
                    Model.ResetSegmentOffsets();
                    Model.RoutingMode = value;
                    OnPropertyChanged();
                    RebuildPoints();
                }
            }
        }

        private void OnEndpointTransformChanged()
        {
            if (Model.SegmentOffsets.Any(o => Math.Abs(o) > 0.001))
            {
                // ResetSegmentOffsets() вызовет CollectionChanged -> OnModelPropertyChanged -> RebuildPoints()
                Model.ResetSegmentOffsets();
            }
            else
            {
                RebuildPoints();
            }
        }

        // --- Единый метод перетаскивания для всех трёх ручек (индексы 0, 1, 2) ---
        public void MoveHandleTo(int handleIndex, Point targetPos)
        {
            double grid = _gridSettings.GridSize;
            bool isHor1 = GetControlledSegmentIndex() is int idx && idx >= 0 &&
                          Math.Abs(ScreenPoints[idx].Y - ScreenPoints[idx + 1].Y) < 0.5;

            double rawOffsetCells = handleIndex switch
            {
                1 => (targetPos.Y - _baseHandle2Y) / grid,
                2 => (targetPos.X - _baseHandle3X) / grid,
                _ => isHor1
                        ? (targetPos.Y - _baseHandle1Y) / grid
                        : (targetPos.X - _baseHandle1X) / grid
            };

            Model.SetSegmentOffset(handleIndex, Math.Round(rawOffsetCells * 4.0) / 4.0);
        }

        // Индекс сегмента, которым управляет ChannelOffset
        private int GetControlledSegmentIndex()
        {
            if (ScreenPoints.Count == 3)
                return Math.Abs(ScreenPoints[0].X - ScreenPoints[1].X) < 0.5 ? 0 : 1;

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

        private void UpdateHandles()
        {
            var p = ScreenPoints;

            if (!IsSelected || Model.RoutingMode != LinkRoutingMode.Orthogonal || p == null || p.Count < 3)
            {
                Handles.Clear();
                return;
            }

            var next = new List<LinkHandleVm>(3);

            // 1. Основная ручка (индекс 0)
            int idx1 = GetControlledSegmentIndex();
            if (idx1 >= 0 && idx1 + 1 < p.Count)
            {
                var a = p[idx1];
                var b = p[idx1 + 1];
                bool isHor1 = Math.Abs(a.Y - b.Y) < 0.5;
                next.Add(new LinkHandleVm(0, (a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, isHor1));
            }

            // 2 и 3. Дополнительные ручки (индексы 1 и 2) для 6-точечного обходного провода
            if (p.Count == 6)
            {
                var p2 = p.ElementAt(2);
                var p3 = p.ElementAt(3);
                var p4 = p.ElementAt(4);

                // Ручка 2 (индекс 1): горизонтальный пролёт между точками 2 и 3
                next.Add(new LinkHandleVm(1, (p2.X + p3.X) / 2.0, (p2.Y + p3.Y) / 2.0, true));

                // Ручка 3 (индекс 2): вторая вертикаль между точками 3 и 4
                next.Add(new LinkHandleVm(2, (p3.X + p4.X) / 2.0, (p3.Y + p4.Y) / 2.0, false));
            }

            // Обновляем координаты существующих ручек на месте, чтобы Thumb не терял захват мыши
            if (Handles.Count == next.Count)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    Handles[i].X = next[i].X;
                    Handles[i].Y = next[i].Y;
                    Handles[i].IsHorizontal = next[i].IsHorizontal;
                }
            }
            else
            {
                Handles.Clear();
                foreach (var h in next)
                    Handles.Add(h);
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

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Link.RoutingMode) or nameof(Link.SegmentOffsets))
            {
                OnPropertyChanged(e.PropertyName);
                OnPropertyChanged(nameof(HasSegmentOffsets));
                RebuildPoints();
                return;
            }

            if (e.PropertyName is nameof(Link.State)
                or nameof(Link.Activity)
                or nameof(Link.LinkStatus)
                or nameof(Link.Name))
            {
                OnPropertyChanged(e.PropertyName);
            }
        }



        private void OnGridSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GridSettings.GridSize))
                RebuildPoints();
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

                var route = Model.RoutingMode == LinkRoutingMode.Diagonal
                    ? BuildDiagonal45Route(startPt, dirA, endPt, dirB)
                    : BuildOrthogonalRoute(startPt, dirA, endPt, dirB);

                pts = [.. route];
            }

            pts.Freeze();
            ScreenPoints = pts;

            OnPropertyChanged(nameof(ScreenPoints));
            UpdateHandles();
        }




        /// <summary>
        /// Трассировка со скосами и вертикальным выносом за габариты корпусов (как на классических схемах триггеров).
        /// </summary>
        private List<Point> BuildDiagonal45Route(Point p1, Vector dir1, Point p2, Vector dir2)
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

            double offset1 = Model.GetSegmentOffset(0);
            double offset2 = Model.GetSegmentOffset(1);
            double offset3 = Model.GetSegmentOffset(2);

            // Если пины лежат на одной прямой (вертикальной или горизонтальной) и ручных сдвигов нет — соединяем напрямую
            if (Math.Abs(offset1) < 0.1 && Math.Abs(offset2) < 0.1 && Math.Abs(offset3) < 0.1)
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

            // Точные верхние и нижние границы корпусов с учётом локального смещения BodyGeometry.Bounds
            double topA = _nodeA.BodyGeometry.Bounds.IsEmpty
                ? _nodeA.PixelY - _nodeA.HalfHeight
                : _nodeA.PixelY + _nodeA.BodyGeometry.Bounds.Top;
            double bottomA = _nodeA.BodyGeometry.Bounds.IsEmpty
                ? _nodeA.PixelY + _nodeA.HalfHeight
                : _nodeA.PixelY + _nodeA.BodyGeometry.Bounds.Bottom;

            double topB = _nodeB.BodyGeometry.Bounds.IsEmpty
                ? _nodeB.PixelY - _nodeB.HalfHeight
                : _nodeB.PixelY + _nodeB.BodyGeometry.Bounds.Top;
            double bottomB = _nodeB.BodyGeometry.Bounds.IsEmpty
                ? _nodeB.PixelY + _nodeB.HalfHeight
                : _nodeB.PixelY + _nodeB.BodyGeometry.Bounds.Bottom;

            // 1. Оба пина горизонтальные
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

                    _baseHandle1X = baseMidX + autoOffset;

                    double minX = Math.Min(p1.X, p2.X) + grid;
                    double maxX = Math.Max(p1.X, p2.X) - grid;
                    double midX = minX <= maxX
                        ? Math.Clamp(_baseHandle1X + offset1 * grid, minX, maxX)
                        : _baseHandle1X;

                    return SimplifyPoints([p1, new Point(midX, p1.Y), new Point(midX, p2.Y), p2]);
                }

                // Перемычка на одном и том же элементе (пины смотрят в одну сторону)
                if (_nodeA == _nodeB && Math.Sign(dir1.X) == Math.Sign(dir2.X))
                {
                    double baseVertX = (dir1.X < 0) ? Math.Min(s1.X, s2.X) : Math.Max(s1.X, s2.X);
                    _baseHandle1X = baseVertX;

                    double rawVertX = baseVertX + offset1 * grid;
                    double minStub = grid * 0.25;

                    double vertX = (dir1.X < 0)
                        ? Math.Min(rawVertX, Math.Min(p1.X, p2.X) - minStub)
                        : Math.Max(rawVertX, Math.Max(p1.X, p2.X) + minStub);

                    return SimplifyPoints([
                        p1,
                new Point(vertX, p1.Y),
                new Point(vertX, p2.Y),
                p2
                    ]);
                }

                // Обход разных корпусов сверху, снизу или через зазор между ними (6 точек, 3 ручки)
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

                // Ручка 2 (индекс 1): горизонтальный пролёт
                _baseHandle2Y = bypassY;
                double finalBypassY = bypassY + offset2 * grid;

                if (Math.Abs(finalBypassY - p1.Y) < 0.5)
                    finalBypassY += (p1.Y >= _nodeA.PixelY) ? grid * 0.5 : -grid * 0.5;
                if (Math.Abs(finalBypassY - p2.Y) < 0.5)
                    finalBypassY += (p2.Y >= _nodeB.PixelY) ? grid * 0.5 : -grid * 0.5;

                // Ручка 1 (индекс 0): первая вертикаль (возле _nodeA)
                _baseHandle1X = s1.X;
                double rawVert1X = s1.X + offset1 * grid;
                double vert1X = dir1.X > 0
                    ? Math.Max(rawVert1X, p1.X + grid * 0.25)
                    : Math.Min(rawVert1X, p1.X - grid * 0.25);

                // Ручка 3 (индекс 2): вторая вертикаль (возле _nodeB)
                _baseHandle3X = s2.X;
                double rawVert2X = s2.X + offset3 * grid;
                double vert2X = dir2.X > 0
                    ? Math.Max(rawVert2X, p2.X + grid * 0.25)
                    : Math.Min(rawVert2X, p2.X - grid * 0.25);

                return SimplifyPoints([
                    p1,
            new Point(vert1X, p1.Y),
            new Point(vert1X, finalBypassY),
            new Point(vert2X, finalBypassY),
            new Point(vert2X, p2.Y),
            p2
                ]);
            }

            // 2. Один пин горизонтальный, второй вертикальный (GND / питание)
            if (isHorizontal1 && !isHorizontal2)
            {
                bool pinFacingWire = (p2.Y >= p1.Y && dir2.Y < 0) || (p2.Y <= p1.Y && dir2.Y > 0);
                if (pinFacingWire && ((dir1.X > 0 && s2.X >= s1.X) || (dir1.X < 0 && s2.X <= s1.X)) && Math.Abs(offset1) < 0.1)
                    return SimplifyPoints([p1, new Point(p2.X, p1.Y), p2]);

                double turnY;
                if (p2.Y < topA)
                    turnY = Math.Min(s2.Y, topA - grid * 0.5);
                else if (p2.Y > bottomA)
                    turnY = Math.Max(s2.Y, bottomA + grid * 0.5);
                else
                    turnY = (dir2.Y < 0)
                        ? Math.Max(s2.Y, bottomA + grid * 0.5)
                        : Math.Min(s2.Y, topA - grid * 0.5);

                _baseHandle1X = s1.X;
                double rawVertX = s1.X + offset1 * grid;

                double vertX = dir1.X < 0
                    ? Math.Min(rawVertX, p1.X - grid * 0.5)
                    : Math.Max(rawVertX, p1.X + grid * 0.5);

                return SimplifyPoints([p1, new Point(vertX, p1.Y), new Point(vertX, turnY), new Point(s2.X, turnY), p2]);
            }

            if (!isHorizontal1 && isHorizontal2)
            {
                bool pinFacingWire = (p1.Y >= p2.Y && dir1.Y < 0) || (p1.Y <= p2.Y && dir1.Y > 0);
                if (pinFacingWire && ((dir2.X > 0 && s1.X >= s2.X) || (dir2.X < 0 && s1.X <= s2.X)) && Math.Abs(offset1) < 0.1)
                    return SimplifyPoints([p1, new Point(p1.X, p2.Y), p2]);

                // Повторное объявление topB и bottomB удалено — используются переменные из начала метода
                double turnY;
                if (p1.Y < topB)
                    turnY = Math.Min(s1.Y, topB - grid * 0.5);
                else if (p1.Y > bottomB)
                    turnY = Math.Max(s1.Y, bottomB + grid * 0.5);
                else
                    turnY = (dir1.Y < 0)
                        ? Math.Max(s1.Y, bottomB + grid * 0.5)
                        : Math.Min(s1.Y, topB - grid * 0.5);

                _baseHandle1X = s2.X;
                double rawVertX = s2.X + offset1 * grid;

                double vertX = dir2.X < 0
                    ? Math.Min(rawVertX, p2.X - grid * 0.5)
                    : Math.Max(rawVertX, p2.X + grid * 0.5);

                return SimplifyPoints([p1, new Point(s1.X, turnY), new Point(vertX, turnY), new Point(vertX, p2.Y), p2]);
            }


            // 3. Оба пина вертикальные (средний сегмент горизонтальный)
            _baseHandle1Y = SnapToGrid((p1.Y + p2.Y) / 2.0, grid);

            double minY = Math.Min(p1.Y, p2.Y) + grid;
            double maxY = Math.Max(p1.Y, p2.Y) - grid;
            double midY = minY <= maxY
                ? Math.Clamp(_baseHandle1Y + offset1 * grid, minY, maxY)
                : _baseHandle1Y;

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



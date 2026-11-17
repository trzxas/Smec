using Smec.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Smec.ViewModels
{

    public class NodeVm : ObservableObject, IDisposable
    {
        public Node Model { get; }
        private readonly SymbolDefinition _symbolDef;
        private readonly GridSettings _gridSettings;

        // Графика корпуса и пины для XAML
        public Geometry BodyGeometry { get; private set; } = Geometry.Empty;
        public ObservableCollection<PinVm> Pins { get; } = [];

        // Зона захвата мышкой (HitBox) в пикселях
        public double HitBoxX => -(_symbolDef.Width / 2.0) * _gridSettings.GridSize;
        public double HitBoxY => -(_symbolDef.Height / 2.0) * _gridSettings.GridSize;
        public double HitBoxWidth => _symbolDef.Width * _gridSettings.GridSize;
        public double HitBoxHeight => _symbolDef.Height * _gridSettings.GridSize;

        // Позиция центра (0, 0) элемента на холсте в пикселях (с привязкой к сетке)
        public double PixelX
        {
            get => Model.Location.X * _gridSettings.GridSize;
            set
            {
                int snappedX = (int)Math.Round(value / _gridSettings.GridSize);
                if (Model.Location.X != snappedX)
                {
                    Model.Location = Model.Location with { X = snappedX };
                    OnPropertyChanged();
                    NotifyPositionChanged();
                }
            }
        }

        public double PixelY
        {
            get => Model.Location.Y * _gridSettings.GridSize;
            set
            {
                int snappedY = (int)Math.Round(value / _gridSettings.GridSize);
                if (Model.Location.Y != snappedY)
                {
                    Model.Location = Model.Location with { Y = snappedY };
                    OnPropertyChanged();
                    NotifyPositionChanged();
                }
            }
        }

        public int ZIndex => IsSelected ? 999 : Model.ZIndex;

        public Transform GraphicsTransform
        {
            get
            {
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(ScaleX, ScaleY));
                group.Children.Add(new RotateTransform(Rotation));
                group.Freeze(); // Замораживаем для максимальной производительности WPF!
                return group;
            }
        }

        // Поворот и зеркалирование
        public int Rotation
        {
            get => Model.Rotation;
            set
            {
                int normalized = ((value % 360) + 360) % 360;
                if (Model.Rotation != normalized)
                {
                    Model.Rotation = normalized;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GraphicsTransform)); // <-- Обновляем трансформацию!
                    UpdateLabelOffsets();
                    NotifyPositionChanged();
                }
            }
        }
        public bool MirrorX
        {
            get => Model.MirrorX;
            set
            {
                if (Model.MirrorX != value)
                {
                    Model.MirrorX = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ScaleX)); // Уведомляем XAML ScaleTransform!
                    OnPropertyChanged(nameof(GraphicsTransform)); // <-- Обновляем трансформацию!
                    NotifyPositionChanged();           // Пересчитываем координаты подключённых проводов
                }
            }
        }

        public bool MirrorY
        {
            get => Model.MirrorY;
            set
            {
                if (Model.MirrorY != value)
                {
                    Model.MirrorY = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ScaleY)); // Уведомляем XAML ScaleTransform!
                    OnPropertyChanged(nameof(GraphicsTransform)); // <-- Обновляем трансформацию!
                    NotifyPositionChanged();           // Пересчитываем координаты подключённых проводов
                }
            }
        }

        // Свойства для привязки в XAML: <ScaleTransform ScaleX="{Binding ScaleX}" ScaleY="{Binding ScaleY}" />
        public double ScaleX => Model.MirrorX ? -1.0 : 1.0;
        public double ScaleY => Model.MirrorY ? -1.0 : 1.0;


        // Текстовые атрибуты
        public string Designator
        {
            get => Model.Designator;
            set { Model.Designator = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => Model.Value;
            set { Model.Value = value; OnPropertyChanged(); }
        }

        public bool ShowDesignator { get; set; } = true;
        public bool ShowValue { get; set; } = true;


        // Смещения надписей (остаются горизонтальными при повороте детали)
        public double DesignatorOffsetX { get; private set; }
        public double DesignatorOffsetY { get; private set; }
        public double ValueOffsetX { get; private set; }
        public double ValueOffsetY { get; private set; }

        // Для функциональной метки
        public string? FunctionText => _symbolDef.FunctionText;
        public bool HasFunctionText => !string.IsNullOrWhiteSpace(FunctionText);

        public double FuncBoxX { get; private set; }
        public double FuncBoxY { get; private set; }
        public double FuncBoxWidth { get; private set; }
        public double FuncBoxHeight { get; private set; }

        public VerticalAlignment FunctionVerticalAlignment => _symbolDef.IsPlaceholder ? VerticalAlignment.Center : VerticalAlignment.Top;

        public double FunctionFontSize =>_symbolDef.IsPlaceholder ? 12: 8;

        public double TotalWidth { get; private set; }
        public double TotalHeight { get; private set; }
        public double HalfWidth { get; private set; }
        public double HalfHeight { get; private set; }
        public double ContainerLeft => PixelX - HalfWidth;
        public double ContainerTop => PixelY - HalfHeight;

        public double BodyLeft => BodyGeometry.Bounds.IsEmpty ? 0 : BodyGeometry.Bounds.Left;
        public double BodyTop => BodyGeometry.Bounds.IsEmpty ? 0 : BodyGeometry.Bounds.Top;


        public bool IsFilled => _symbolDef.IsFilled;

       

        public bool HasCustomBody => !string.IsNullOrEmpty(Model.CustomBodyTemplate);
        public string? CustomBodyTemplateKey => Model.CustomBodyTemplate;

        // Состояние выделения в UI
        public bool IsSelected
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ZIndex));
                }
            }
        }



        // Событие для проводов (LinkVm), чтобы они перерисовывались при движении/повороте узла
        public event Action? TransformChanged;

        public NodeVm(Node model, SymbolDefinition symbolDef, GridSettings gridSettings)
        {
            Model = model;
            _symbolDef = symbolDef;
            _gridSettings = gridSettings;

            Model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Node.Value))
                    OnPropertyChanged(nameof(Value));
                else if (e.PropertyName == nameof(Node.Designator))
                    OnPropertyChanged(nameof(Designator));
            };

            RebuildGeometry();
            UpdateLabelOffsets();
            RefreshLabels();

            _gridSettings.PropertyChanged += OnGridSettingsChanged;
        }

        private void OnGridSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GridSettings.GridSize))
            {
                OnPropertyChanged(nameof(PixelX));
                OnPropertyChanged(nameof(PixelY));
                OnPropertyChanged(nameof(HitBoxX));
                OnPropertyChanged(nameof(HitBoxY));
                OnPropertyChanged(nameof(HitBoxWidth));
                OnPropertyChanged(nameof(HitBoxHeight));

                RebuildGeometry();
                UpdateLabelOffsets();
                NotifyPositionChanged();
            }
        }

        private void RebuildGeometry()
        {
            double grid = _gridSettings.GridSize;
            BodyGeometry = GeometryBuilder.Build(_symbolDef, grid);
            OnPropertyChanged(nameof(BodyGeometry));
            if (Pins.Count == 0)
            {
                foreach (var pinDef in _symbolDef.Pins)
                {
                    var matchingPort = Model.Ports.FirstOrDefault(p => p.PinNumber == pinDef.Number);
                    Pins.Add(new PinVm(pinDef, grid, matchingPort));
                }
            }
            else
            {
                foreach (var pin in Pins)
                    pin.UpdateGeometry(grid);
            }
            UpdateLabelOffsets();
            UpdateBounds();

        }

        public void UpdatePixelPosition()
        {
            //double grid = _gridSettings.GridSize;
            //PixelX = Model.Location.X * grid;
            //PixelY = Model.Location.Y * grid;
            OnPropertyChanged(nameof(PixelX));
            OnPropertyChanged(nameof(PixelY));
            NotifyPositionChanged(); // Сообщаем проводам (LinkVm), что узел сдвинулся
        }

        public void SetGridLocation(GridPoint newLocation)
        {
            if (Model.Location == newLocation)
                return;

            // Меняем Location в модели — это автоматически вызовет CurrentModel_PropertyChanged -> UpdatePixelPosition()!
            Model.Location = newLocation;
            //UpdatePixelPosition();
        }

        // Настройка отступов от корпуса
        private void UpdateLabelOffsets()
        {
            double grid = _gridSettings.GridSize;
            Rect bounds = BodyGeometry.Bounds;

            // Защита: если геометрия ещё пуста, берём базовый квадрат 1x1 клетку вокруг центра (0, 0)
            if (bounds.IsEmpty || bounds.Width == 0 || bounds.Height == 0)
            {
                bounds = new Rect(-grid * 0.5, -grid * 0.5, grid, grid);
            }
            else if (Rotation == 90 || Rotation == 270)
            {
                bounds = new Rect(bounds.Top, bounds.Left, bounds.Height, bounds.Width);
            }

            // 1. Позиционное обозначение (D3) над верхней гранью
            double topOffset = 12; // Смещение надписи относительно верха
            DesignatorOffsetX = bounds.Left;
            DesignatorOffsetY = bounds.Top - topOffset;

            // 2. Номинал под нижней гранью
            double bottomOffset = 2; // Смещение надписи относительно низа
            ValueOffsetX = bounds.Left;
            ValueOffsetY = bounds.Bottom + bottomOffset;

            // 3. Рамка для знака функции (≥1) прижимается к ВЕРХНЕЙ грани корпуса!
            FuncBoxX = bounds.Left;
            FuncBoxY = bounds.Top;
            FuncBoxWidth = bounds.Width;
            FuncBoxHeight = Math.Max(20, bounds.Height);

            OnPropertyChanged(nameof(DesignatorOffsetX));
            OnPropertyChanged(nameof(DesignatorOffsetY));
            OnPropertyChanged(nameof(ValueOffsetX));
            OnPropertyChanged(nameof(ValueOffsetY));
            OnPropertyChanged(nameof(FuncBoxX));
            OnPropertyChanged(nameof(FuncBoxY));
            OnPropertyChanged(nameof(FuncBoxWidth));
            OnPropertyChanged(nameof(FuncBoxHeight));
        }


        public void RefreshLabels()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Designator));
        }

        private void UpdateBounds()
        {
            Rect totalBounds = BodyGeometry.Bounds;
            if (totalBounds.IsEmpty)
            {
                double grid = _gridSettings.GridSize;
                totalBounds = new Rect(-grid * 0.5, -grid * 0.5, grid, grid);
            }

            // Расширяем границы, чтобы в них гарантированно вошли все выводы (Pins)
            foreach (var pin in Pins)
            {
                totalBounds.Union(new Point(pin.X1, pin.Y1));
                totalBounds.Union(new Point(pin.X2, pin.Y2));
            }

            // Добавляем небольшой отступ (4 px с каждой стороны) под толщину линий и кружки инверсии
            totalBounds.Inflate(4, 4);

            TotalWidth = totalBounds.Width;
            TotalHeight = totalBounds.Height;
            HalfWidth = -totalBounds.Left; // Сдвиг центра (0, 0) от левого края
            HalfHeight = -totalBounds.Top; // Сдвиг центра (0, 0) от верхнего края

            OnPropertyChanged(nameof(TotalWidth));
            OnPropertyChanged(nameof(TotalHeight));
            OnPropertyChanged(nameof(HalfWidth));
            OnPropertyChanged(nameof(HalfHeight));
        }

        /// <summary>
        /// Вычисляет мировые пиксельные координаты конкретного пина на холсте (для привязки проводов).
        /// </summary>
        public Point GetPinScreenPosition(string pinNumber)
        {
            var pin = Pins.FirstOrDefault(p => p.Definition.Number == pinNumber);
            if (pin == null)
                return new Point(PixelX, PixelY);

            double lx = pin.X2;
            double ly = pin.Y2;

            if (Model.MirrorX) lx = -lx;
            if (Model.MirrorY) ly = -ly;

            double rad = Model.Rotation * Math.PI / 180.0;
            double rotX = lx * Math.Cos(rad) - ly * Math.Sin(rad);
            double rotY = lx * Math.Sin(rad) + ly * Math.Cos(rad);

            return new Point(PixelX + rotX, PixelY + rotY);
        }

        private void NotifyPositionChanged() => TransformChanged?.Invoke();

        public void Dispose()
        {
            _gridSettings.PropertyChanged -= OnGridSettingsChanged;
            foreach (var pin in Pins)
                pin.Dispose();
        }

    }
}




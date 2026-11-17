using Smec.Models;
using Smec.Services;
using Smec.Services.DataServices;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Link = Smec.Models.Link;
using Point = System.Windows.Point;

namespace Smec.ViewModels
{

    public partial class SysViewModel : ObservableObject
    {
        // Событие очистки всех выбранных объектов (Node, Link)
        public event Action? RequestClearSelection;



        // Project file extension
        const string ProjExtension = ".prj";
        const string BackupExtension = ".back";
        const string defaultFilter = $"Project(*{ProjExtension})|*{ProjExtension}|Все файлы(*.*)|*.*";


        public ObservableCollection<NodeVm> Components { get; } = [];
        public ObservableCollection<LinkVm> Links { get; } = [];
        public ObservableCollection<NodeVm> StockList { get; set; } = [];

        public ObservableCollection<JunctionVm> Junctions { get; } = [];


        /// <summary>
        /// Главное свойство модели
        /// При изменении модели (например, при открытии нового файла) происходит: 
        /// сохраняется ссылка на старую модель, отписка от событий старой модели,
        /// очистка визуальных коллекций Components , Links, StockList
        /// Далее настраивается новая модель: подписка на события, наполнение StockList и визуальных коллекций
        /// 
        /// </summary>

        public SysModel? CurrentModel
        {
            get;
            set
            {
                var oldModel = field;

                // SetProperty сам проверит равенство, обновит field и вызовет OnPropertyChanged()
                if (!SetProperty(ref field, value))
                    return;

                if (oldModel != null)
                {
                    // 1. Отписываемся от старой модели
                    oldModel.PropertyChanged -= CurrentModel_PropertyChanged;
                    oldModel.Nodes.CollectionChanged -= OnModelNodesChanged;
                    oldModel.Links.CollectionChanged -= OnModelLinksChanged;
                }
                ClearVisualScene();
                StockList.Clear();  // <-- Очищаем  при смене или закрытии модели


                // 2. Настраиваем новую модель
                if (field == null)
                {
                    CurrentFilePath = string.Empty;
                }
                else
                {
                    field.PropertyChanged += CurrentModel_PropertyChanged;
                    field.Nodes.CollectionChanged += OnModelNodesChanged;
                    field.Links.CollectionChanged += OnModelLinksChanged;


                    RebuildVisualScene(field);  // Первичная отрисовка загруженной схемы
                }

                UpdateContentBounds();
                VmStatus = ModelStatus.UnChanged;
                CommandManager.InvalidateRequerySuggested();
            }
        } = null;

        private void OnModelNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ClearVisualScene();
                return;
            }

            // Автоматически создаём NodeVm при добавлении Node в модель
            if (e.NewItems != null)
            {
                foreach (Node newNode in e.NewItems)
                {
                    // подписка на изменения свойств Node
                    newNode.PropertyChanged += CurrentModel_PropertyChanged;

                    var symbolDef = _symbolLibrary.GetSymbol(newNode.SymbolId);
                    Components.Add(new NodeVm(newNode, symbolDef, GridSettings));
                }
            }

            // Автоматически удаляем NodeVm при удалении Node из модели
            if (e.OldItems != null)
            {
                foreach (Node oldNode in e.OldItems)
                {
                    // отписка от изменений свойст Node
                    oldNode.PropertyChanged -= CurrentModel_PropertyChanged;

                    // удаление NodeVm
                    var vm = Components.FirstOrDefault(c => c.Model == oldNode);
                    if (vm != null)
                    {
                        Components.Remove(vm);
                        SelectedNodes.Remove(vm);
                        vm.Dispose();
                    }
                }
            }
        }

        private void OnModelLinksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var vm in Links)
                {
                    vm.PropertyChanged -= OnLinkVmPropertyChanged;
                    vm.Dispose();
                }
                Links.Clear();
                Junctions.Clear();
                return;
            }

            // Автоматически создаём LinkVm при добавлении Link в модель
            if (e.NewItems != null)
            {
                foreach (Link newLink in e.NewItems)
                {
                    var nodeA = Components.FirstOrDefault(c => c.Model == newLink.PortA?.Owner);
                    var nodeB = Components.FirstOrDefault(c => c.Model == newLink.PortB?.Owner);
                    if (nodeA != null && nodeB != null)
                    {
                        var linkVm = new LinkVm(newLink, nodeA, nodeB, GridSettings);
                        linkVm.PropertyChanged += OnLinkVmPropertyChanged;
                        Links.Add(linkVm);
                    }
                }
            }

            // Автоматически удаляем LinkVm при удалении Link из модели (включая каскадное удаление из SysModel!)
            if (e.OldItems != null)
            {
                foreach (Link oldLink in e.OldItems)
                {
                    var linkVm = Links.FirstOrDefault(l => l.Model == oldLink);
                    if (linkVm != null)
                    {
                        linkVm.PropertyChanged -= OnLinkVmPropertyChanged;
                        linkVm.Dispose();
                        Links.Remove(linkVm);
                        
                    }
                }
            }

            UpdateJunctions();
        }

        private void OnLinkVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(LinkVm.ScreenPoints) or nameof(LinkVm.State))
            {
                UpdateJunctions();
            }
        }

        /// <summary>
        /// Заполняем свойства текущей VM (Components + Links).
        /// </summary>
        /// <param name="model"></param>
        private void RebuildVisualScene(SysModel model)
        {
            // Временный словарь для мгновенного поиска NodeVm по Id (O(1))
            var nodeVmMap = new Dictionary<Node, NodeVm>(model.Nodes.Count);

            // 1. Сначала создаём все визуальные компоненты
            foreach (var node in model.Nodes)
            {
                // Подписываемся на изменение свойств в каждом Node
                node.PropertyChanged -= CurrentModel_PropertyChanged;
                node.PropertyChanged += CurrentModel_PropertyChanged;

                // создание NodeVm
                var symbolDef = _symbolLibrary.GetSymbol(node.SymbolId);
                var nodeVm = new NodeVm(node, symbolDef, GridSettings);
                Components.Add(nodeVm);
                nodeVmMap[node] = nodeVm; // <-- Заполняем словарь для быстрого поиска node
            }

            // 2. Затем создаём провода, связывая их с созданными NodeVm
            foreach (var link in model.Links)
            {
                if (link.PortA?.Owner == null || link.PortB?.Owner == null)
                    continue;
                // Здесь мы находим nodeVM соотвествущие Node главной модели
                if (nodeVmMap.TryGetValue(link.PortA.Owner, out var nodeA) &&
                    nodeVmMap.TryGetValue(link.PortB.Owner, out var nodeB))
                {
                    var linkVm = new LinkVm(link, nodeA, nodeB, GridSettings);
                    linkVm.PropertyChanged += OnLinkVmPropertyChanged;
                    Links.Add(linkVm);
                }
            }

            UpdateJunctions();
        }

        private void ClearVisualScene()
        {
            // 1. Сначала освобождаем провода (отписываем от узлов, модели и GridSettings)
            foreach (var linkVm in Links)
            {
                linkVm.PropertyChanged -= OnLinkVmPropertyChanged;
                linkVm.Dispose();
            }
            Links.Clear();
            Junctions.Clear();

            // 2. Затем освобождаем узлы (отписываем их и их пины от GridSettings и PortModel)
            foreach (var nodeVm in Components)
            {
                nodeVm.Model.PropertyChanged -= CurrentModel_PropertyChanged;
                nodeVm.Dispose();
            }
            Components.Clear();

            // 3. Очищаем список выделения
            SelectedNodes.Clear();
        }


        // 1. Версия для готового экземпляра (для рефлексии из StockCollection)
        private NodeVm CreateNodeVmForStock(Node instance)
        {
            var symbolDef = _symbolLibrary.GetSymbol(instance.SymbolId);
            return
                new NodeVm(instance, symbolDef, GridSettings)
                { ShowDesignator = true, ShowValue = false };
        }

        // 2. Обобщённая версия для удобного вызова в коде (переиспользует первую)
        private NodeVm CreateNodeVmForStock<T>() where T : Node, new()
        {
            return CreateNodeVmForStock(Node.CreateDefaultInstance<T>());
        }

        private void LoadStockForModel()
        {
            // Fill Stock list 
            StockList.Add(CreateNodeVmForStock<GND>());

            StockList.Add(CreateNodeVmForStock<L_Gen>());

            StockList.Add(CreateNodeVmForStock<L2OR>());
            StockList.Add(CreateNodeVmForStock<L3OR>());
            StockList.Add(CreateNodeVmForStock<L4OR>());
            StockList.Add(CreateNodeVmForStock<L8OR>());

            StockList.Add(CreateNodeVmForStock<L2AND>());
            StockList.Add(CreateNodeVmForStock<L3AND>());
            StockList.Add(CreateNodeVmForStock<L4AND>());
            StockList.Add(CreateNodeVmForStock<L8AND>());

            StockList.Add(CreateNodeVmForStock<L2XOR>());
            StockList.Add(CreateNodeVmForStock<L3XOR>());
            StockList.Add(CreateNodeVmForStock<L4XOR>());
            StockList.Add(CreateNodeVmForStock<L8XOR>());

            StockList.Add(CreateNodeVmForStock<Resistor>());
            StockList.Add(CreateNodeVmForStock<Capacitor>());
            StockList.Add(CreateNodeVmForStock<Inductor>());

        }

        private void CurrentModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 1. Во время симуляции схема заблокирована от редактирования — сразу выходим (не нагружая Dispatcher)
            if (IsSimulating) return;

            // 2. Игнорируем UI-свойства и временные свойства симуляции (в случае их изменения (сброса) после остановки симуляции)
            if (e.PropertyName is "IsSelected" or "IsExpanded"
                              or nameof(NodePort.State)
                              or nameof(NodePort.HighlightStatus)
                              or nameof(Link.LinkStatus)
                              or nameof(Link.State))
            {
                return;
            }

            // Безопасность потоков (если модель изменяется из другого потока тогда перенаправляем вызов)
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => CurrentModel_PropertyChanged(sender, e));
                return;
            }

            // 3. Обработка чисто визуального изменения координат узла
            if (sender is Node movedNode && e.PropertyName == nameof(Node.Location))
            {
                var nodeVm = Components.FirstOrDefault(c => c.Model == movedNode);
                nodeVm?.UpdatePixelPosition();

                // Ставим именно VisualChanged и выходим, чтобы не перезаписать его на LogicalChanged ниже!
                if (VmStatus != ModelStatus.LogicalChanged)
                {
                    VmStatus = ModelStatus.VisualChanged;
                }
                return;
            }

            // 4. Обновляем визуальные надписи на схеме, если у Node изменились Value (например, частота L_Gen) или Designator
            if (sender is Node changedNode &&
                (e.PropertyName == nameof(Node.Value) || e.PropertyName == nameof(Node.Designator)))
            {
                var nodeVm = Components.FirstOrDefault(c => c.Model == changedNode);
                nodeVm?.RefreshLabels(); // Вызывает OnPropertyChanged(nameof(Value)) и OnPropertyChanged(nameof(Designator))
            }

            if (e.PropertyName == nameof(SysModel.Nodes))
            {
                UpdateContentBounds();
            }



            // ВРЕМЕННАЯ ОТЛАДКА: покажет в окне Output (Вывод), кто именно поменял статус модели!
            //System.Diagnostics.Debug.WriteLine($"[Модель изменена] Объект: {sender?.GetType().Name}, Свойство: {e.PropertyName}");


            VmStatus = ModelStatus.LogicalChanged;
        }






        // Публичное свойство для Статуса (чтобы UI мог например блокировать/разблокировать кнопку Сохранить)
        public ModelStatus VmStatus
        {
            get;
            private set => SetProperty(ref field, value);
        } = ModelStatus.UnChanged;



        // Свойства пути к файлу
        public string CurrentFilePath
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                {
                    OnPropertyChanged(nameof(CurrentFile)); // Уведомляем, что имя файла тоже изменилось
                }
            }
        } = string.Empty;


        // Свойство только для чтения, к которому привязан заголовок окна или текст в UI
        public string CurrentFile => string.IsNullOrEmpty(CurrentFilePath) ?
            "Новый_" + DateTime.Now.ToString("yyyyMMddHHmmss") : Path.GetFileName(CurrentFilePath);



        // Additioanal properties
        public String ContentText { get; set { SetProperty(ref field, value); } } = String.Empty;
        public String StatusText { get; set { SetProperty(ref field, value); } } = String.Empty;





        // Команды Открытия, закрытия, импорта, экспорта 
        public RelayCommand QuickLoadCommand { get; }
        public RelayCommand NewFileCommand { get; }
        public RelayCommand OpenFileCommand { get; }
        public RelayCommand SaveFileCommand { get; }
        public RelayCommand CloseFileCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ExitCommand { get; }

        // Команды работы с объектами
        public RelayCommand OptionsColorCommand { get; }
        public RelayCommand CloneNodeCommand { get; }
        public RelayCommand RemoveNodeCommand { get; }
        public RelayCommand NodePropertiesCommand { get; }

        // Команды  рабочей области
        public RelayCommand SetGridPresetCommand { get; }
        public RelayCommand SetGridStyleCommand { get; }

        public RelayCommand SetGridThemeCommand { get; }

        public RelayCommand ZoomInCommand { get; }
        public RelayCommand ZoomOutCommand { get; }
        public RelayCommand ResetZoomCommand { get; }

        // Команды редактирования
        public RelayCommand ClearSelectionCommand { get; }
        public RelayCommand CloneSelectedNodesCommand { get; }
        public RelayCommand RemoveSelectedNodesCommand { get; }

        public RelayCommand ToggleLinksActivityCommand { get; }
        public RelayCommand DeleteLinksCommand { get; }

        // Команды симуляции
        public ICommand StartCommand => field ??= new RelayCommand(_ => StartSimulation(), _ => CurrentModel != null && !IsSimulating);

        public ICommand StopCommand => field ??= new RelayCommand(async _ => await StopSimulationAsync(), _ => IsSimulating);

        public ICommand PauseCommand => field ??= new RelayCommand(param => TogglePause(param is true), _ => IsSimulating);

        // Services
        private readonly IAppSettings _appSettings;
        private readonly IDialogService _fileDialogService;
        private readonly IDataServiceFactory _serviceFactory;

        private readonly IMessageService _messageService;
        private readonly IWindowService _windowService;

        private readonly SymbolLibrary _symbolLibrary;

        // Публичное свойство — удобно биндить в XAML (например, к ComboBox выбора шага сетки)
        public GridSettings GridSettings { get; }


        // Флаг типа работы с файлами
        private bool AsyncFlag => _appSettings.IsAsyncMode;

        private readonly DispatcherTimer _autoSaveTimer;



        // Конструктор-заглушка ДЛЯ XAML DESIGNER (теперь он пустой и не ломает readonly)
#pragma warning disable CS8618 // Отключаем проверку для пустого конструктора
        public SysViewModel() { }
#pragma warning restore CS8618 // Включаем обратно

        // Основной конструктор, который вызывает DI-контейнер
        public SysViewModel(
            IAppSettings appSettings,
            IDialogService fileDialogService,
            IDataServiceFactory serviceFactory,
            IMessageService messageService,
            IWindowService windowService,
            GridSettings gridSettings,
            SymbolLibrary symbolLibrary)
        {
            // 1. Инициализируем readonly-сервисы
            _appSettings = appSettings;
            _fileDialogService = fileDialogService;
            _serviceFactory = serviceFactory;
            _messageService = messageService;
            _windowService = windowService;
            GridSettings = gridSettings;
            _symbolLibrary = symbolLibrary;

            // 2. Инициализируем команды (теперь они без проблем видят сервисы)
            QuickLoadCommand = new(OpenFileProc, CanExecuteIfNotSimulating);
            NewFileCommand = new(NewFile, CanExecuteIfNotSimulating);
            OpenFileCommand = new(OpenFile, CanExecuteIfNotSimulating);
            SaveFileCommand = new(SaveFile, CanExecuteIfExist);
            CloseFileCommand = new(CloseFile, CanExecuteIfNotSimulating);
            ImportCommand = new(ImportData, CanExecuteIfnotExist);
            ExportCommand = new(ExportData, CanExecuteIfExist);
            ExitCommand = new(Exit);
            OptionsColorCommand = new(OpenColorDialog);

            RemoveNodeCommand = new(RemoveNode, CanExecuteIfNotSimulating);
            CloneNodeCommand = new(CloneNode, CanExecuteIfNotSimulating);
            NodePropertiesCommand = new(ShowNodeProperties, CanExecuteIfNotSimulating);

            ToggleLinksActivityCommand = new(ToggleLinksActivity, CanModifySelectedLinks);
            DeleteLinksCommand = new(DeleteSelectedLinks, CanModifySelectedLinks);


            ZoomInCommand = new(_ => ZoomIn(), CanExecuteIfExist);
            ZoomOutCommand = new(_ => ZoomOut(), CanExecuteIfExist);
            ResetZoomCommand = new(_ => ResetZoom(), CanExecuteIfExist);

            ClearSelectionCommand = new RelayCommand(
                execute: _ => RequestClearSelection?.Invoke(),
                canExecute: _ => SelectedNodes.Count > 0 && _primaryDraggedNode == null); // Не сбрасываем во время активного DragDrop

            CloneSelectedNodesCommand = new RelayCommand(
                execute: _ => CloneNodes(SelectedNodes),
                canExecute: _ => !IsSimulating && SelectedNodes.Count > 0); // Запрещаем клонирование при симуляции

            RemoveSelectedNodesCommand = new RelayCommand(
                execute: _ => DeleteNodes(SelectedNodes),
                canExecute: _ => !IsSimulating && SelectedNodes.Count > 0); // Запрещаем удаление при симуляции (блокирует и Delete / Backspace)

            SetGridPresetCommand = new RelayCommand(p => { if (p is GridPreset preset) CurrentGridPreset = preset; });
            SetGridStyleCommand = new RelayCommand(p => { if (p is GridStyleMode style) CurrentGridStyle = style; });
            SetGridThemeCommand = new(SetGridTheme);

            UpdateGridBrush();

            // Настраиваем таймер автосохранения
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1) // Проверка каждую 1 минуту (можно вынести в настройки)
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start(); // Запускаем таймер при старте приложения
        }

        #region  Impementation



        // Кнопки доступны, если модель просто создана
        private bool CanExecuteIfExist(object? obj)
        {
            return CurrentModel != null;
        }

        // Если модель уже открыта, но она "Чистая" (не изменена), мы можем открыть поверх другой файл.
        // Если же статус LogicalChanged — блокируем открытие/импорт, пока пользователь не сохранит или не закроет текущий проект.
        private bool CanExecuteIfnotExist(object? obj)
        {
            //TODO Добавить в сравнение если необходимо отслеживать статус модели
            return CurrentModel == null /* || Status == ModelStatus.UnChanged */;
        }

        // Специальный предикат для кнопки "Сохранить"
        private bool CanSaveExecute(object? obj)
        {
            // Сохранить можно только если модель есть И в ней что-то поменяли!
            return CurrentModel != null && VmStatus == ModelStatus.LogicalChanged;
        }

        private bool CanExecuteIfNotSimulating(object? obj)
        {
            return !IsSimulating;
        }


        public NodeVm CreateStockNodeVm(Node stockNode)
        {
            return new NodeVm(stockNode, _symbolLibrary.GetSymbol(stockNode.SymbolId), GridSettings);
        }

        private bool _isExiting;

        public async void Exit(object? obj)
        {
            // Если мы уже прошли все проверки и закрываем окно — просто выходим
            if (_isExiting) return;

            // Если нажали крестик окна — ставим закрытие на паузу до завершения всех проверок
            if (obj is CancelEventArgs e)
            {
                e.Cancel = true;
            }

            // 1. Останавливаем симуляцию, если она запущена
            if (IsSimulating)
            {
                await StopSimulationAsync();
            }

            // 2. Проверяем несохраненные изменения
            if (!string.IsNullOrEmpty(CurrentFilePath))
            {
                bool confirm = await CanCloseCurrentModel();
                if (!confirm)
                {
                    // Пользователь нажал "Отмена" — оставляем окно открытым
                    return;
                }
            }

            // 3. Штатная очистка перед выходом
            DeleteBackupFile();
            CurrentModel = default;

            // 4. Безопасно закрываем окно в следующем тике UI-потока (после завершения Window_Closing)
            _isExiting = true;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                _windowService?.CloseCurrentWindow();
            });
        }


        private void OpenColorDialog(object? obj)
        {
            Color? wpfColor = _fileDialogService.OpenColorDialog();
            if (wpfColor != null)
            {
                CurrentGridBrush = new SolidColorBrush((Color)wpfColor);
            }
        }

        #endregion



        #region Расчет точек соединения
        /// <summary>
        /// Пересчитывает все точки электрического соединения (узлы ветвления) на схеме.
        /// </summary>
        public void UpdateJunctions()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateJunctions);
                return;
            }

            Junctions.Clear();
            if (Links.Count < 2)
                return;

            const double eps = 1.5;
            var foundNodes = new List<(Point Pt, PortState State)>();

            void AddUniqueJunction(Point pt, PortState state)
            {
                if (!foundNodes.Any(existing => Math.Abs(existing.Pt.X - pt.X) < eps && Math.Abs(existing.Pt.Y - pt.Y) < eps))
                {
                    foundNodes.Add((pt, state));
                }
            }

            // 1. Для каждой пары проводов, начинающихся или заканчивающихся на одном пине,
            // находим последнюю общую точку их маршрутов перед тем, как линии разойдутся в разные стороны!
            for (int i = 0; i < Links.Count; i++)
            {
                for (int j = i + 1; j < Links.Count; j++)
                {
                    var ptsA = Links[i].ScreenPoints;
                    var ptsB = Links[j].ScreenPoints;
                    if (ptsA.Count < 2 || ptsB.Count < 2) continue;

                    // Проверяем все 4 комбинации стыковки концов (Start-Start, Start-End, End-Start, End-End)
                    TryFindDivergencePoint(ptsA, reverseA: false, ptsB, reverseB: false, Links[i].State);
                    TryFindDivergencePoint(ptsA, reverseA: false, ptsB, reverseB: true, Links[i].State);
                    TryFindDivergencePoint(ptsA, reverseA: true, ptsB, reverseB: false, Links[i].State);
                    TryFindDivergencePoint(ptsA, reverseA: true, ptsB, reverseB: true, Links[i].State);
                }
            }

            void TryFindDivergencePoint(PointCollection a, bool reverseA, PointCollection b, bool reverseB, PortState state)
            {
                Point startA = reverseA ? a[^1] : a[0];
                Point startB = reverseB ? b[^1] : b[0];

                // Если провода не выходят из одного и того же пина — пропускаем
                if ((startA - startB).Length >= eps)
                    return;

                // Строим упорядоченные списки точек от общего пина наружу
                var pathA = reverseA ? a.Reverse().ToList() : a.ToList();
                var pathB = reverseB ? b.Reverse().ToList() : b.ToList();

                Point lastCommon = pathA[0];
                int minCount = Math.Min(pathA.Count, pathB.Count);

                for (int k = 1; k < minCount; k++)
                {
                    // Пока вершины полностью совпадают — двигаемся вдоль общего участка
                    if ((pathA[k] - pathB[k]).Length < eps)
                    {
                        lastCommon = pathA[k];
                        continue;
                    }

                    // Если один сегмент короче другого, но лежит на той же прямой (как отрезок G2 -> красная стрелка
                    // внутри длинного горизонтального отрезка G2 -> D5!), то точка расхождения — конец более короткого сегмента!
                    if (IsPointStrictlyOnSegment(pathA[k], lastCommon, pathB[k], eps))
                    {
                        lastCommon = pathA[k];
                    }
                    else if (IsPointStrictlyOnSegment(pathB[k], lastCommon, pathA[k], eps))
                    {
                        lastCommon = pathB[k];
                    }
                    break;
                }

                AddUniqueJunction(lastCommon, state);
            }

            // 2. Дополнительно проверяем классические Т-стыки (конец одного провода упирается в середину отрезка другого)
            foreach (var linkA in Links)
            {
                if (linkA.ScreenPoints.Count < 2) continue;
                Point[] ends = [linkA.ScreenPoints[0], linkA.ScreenPoints[^1]];

                foreach (var ep in ends)
                {
                    foreach (var linkB in Links)
                    {
                        if (linkA == linkB) continue;
                        var pts = linkB.ScreenPoints;
                        for (int k = 0; k < pts.Count - 1; k++)
                        {
                            if (IsPointStrictlyOnSegment(ep, pts[k], pts[k + 1], eps))
                            {
                                AddUniqueJunction(ep, linkA.State);
                            }
                        }
                    }
                }
            }

            foreach (var (pt, state) in foundNodes)
            {
                Junctions.Add(new JunctionVm(pt.X, pt.Y, state));
            }
        }


        private static bool IsPointStrictlyOnSegment(Point p, Point a, Point b, double eps)
        {
            // Исключаем сами концы отрезка (они уже учтены в п. 1)
            if ((p - a).Length < eps || (p - b).Length < eps)
                return false;

            double segLen = (b - a).Length;
            if (segLen < eps)
                return false;

            // Расстояние от точки p до прямой ab и проверка попадания внутрь проекции
            double cross = Math.Abs((p.X - a.X) * (b.Y - a.Y) - (p.Y - a.Y) * (b.X - a.X)) / segLen;
            if (cross > eps)
                return false;

            double dot = (p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y);
            return dot > eps && dot < segLen * segLen - eps;
        }

        #endregion
    }
}

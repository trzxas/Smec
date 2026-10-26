using Emulation.Models;
using Emulation.Services;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Color = System.Windows.Media.Color;
using DragDropEffects = System.Windows.DragDropEffects;
using Point = System.Windows.Point;
//using CommunityToolkit.Mvvm.Input;

namespace Emulation.ViewModels
{

    public partial class SysViewModel : Base
    {
        //Const

        // Project file extension
        const string ProjExtension = ".prj";
        const string BackupExtension = ".back";
        const string defaultFilter = $"Project(*{ProjExtension})|*{ProjExtension}|Все файлы(*.*)|*.*";




        // Главное свойство модели
        public SysModel? CurrentModel
        {
            get; set
            {
                if (field == value) return; // Оптимизация

                // 1. Отписываемся от старой модели целиком
                field?.PropertyChanged -= CurrentModel_PropertyChanged;

                // 2. Присвоение нового значения
                field = value;

                // 3. Логика для нового значения
                if (field == null)
                {
                    CurrentFilePath = string.Empty;
                }
                else
                {
                    // Подписываемся на новую модель целиком
                    field.PropertyChanged += CurrentModel_PropertyChanged;
                }

                Status = ModelStatus.UnChanged;
                OnPropertyChanged();
            }
        } = null;

        // Абсолютно любое изменение в любой точке модели теперь вызовет данный метод
        private void CurrentModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Безопасность потоков
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => CurrentModel_PropertyChanged(sender, e));
                return;
            }

            // Игнорируем UI-свойства, если они прописаны в исключениях
            if (e.PropertyName == "IsSelected" || e.PropertyName == "IsExpanded") return;

            Status = ModelStatus.LogicalChanged;
        }


        private ModelStatus _status = ModelStatus.UnChanged;
        // Публичное свойство для Статуса (чтобы UI мог например блокировать/разблокировать кнопку Сохранить)
        public ModelStatus Status
        {
            get => _status;
            private set // private set, чтобы статус менялся только внутри этой ViewModel
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        // Свойства пути к файлу
        public string CurrentFilePath
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFile)); // Уведомляем, что имя файла тоже изменилось
            }
        } = string.Empty;

        // Свойство только для чтения, к которому привязан заголовок окна или текст в UI
        public string CurrentFile => string.IsNullOrEmpty(CurrentFilePath) ?
            "Новый_" + DateTime.Now.ToString("yyyyMMddHHmmss") : Path.GetFileName(CurrentFilePath);



        // Additioanal properties
        public String ContentText { get; set { SetProperty(ref field, value); } } = String.Empty;
        public String StatusText { get; set { SetProperty(ref field, value); } } = String.Empty;

        // Настройки пользователя
        public SolidColorBrush CurrentGridBrush { get; set { SetProperty(ref field, value); } } = new(Color.FromRgb(0xe0, 0xe0, 0xe0));
        public SolidColorBrush CurrentGridBackground { get; set { SetProperty(ref field, value); } } = new(Color.FromRgb(255, 255, 240));



        // Command
        public RelayCommand NewFileCommand { get; }
        public RelayCommand OpenFileCommand { get; }
        public RelayCommand SaveFileCommand { get; }
        public RelayCommand CloseFileCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ExitCommand { get; }

        public RelayCommand OptionsCommand { get; }
        public RelayCommand CloneNodeCommand { get; }
        public RelayCommand RemoveNodeCommand { get; }

        // Services
        private readonly IAppSettings _appSettings;
        private readonly IDialogService _fileDialogService;
        private readonly IDataServiceFactory _serviceFactory;

        private readonly IMessageService _messageService;
        private readonly IWindowService _windowService;


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
            IWindowService windowService)
        {
            // 1. Инициализируем readonly-сервисы
            _appSettings = appSettings;
            _fileDialogService = fileDialogService;
            _serviceFactory = serviceFactory;
            _messageService = messageService;
            _windowService = windowService;

            // 2. Инициализируем команды (теперь они без проблем видят сервисы)
            NewFileCommand = new(NewFile);
            OpenFileCommand = new(OpenFile);
            SaveFileCommand = new(SaveFile, CanExecuteIfExist);
            CloseFileCommand = new(CloseFile);
            ImportCommand = new(ImportData, CanExecuteIfnotExist);
            ExportCommand = new(ExportData, CanExecuteIfExist);
            ExitCommand = new(Exit);
            OptionsCommand = new(OpenColorDialog);
            RemoveNodeCommand = new(RemoveNode);
            CloneNodeCommand = new(CloneNode);

            // Настраиваем таймер автосохранения
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1) // Проверка каждую 1 минуту (можно вынести в настройки)
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start(); // Запускаем таймер при старте приложения
        }

        #region  Impementation

        // Путь Автосохраненного файла проекта
        private string BackupFile => String.IsNullOrEmpty(CurrentFilePath) ?
                   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{CurrentFile}{BackupExtension}") : $"{CurrentFilePath}.back";
        private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            // Если модели нет или она НЕ изменялась — сохранять нечего, просто выходим
            if (CurrentModel == null || Status == ModelStatus.UnChanged || CurrentFilePath == String.Empty)
                return;

            try
            {
                // Временно останавливаем таймер, чтобы новые тики не накладывались, если сохранение затянется
                _autoSaveTimer.Stop();
                StatusText = "Выполняется автоматическое сохранение резервной копии...";

                var defaultDataService = _serviceFactory.GetStorageService();
                var fileData = CreateProjectFile(); // Собирает SysModel + ViewSettings

                await defaultDataService.SaveToFileAsync(BackupFile, fileData);

                StatusText = $"Резервная копия автоматически сохранена в {DateTime.Now:HH:mm:ss}";

                // ВАЖНО: Мы НЕ сбрасываем статус модели в UnChanged! 
                // Пользователь всё еще должен сам осознанно нажать кнопку "Сохранить", чтобы обновить свой рабочий файл.
                // Но мы фиксируем, что бэкап актуален.
            }

            catch (Exception ex)
            {
                // Не показываем жесткий MessageBox при автосохранении, чтобы не пугать и не прерывать пользователя,
                // просто пишем ошибку в строку статуса или лог
                StatusText = $"Ошибка автосохранения: {ex.Message}";
            }
            finally
            {
                // В любом случае запускаем таймер обратно для следующей проверки
                _autoSaveTimer.Start();
            }
        }



        private async void NewFile(object? obj)
        {
            // Чекаємо перевірки. Якщо користувач натиснув "Скасувати", просто виходимо
            if (!await CanCloseCurrentModel()) return;

            // Створюємо чисту модель
            CurrentModel = new SysModel();
            CurrentFilePath = string.Empty;
            Status = ModelStatus.UnChanged; // Модель щойно створена, вона чиста

            CurrentModel.FillRandom();
        }

        #region Основное сохранение проекта (Save SaveAs)

        private async void SaveFile(object? obj)
        {
            // Определяем, было ли нажато "Сохранить как". 
            // Если параметр — строка из XAML (например, "SaveAs") или true:
            bool isSaveAs = obj is true || (obj?.ToString() == "SaveAs");

            await SaveFileUnionAsync(isAsync: AsyncFlag, isSaveAs: isSaveAs);

        }

        // Объединенный метод. По умолчанию работает асинхронно.
        private async Task<bool> SaveFileUnionAsync(bool isAsync = true, bool isSaveAs = false)
        {
            if (CurrentModel == null) return false;

            // Запрашиваем сервис для ProjectFile
            var defaultDataService = _serviceFactory.GetStorageService();

            // если это "Сохранить как" или имени файла еще нет то ВСЕГДА открываем диалог, 
            // игнорируя существующий CurrentFilePath
            string? filePath = (isSaveAs || string.IsNullOrEmpty(CurrentFilePath))
                ? _fileDialogService.SaveFileDialog(ProjExtension, defaultFilter)
                : CurrentFilePath;

            if (string.IsNullOrEmpty(filePath)) return false;

            try
            {
                StatusText = isAsync ? "Асинхронное сохранение..." : "Синхронное сохранение...";
                var fileData = CreateProjectFile(); // Собирает SysModel + ViewSettings

                if (isAsync)
                    await defaultDataService.SaveToFileAsync(filePath, fileData);
                else
                    defaultDataService.SaveToFile(filePath, fileData);

                // ПОСЛЕ успешного сохранения ВСЕГДА обновляем текущий путь на новый
                CurrentFilePath = filePath;
                StatusText = $"Проект успешно сохранен в {Path.GetFileName(filePath)}";
                Status = ModelStatus.UnChanged;

                //_messageService.ShowInfo("Файл успешно сохранен!", "Успех");
                return true;
            }
            catch (Exception ex)
            {
                HandleSaveException(ex); // Метод обработки ошибок
                return false;
            }
        }
        // Шаблон для сборки ProjectFile
        private ProjectFile CreateProjectFile()
        {
            return new ProjectFile
            {
                Model = this.CurrentModel!,
                ViewSettings = new Dictionary<string, string>
                {
                    { "GridColor", CurrentGridBrush.Color.ToString() },
                    { "GridBackground", CurrentGridBackground.Color.ToString()}
                }
            };
        }

        // Обработка ошибок при сохранении файла
        private void HandleSaveException(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
            {
                StatusText = "Ошибка доступа";
                _messageService.ShowError("Отказано в доступе. Нет прав для записи в эту папку.", "Ошибка сохранения");
            }
            else if (ex is IOException ioEx && ioEx.HResult == -2147024784)
            {
                StatusText = "Недостаточно места";
                _messageService.ShowError("На устройстве недостаточно свободного места.", "Ошибка сохранения");
            }
            else
            {
                StatusText = "Ошибка сохранения";
                _messageService.ShowError($"Ошибка при записи на диск: {ex.Message}", "Ошибка сохранения");
            }
        }
        #endregion

        #region Загрузка данных из файла (Open)
        private async void OpenFile(object? obj)
        {
            if (!await CanCloseCurrentModel()) return;

            // 1. Запрашиваем сервис для структуры проекта (ProjectFile) из фабрики
            var storageService = _serviceFactory.GetStorageService();

            // Запрашиваем путь через сервис (View), используя фильтр сервиса формата проекта
            string? filePath = _fileDialogService.OpenFileDialog(defaultFilter);

            if (string.IsNullOrEmpty(filePath)) return; // Выходим, если пользователь отменил диалог

            // ВЫЧИСЛЯЕМ ПУТЬ К БЭКАПУ ДЛЯ ЭТОГО ФАЙЛА (Локально, ДО загрузки!)
            // Повторяем логику вашего вычисляемого свойства BackupFile:
            string potentialBackupPath = $"{filePath}{BackupExtension}";

            // Если файл автосохранения существует — значит, приложение в прошлый раз закрылось некорректно
            if (File.Exists(potentialBackupPath))
            {
                // Запрашиваем подтверждение у пользователя через наш MessageService
                MessageResult recover = _messageService.ShowYesNo(
                    "Обнаружена автоматически сохраненная резервная копия после некорректного завершения программы. Восстановить последнюю сессию?",
                    "Восстановление данных"
                );

                if (recover == MessageResult.Yes)
                {
                    // Если пользователь согласился — подменяем путь загрузки на файл бэкапа!
                    filePath = potentialBackupPath;
                }
                else
                {
                    // Если отказался — удаляем бэкап, чтобы он не мешал в будущем
                    try { File.Delete(potentialBackupPath); } catch { }
                }
            }

            // 5. ОДНОКРАТНАЯ ЗАГРУЗКА (Считает либо оригинал, либо бэкап, в зависимости от предыдущего шага)
            try
            {
                StatusText = AsyncFlag ? "Асинхронная загрузка..." : "Синхронная загрузка...";
                ProjectFile? projectData = null;



                // 2. Ветвление в зависимости от флага (используем полученный storageService)
                if (AsyncFlag)
                {
                    projectData = await storageService.LoadFromFileAsync(filePath);
                }
                else
                {
                    // Запускаем синхронный метод сервиса
                    projectData = storageService.LoadFromFile(filePath);
                }

                // 3. Если данные успешно прочитаны — обновляем UI
                if (projectData != null)
                {
                    // Восстанавливаем связи для внутренней модели проекта после чтения BSON/JSON
                    projectData.Model.RestoreRelations();

                    CurrentModel = projectData.Model;

                    // ВАЖНО: Если мы загрузили файл бэкапа (*.back), оригинальный путь проекта 
                    // должен очиститься от ".back", чтобы рабочий CurrentFilePath указывал на оригинал!
                    CurrentFilePath = filePath.EndsWith(BackupExtension) ? filePath.Replace(BackupExtension, String.Empty) : filePath;

                    // Вызываем единый метод восстановления настроек интерфейса
                    RestoreViewSettings(projectData.ViewSettings);

                    // Если загрузили бэкап — модель изменена (LogicalChanged), если оригинал — чистая (UnChanged)
                    Status = filePath.EndsWith(BackupExtension) ? ModelStatus.LogicalChanged : ModelStatus.UnChanged;

                    StatusText = $"File {Path.GetFileName(filePath)} loaded successfully";
                }
                else
                {
                    StatusText = "Ошибка: Файл пуст или имеет неверный формат";
                }
            }
            catch (Exception ex)
            {
                HandleLoadException(ex); // Наш единый метод обработки ошибок загрузки
            }

        }

        private void RestoreViewSettings(Dictionary<string, string>? viewSettings)
        {
            // Если настроек в файле вообще нет (например, старая версия файла) — просто выходим
            if (viewSettings == null) return;

            // 1. Восстановление цвета сетки
            if (viewSettings.TryGetValue("GridColor", out var colorHex) && !string.IsNullOrEmpty(colorHex))
            {
                try
                {
                    var convertedColor = System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                    if (convertedColor is System.Windows.Media.Color parsedColor)
                    {
                        CurrentGridBrush = new System.Windows.Media.SolidColorBrush(parsedColor);
                    }
                }
                catch (FormatException)
                {
                    System.Diagnostics.Debug.WriteLine($"Некорректный формат цвета в файле: {colorHex}");
                }
            }

            // Восстановление цвета фона
            if (viewSettings.TryGetValue("GridBackground", out var strBackground))
            {
                try
                {
                    var convertedColor = System.Windows.Media.ColorConverter.ConvertFromString(strBackground);
                    if (convertedColor is Color parsedBackgroundColor)
                    {
                        CurrentGridBackground = new SolidColorBrush(parsedBackgroundColor);
                    }
                }
                catch (FormatException)
                {
                    System.Diagnostics.Debug.WriteLine($"Некорректный формат цвета в файле: {strBackground}");
                }
            }


            // 3. Пример: Восстановление видимости сетки
            if (viewSettings.TryGetValue("ShowGrid", out var showGridStr) && bool.TryParse(showGridStr, out var showGrid))
            {
                // IsGridVisible = showGrid;
            }
        }


        // Обработка ошибок при загрузке файла
        private void HandleLoadException(Exception ex)
        {
            if (ex is FileNotFoundException)
            {
                StatusText = "Ошибка: Файл не найден. Возможно, он был удален.";
                _messageService.ShowError(StatusText, "Ошибка открытия файла");
            }
            else if (ex is UnauthorizedAccessException)
            {
                StatusText = "Ошибка: Нет доступа к файлу (проверьте права администратора).";
                _messageService.ShowError(StatusText, "Ошибка доступа");
            }
            // Додаємо перевірку на пошкоджений XML, BSON або JSON файл
            else if (
                     // для XML
                     // Когда XmlSerializer при чтении файла натыкается на поврежденную XML-структуру(например, незакрытый тег,
                     // отсутствие корневого элемента или случайные бинарные символы), он никогда не выбрасывает XmlException напрямую.
                     // Вместо этого он перехватывает ошибку парсинга внутри себя, оборачивает её в исключение более высокого
                     // уровня — InvalidOperationException(с общим текстом вроде "There is an error in XML document (1, 5)"),
                     // а исходную причину прячет глубоко внутрь во свойство InnerException.
                     // Поэтому чтобы поймать ошибку именно как ошибку данных XML  то и вводим доп. второе условие проверки
                     // Иначе ошибка поврежденного файла «упадет» в самый финальный блок else
                     // как «Критическая ошибка: There is an error in XML document...». 
                     ex is XmlException ||
                    (ex is InvalidOperationException && ex.InnerException is XmlException) ||

                     ex is System.Text.Json.JsonException || // <- для JSON

                     ex is MongoDB.Bson.BsonSerializationException || // <- для BSON
                     ex is FormatException)
            {
                StatusText = "Ошибка: Файл поврежден или имеет неверный формат данных.";
                _messageService.ShowError(StatusText, "Ошибка формата данных");
            }
            else
            {
                StatusText = $"Ошибка загрузки файла: {ex.Message}";
                _messageService.ShowError(StatusText, "Критическая ошибка");
            }
        }

        #endregion


        #region Импорт и Экспорт чистой SysModel (XML, JSON)

        // Вызывается по "Экспорт"
        private async void ExportData(object? obj)
        {
            if (CurrentModel == null) return;

            // Получаем фильтр типов файлов с доступным сервисом
            string exportFilter = _serviceFactory.GetExportFilter();

            // В качестве расширения по умолчанию (.xml) можно передавать пустую строку 
            // или вытянуть первое доступное расширение из фабрики, если это необходимо.
            string defaultExt = string.Empty;

            // Показываем диалог (задача UI)
            string? filePath = _fileDialogService.SaveFileDialog(defaultExt, exportFilter);

            if (string.IsNullOrEmpty(filePath)) return; // Отмена пользователем

            // Ждем завершения выполнения. Если режим синхронный — поток UI заблокируется внутри,
            // но этот await гарантирует безопасность выполнения без дедлоков.
            await ExecuteExport(filePath);
        }
        private async Task ExecuteExport(string filePath)
        {
            try
            {
                // Фабрика определяет сервис экпорта по расширению
                var exportService = _serviceFactory.GetExportService(filePath);
                string extension = Path.GetExtension(filePath).ToUpper();

                StatusText = AsyncFlag ? $"Экспорт модели в {extension}..." : "Синхронный экспорт...";

                if (AsyncFlag)
                    await exportService.SaveToFileAsync(filePath, CurrentModel!);
                else
                    exportService.SaveToFile(filePath, CurrentModel!);

                StatusText = $"Модель успешно экспортирована в {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                HandleSaveException(ex);
            }
        }


        // Вызывается по  "Импорт"
        private async void ImportData(object? obj)
        {
            // Динамический фильтр на основе зарегистрированных в фабрике сервисов
            string importFilter = _serviceFactory.GetImportFilter();
            string? filePath = _fileDialogService.OpenFileDialog(importFilter);

            if (string.IsNullOrEmpty(filePath)) return; // Отмена пользователем

            // Ждем завершения выполнения бизнес-логики импорта
            await ExecuteImport(filePath);
        }
        private async Task ExecuteImport(string filePath)
        {
            try
            {
                // Фабрика сама поймет по расширению (.xml/.json), какой сервис импорта вернуть
                var importService = _serviceFactory.GetImportService(filePath);

                StatusText = AsyncFlag ? "Асинхронный импорт модели..." : "Синхронный импорт модели...";
                SysModel? importedModel = null;

                if (AsyncFlag)
                    importedModel = await importService.LoadFromFileAsync(filePath);
                else
                    importedModel = importService.LoadFromFile(filePath);

                if (importedModel != null)
                {
                    // ЖЕЛЕЗНЫЙ ВЫЗОВ: Файл прочитан, восстанавливаем связи портов вручную!
                    importedModel.RestoreRelations();

                    // Присваиваем импортированную модель.
                    // Визуальные настройки (ViewSettings) остаются текущими, так как их не было в файле.
                    CurrentModel = importedModel;

                    // СБРАСЫВАЕМ рабочий путь. При клике на "Сохранить" система попросит создать .prj файл
                    CurrentFilePath = String.Empty;
                    _status = ModelStatus.UnChanged;
                    StatusText = $"Модель из файла {Path.GetFileName(filePath)} успешно импортирована";
                }
            }
            catch (Exception ex)
            {
                HandleLoadException(ex);
            }
        }
        #endregion


        private async void CloseFile(object? obj)
        {
            //TODO Включить защиту от потери файла при закрытии
#if !DEBUG
    if (!await CanCloseCurrentModel()) return;
#endif

            // При штатном закрытии файла удаляем файл бекапа
            DeleteBackupFile();

            CurrentModel = null; // Повністю обнуляємо модель, блок set у CurrentModel сам очистить шлях
        }

        private void DeleteBackupFile()
        {
            if (File.Exists(BackupFile))
            {
                try { File.Delete(BackupFile); } catch { }
            }
        }

        private async Task<bool> CanCloseCurrentModel()
        {
            // Якщо моделі немає або вона не змінювалася, її можна закривати без питань
            if (CurrentModel == null || Status == ModelStatus.UnChanged)
                return true;

            // Запитуємо користувача через сервіс повідомлень
            var result = _messageService.ShowYesNoCancel(
                "У вас есть несохраненные изменения. Сохранить проект перед продолжением?",
                "Предупреждение"
            );

            if (result == MessageResult.Cancel)
            {
                return false; // Користувач скасував дію, нічого не робимо
            }

            if (result == MessageResult.Yes)
            {
                // Викликаємо збереження файлу. Якщо користувач у діалозі збереження натиснув "Скасувати", 
                // метод SaveFile повинен повернути false, і ми перериваємо процес.
                bool isSaved = await SaveFileUnionAsync(isAsync: AsyncFlag, isSaveAs: false);
                return isSaved;
            }

            // Якщо користувач вибрав "No", то зміни ігноруються, повертаємо true (можна закривати)
            return true;
        }


        // Кнопки (например, Экспорт) доступны, если модель просто создана
        private bool CanExecuteIfExist(object? obj)
        {
            return CurrentModel != null;
        }

        // Если модель уже открыта, но она "Чистая" (не изменена), мы можем открыть поверх другой файл.
        // Если же статус LogicalChanged — блокируем открытие/импорт, пока пользователь не сохранит или не закроет текущий проект.
        private bool CanExecuteIfnotExist(object? obj)
        {
            //TODO Добавить в сравнение если необходимо отслеживать статус модели
            // || Status == ModelStatus.UnChanged;
            return CurrentModel == null;
        }

        // Специальный предикат для кнопки "Сохранить"
        private bool CanSaveExecute(object? obj)
        {
            // Сохранить можно только если модель есть И в ней что-то поменяли!
            return CurrentModel != null && Status == ModelStatus.LogicalChanged;
        }



        // Тимчасове сховище для фіксації нових координат, які прийшли з події Drop
        private Point _lastDropPoint;
        private Node? _originalNode;

        public void StartDrag(Node node)
        {
            _originalNode = node;
        }

        /// <summary>
        /// Метод викликається з View (MainWindow.xaml.cs) після завершення системного DragDrop
        /// </summary>
        /// <param name="result">Фінальний ефект (Copy, Move або None)</param>
        /// <param name="originalLocation">начально положение Node который участвует в Drag&Drop </param>
        public void CompleteDragDrop(DragDropEffects result, Point originalLocation)
        {
            if (_originalNode == null) return;

            switch (result)
            {
                // Перемещение
                case DragDropEffects.Move:
                    // Просто атомарно оновлюємо координати існуючого вузла
                    _originalNode.Location = _lastDropPoint;
                    // Обновляем статус модели
                    _status = ModelStatus.VisualChanged;
                    break;
                // Копирование
                case DragDropEffects.Copy:
                    _originalNode.Location = originalLocation;
                    Node clonedNode = new()
                    {
                        Name = "_" + _originalNode.Name,
                        // Встановлюємо нові координати, які ми зберегли під час Drop на Canvas
                        Location = _lastDropPoint
                    };

                    // Додаємо новий вузол у колекцію, він автоматично з'явиться на екрані
                    // статус модели также автоматически измениться по событию изменения коллекции Nodes
                    CurrentModel?.Nodes?.Add(clonedNode);
                    break;

                // Якщо користувач скасував перетягування (відпустив мишу не над Canvas або натиснув Esc)
                case DragDropEffects.None:
                    return;
            }

        }

        /// <summary>
        /// Допоміжний метод, щоб передати точку з Canvas у ViewModel
        /// </summary>
        public void SetDropPoint(Point point, DragDropEffects dragDropEffects, Point origin)
        {
            _lastDropPoint = point;

            if (dragDropEffects == DragDropEffects.Move)
                _originalNode?.Location = _lastDropPoint;
            else
                _originalNode?.Location = origin;
        }

        //public void OnResizeNodeControl(Node changedNode, double width)
        //{
        //    changedNode.RightSideWidth = width;
        //}

        private void RemoveNode(object? obj)
        {
            if (obj is not Node node) return;
            CurrentModel?.Nodes.Remove(node);
        }

        private void CloneNode(object? obj)
        {
            if (obj is not Node node) return;
            if (node.Clone() is Node cloneNode)
                CurrentModel?.Nodes.Add(cloneNode);
        }

        public async void Exit(object? obj)
        {
            //TODO Временно для тестирования!
            if (CurrentFilePath != String.Empty)
            {
                bool confirm = await CanCloseCurrentModel();
                if (!confirm)
                {
                    // Если пришло событие закрытия — отменяем его
                    if (obj is CancelEventArgs e)
                    {
                        e.Cancel = true;
                    }
                    // И в зависимости от параметра е (измененного здесь или нет) метод главного окна или закроет его или нет
                    return;
                }
            }
            // ШТАТНЫЙ ВЫХОД: раз пользователь сохранил или отклонил изменения,
            // удаляем файл бэкапа, чтобы при следующем запуске не всплывало окно Recovery
            DeleteBackupFile();

            // КРИТИЧНО: Перед закриттям обнуляємо модель, 
            // щоб спрацював наш сеттер і відписався від усіх подій (Nodes.CollectionChanged)
            CurrentModel = default;

            // Если это была команда из меню (obj == null) — закрываем через сервис
            if (obj is not CancelEventArgs)
            {
                _windowService?.CloseCurrentWindow();
            }

        }

        private void OpenColorDialog(object? obj)
        {
            Color? wpfColor=_fileDialogService.OpenColorDialog();
            if (wpfColor!=null)
            {
                CurrentGridBrush = new SolidColorBrush((Color)wpfColor);
            }
        }

        #endregion

    }
}

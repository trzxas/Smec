using Smec.Models;
using Smec.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Xml;
using Color = System.Windows.Media.Color;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
        private async void NewFile(object? obj)
        {
            // Чекаємо перевірки. Якщо користувач натиснув "Скасувати", просто виходимо
            if (!await CanCloseCurrentModel()) return;

            // Створюємо чисту модель
            CurrentModel = new SysModel();
            CurrentFilePath = string.Empty;
            VmStatus = ModelStatus.UnChanged; // Модель щойно створена, вона чиста

            LoadStockForModel(); // <-- Наполняем список свежими экземплярами для новой модели в CurrentModel setter
            CurrentModel.SeedModel();
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
                VmStatus = ModelStatus.UnChanged;

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
            Collection<string> stockCollection= [];
            //Заполняем коллекцию названиями классов, а не самими объектами
            foreach (var item in StockList)
            {
                stockCollection.Add(item.Model.GetType().FullName!); 
                // или если модели не в текущей сборке , то можно использовать AssemblyQualifiedName
            }

            return new ProjectFile
            {
                Model = CurrentModel!,
                StockCollection=stockCollection,
                ViewSettings = new Dictionary<string, string>
                {
                    { "GridColor", CurrentGridBrush.Color.ToString() },
                    { "GridBackground", CurrentGridBackground.Color.ToString() },
                    { "HighlightLinks", HighlightLinks.ToString() }
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
            OpenFileProc(filePath);
        }

        private async void OpenFileProc(object? obj)
        {
            if (obj is string filePath)
            {
                // 1. Запрашиваем сервис для структуры проекта (ProjectFile) из фабрики
                var storageService = _serviceFactory.GetStorageService();

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

                        // Восстанавливаем список доступных элементов
                        //TODO в будущем нужно сделать восстанавление группы элементов
                        foreach (var typeName in projectData.StockCollection)
                        {
                            Type? type = Type.GetType(typeName);

                            if (type != null && Node.CreateDefault(type) is Node defaultNode)
                            {
                                StockList.Add(CreateNodeVmForStock(defaultNode));
                            }
                            else
                            {
                                // UseLogger
                            }
                        }

                        // ВАЖНО: Если мы загрузили файл бэкапа (*.back), оригинальный путь проекта 
                        // должен очиститься от ".back", чтобы рабочий CurrentFilePath указывал на оригинал!
                        CurrentFilePath = filePath.EndsWith(BackupExtension) ? filePath.Replace(BackupExtension, String.Empty) : filePath;

                        // Вызываем единый метод восстановления настроек интерфейса
                        RestoreViewSettings(projectData.ViewSettings);

                        // Если загрузили бэкап — модель изменена (LogicalChanged), если оригинал — чистая (UnChanged)
                        VmStatus = filePath.EndsWith(BackupExtension) ? ModelStatus.LogicalChanged : ModelStatus.UnChanged;

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

            // 3. Восстановление флага подсветки связей при симуляции
            if (viewSettings.TryGetValue(nameof(HighlightLinks), out var strHighlightLinks) && bool.TryParse(strHighlightLinks, out var highlightLinks))
            {
                HighlightLinks= highlightLinks;
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

        private async Task<bool> CanCloseCurrentModel()
        {
            // Якщо моделі немає або вона не змінювалася, її можна закривати без питань
            if (CurrentModel == null || VmStatus == ModelStatus.UnChanged)
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

        #endregion

    }
}

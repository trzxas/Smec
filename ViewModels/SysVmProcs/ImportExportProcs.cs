using Smec.Models;
using System.IO;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
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
                    VmStatus = ModelStatus.UnChanged;
                    StatusText = $"Модель из файла {Path.GetFileName(filePath)} успешно импортирована";
                }
            }
            catch (Exception ex)
            {
                HandleLoadException(ex);
            }
        }
        #endregion

    }
}

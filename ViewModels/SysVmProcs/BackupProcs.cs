using Smec.Models;
using System.IO;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
        // Путь Автосохраненного файла проекта
        private string BackupFile => String.IsNullOrEmpty(CurrentFilePath) ?
                   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{CurrentFile}{BackupExtension}") : $"{CurrentFilePath}.back";
        private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            // Если модели нет или она НЕ изменялась — сохранять нечего, просто выходим
            if (CurrentModel == null || VmStatus == ModelStatus.UnChanged || CurrentFilePath == String.Empty)
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


        private void DeleteBackupFile()
        {
            if (File.Exists(BackupFile))
            {
                try { File.Delete(BackupFile); } catch { }
            }
        }
    }
}

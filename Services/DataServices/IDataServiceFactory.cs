using System;
using System.Collections.Generic;
using System.Text;

namespace Emulation.Services
{
    using Emulation.Models;
    using Emulation.Services.DataServices;
    using Emulation.ViewModels;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.IO;


    public interface IDataServiceFactory
    {

        string GetImportFilter();
        string GetExportFilter();

        IDataService<ProjectFile> GetStorageService();

        // Сервисы импорта и экспорта
        IDataService<SysModel> GetExportService(string filePath);
        IDataService<SysModel> GetImportService(string filePath);


    }

    public class DataServiceFactory : IDataServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        // Список всех доступных сервисов обмена для SysModel
        private readonly IEnumerable<IDataService<SysModel>> _exchangeServices;

        public DataServiceFactory(IServiceProvider serviceProvider, 
            IEnumerable<IDataService<SysModel>> exchangeServices)
        {
            _serviceProvider = serviceProvider;
            _exchangeServices = exchangeServices;
        }

        // Автоматически находит в контейнере сервис, привязанный к интерфейсу IDataService<ProjectFile>
        public IDataService<ProjectFile> GetStorageService()
            => _serviceProvider.GetRequiredService<IDataService<ProjectFile>>();

        // Фильтр для импорта (только CanImport == true)
        public string GetImportFilter()
        {
            var importable = _exchangeServices.Where(s => s.CanImport);
            return BuildFilterString(importable);
        }

        // Фильтр для экспорта (только CanExport == true)
        public string GetExportFilter()
        {
            var exportable = _exchangeServices.Where(s => s.CanExport);
            return BuildFilterString(exportable);
        }

        // Поиск сервиса для экспорта
        public IDataService<SysModel> GetExportService(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            var service = _exchangeServices.FirstOrDefault(s => s.DefaultExtension.ToLower() == ext && s.CanExport);

            return service ?? throw new InvalidOperationException($"Формат {ext} не поддерживает Экспорт");
        }

        // Поиск сервиса для импорта
        public IDataService<SysModel> GetImportService(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            var service = _exchangeServices.FirstOrDefault(s => s.DefaultExtension.ToLower() == ext && s.CanImport);

            return service ?? throw new InvalidOperationException($"Формат {ext} не поддерживает Импорт");
        }


        // Общий приватный метод для сборки строки фильтра
        private string BuildFilterString(IEnumerable<IDataService<SysModel>> services)
        {
            if (!services.Any()) return "Все файлы (*.*)|*.*";

            // Собираем маску для первой строки "Файлы обмена (*.xml;*.json)"
            var extensions = services.Select(s => $"*{s.DefaultExtension}");
            string allMasks = string.Join(";", extensions);

            var filterBuilder = new StringBuilder();
            filterBuilder.Append($"Файлы обмена ({allMasks})|{allMasks}");

            // Добавляем фильтр каждого сервиса по отдельности
            foreach (var service in services)
            {
                filterBuilder.Append($"|{service.FormatName} (*{service.DefaultExtension})|*{service.DefaultExtension}");
            }

            filterBuilder.Append("|Все файлы (*.*)|*.*");
            return filterBuilder.ToString();
        }



    }

}



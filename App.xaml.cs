using Emulation.Models;
using Emulation.Services;
using Emulation.Services.DataServices;
using Emulation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Emulation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();



            // Регистрируем сервисы данных

            // 1. Внутреннее хранилище проекта (например пока BSON, работает со структурой проекта)
            serviceCollection.AddSingleton<IDataService<ProjectFile>, BsonDataService<ProjectFile>>();


            // 2. Сервисы импорта/экспорта (XML, JSON, BSON ... работают напрямую с чистой SysModel)
            serviceCollection.AddSingleton<IDataService<SysModel>, XmlDataService<SysModel>>(); // <--для XML XmlDataService:
            serviceCollection.AddSingleton<IDataService<SysModel>, JsonDataService<SysModel>>();// <--для JSON  JsonDataService:
            serviceCollection.AddSingleton<IDataService<SysModel>, BsonDataService<SysModel>>();// <--для BSON  BsonDataService:


            // 3.Другие сервисы (Фабрика и инфраструктура)
            serviceCollection.AddSingleton<IDataServiceFactory, DataServiceFactory>();
            serviceCollection.AddSingleton<IAppSettings, AppSettings>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<IMessageService, MessageService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();

            // 4. Окна и модели
            serviceCollection.AddTransient<SysViewModel>(); // <--бизнес-логика (ViewModel)
            serviceCollection.AddTransient<MainWindow>();// <-- Само окно (View) как Transient-элемент

            // Строим контейнер
            ServiceProvider = serviceCollection.BuildServiceProvider();


            // Запрашиваем из контейнера САМО ОКНО.
            // Контейнер автоматически раскрутит всю цепочку:
            // Создаст сервисы -> соберет с ними ViewModel -> передаст ее в конструктор MainWindow
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Emulation.Services
{
    public class WindowService : IWindowService
    {
        public void CloseCurrentWindow()
        {
            // Закрывает главное окно приложения
            System.Windows.Application.Current.MainWindow?.Close();
        }
    }
}

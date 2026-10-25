using Emulation.Models;
using Emulation.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Emulation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Внедрение зависимости прямо в конструктор окна
        public MainWindow(SysViewModel viewModel)
        {
            InitializeComponent();

            // Задаем DataContext для связывания (Binding) в XAML
            DataContext = viewModel;
        }

        // Обработка событий окна

        // Этот метод сработает при нажатии на закрытие окна
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Достаем нашу ViewModel из DataContext
            if (DataContext is SysViewModel viewModel)
            {
                // Напрямую вызываем метод ViewModel и передаем туда аргументы события
                viewModel.Exit(e);
            }
        }

        private void NodeBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.OriginalSource is DependencyObject control)
            {
                // Находим главный контрол Node
                Border? mainControl = control.FindAncestor<Border>("MainNodeControl");
                if (mainControl != null && mainControl.DataContext is Node node && this.DataContext is SysViewModel viewModel)
                {
                    //viewModel.OnResizeNodeControl(node, e.NewSize.Width);
                    node.RightSideWidth = e.NewSize.Width;

                }
            }
        }


    }
}

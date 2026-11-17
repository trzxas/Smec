using Smec.Adorners;
using Smec.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace Smec
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SysViewModel _viewModel;
        private NodeDragAdorner? _dragAdorner;
        private AdornerLayer? _adornerLayer;

        // Внедрение зависимости прямо в конструктор окна
        public MainWindow(SysViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            _viewModel.RequestClearSelection += () =>
            {
                ComponentsLayer.SelectedItems.Clear();
                LinksLayer.SelectedItems.Clear();
            };

            _viewModel.RequestSelectNodes += nodesToSelect =>
            {
                ComponentsLayer.SelectedItems.Clear();
                foreach (var node in nodesToSelect)
                {
                    ComponentsLayer.SelectedItems.Add(node);
                }
            };

            // Задаем DataContext для связывания (Binding) в XAML
            DataContext = viewModel;

            // Вызывать внутри Window или любого элемента, уже добавленного в визуальное дерево
            //DpiScale dpiScale = VisualTreeHelper.GetDpi(this);

            //double dpiX = dpiScale.PixelsPerInchX; // Например, 96, 120 или 144
            //double dpiY = dpiScale.PixelsPerInchY;
            //double scaleFactor = dpiScale.DpiScaleX; // Множитель масштаба (1.0, 1.25, 1.5 и т
        }

        // Обработка событий окна

        // Этот метод сработает при нажатии на закрытие окна
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Напрямую вызываем метод ViewModel и передаем туда аргументы события
            _viewModel.Exit(e);
        }


        private void Window_SelectAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ComponentsLayer.SelectAll();
        }
        private void Window_SelectAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ComponentsLayer?.Items.Count > 0;
        }

       

        
        

        

        

       
    }
}

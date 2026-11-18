namespace Smec.Helpers
{
    using Smec.Converters;
    using Smec.Models;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;

    public static class PanelExtensions
    {
        // Регистрируем свойство зависимости ItemsSource для панелей
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.RegisterAttached(
                "ItemsSource",
                typeof(IEnumerable<PortGroup>),
                typeof(PanelExtensions),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static void SetItemsSource(DependencyObject element, IEnumerable<PortGroup> value) => element.SetValue(ItemsSourceProperty, value);
        public static IEnumerable<PortGroup> GetItemsSource(DependencyObject element) => (IEnumerable<PortGroup>)element.GetValue(ItemsSourceProperty);


        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Panel panel)
            {
                // Если коллекция реализует INotifyCollectionChanged (например, ObservableCollection), следим за изменениями
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                    oldCollection.CollectionChanged -= (s, arg) => UpdatePanelItems(panel, s as IEnumerable<PortGroup>);

                if (e.NewValue is INotifyCollectionChanged newCollection)
                    newCollection.CollectionChanged += (s, arg) => UpdatePanelItems(panel, s as IEnumerable<PortGroup>);

                UpdatePanelItems(panel, e.NewValue as IEnumerable<PortGroup>);
            }
        }

        private static void UpdatePanelItems(Panel panel, IEnumerable<PortGroup> collection)
        {

            // Удаляем старые динамические элементы типа PortGroup, оставляя остальные
            for (int i = panel.Children.Count - 1; i >= 0; i--)
            {
                if (panel.Children[i] is FrameworkElement fe && fe.DataContext is PortGroup)
                {
                    panel.Children.RemoveAt(i);
                }
            }

            if (collection == null) return;

            SideEnumToGridConverter conv = new();

            foreach (var item in collection.OfType<PortGroup>())
            {
                if (item.Ports.Count > 0)
                {
                    // Находим DataTemplate для этого типа данных во внешних ресурсах
                    // if (panel.Resources[typeof(PortGroup)] is DataTemplate template)
                    if (panel.TryFindResource(new DataTemplateKey(typeof(PortGroup))) is DataTemplate template)
                    {
                        // Создаем визуальный элемент из шаблона
                        if (template.LoadContent() is FrameworkElement visualElement)
                        {
                            visualElement.DataContext = item;

                            // Устанавливаем свойства GridRow и GridColumn испоьзуя специальный конвертер
                            // Безопасное извлечение координат через конвертер
                            if (conv.Convert(item.Side, typeof(int), "Row", CultureInfo.InvariantCulture) is int row &&
                                conv.Convert(item.Side, typeof(int), "Column", CultureInfo.InvariantCulture) is int clmn)
                            {
                                Grid.SetRow(visualElement, row);
                                Grid.SetColumn(visualElement, clmn);
                            }

                            // ФИЗИЧЕСКИ добавляем во внешний Grid!
                            panel.Children.Add(visualElement);
                        }
                    }
                }
            }
        }
    }
}



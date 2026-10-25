using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Markup;

namespace Emulation
{
    //    Как это работает:
    //1. Парсер XAML видит local:WindowDataContextBinding Path=Name и создает объект класса через конструктор по умолчанию(без параметров).
    //2. Видя Path = Name, парсер вызывает сеттер свойства: instance.Path = "Name".
    //3. Только после этого вызывается метод ProvideValue(serviceProvider).
    public class WindowDataContextBindingExtension : MarkupExtension
    {
        public string? Path { get; set; }

        // Конструктор по умолчанию (обязателен, если есть другие конструкторы)
        public WindowDataContextBindingExtension() { }

        // Конструктор с параметром для позиционного синтаксиса 
        // Чтобы не писать каждый раз слово Path=, значение можно передать первым же аргументом сразу после имени класса.
        // Это называется позиционным параметром.
        // Например для такого синтаксиса XAML (без Path) <TextBlock Text="{local:WindowDataContextBinding Name}" />
        // Парсер XAML не умеет сам догадываться, в какое свойство записать «безымянную» строку.
        // Для этого в класс нужно добавить конструктор, принимающий эту строку:
        public WindowDataContextBindingExtension(string path)
        {
            Path = path;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Необязательно , но с помощью  serviceProvider можно узнать какому объекту и какому свойству назначается значение
            //var targetProvider = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

            //if (targetProvider != null)
            //{
            //    object targetObject = targetProvider.TargetObject;     // Например, кнопка (Button) или TextBlock
            //    object targetProperty = targetProvider.TargetProperty; // Например, свойство TextProperty или WidthProperty
            //}

            // Возвращаем динамическую привязку к DataContext главного или активного окна
            var binding = new System.Windows.Data.Binding
            {
                Path = new PropertyPath($"DataContext.{Path}"),
                Source = System.Windows.Application.Current.MainWindow // Или через Application.Current.Resources, если ViewModel глобальная
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}

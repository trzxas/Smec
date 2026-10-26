using System.Windows;
using System.Windows.Media;

namespace Emulation.Helpers
{
    public static class VisualTreeExtensions
    {
        // Метод шукає батьківський елемент заданого типу T та з конкретним іменем
        public static T? FindAncestor<T>(this DependencyObject current, string? name = null) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                {
                    // Якщо ім'я не вказано, або воно збігається — повертаємо елемент
                    if (string.IsNullOrEmpty(name) || (ancestor is FrameworkElement fe && fe.Name == name))
                    {
                        return ancestor;
                    }
                }
                // Піднімаємося на один рівень вгору
                current = VisualTreeHelper.GetParent(current);
            }
            return null; // Якщо нічого не знайшли
        }
    }
}

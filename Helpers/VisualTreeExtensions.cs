using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Smec.Helpers
{
    public static class VisualTreeExtensions
    {
        // Метод шукає батьківський елемент заданого типу T та з конкретним іменем
        public static T? FindAncestor<T>(this DependencyObject? current, string? name = null, 
            Func<T, bool>? predicate = null) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                {
                    bool nameMatches = string.IsNullOrEmpty(name) || (ancestor is FrameworkElement fe && fe.Name == name);
                    bool predicateMatches = predicate == null || predicate(ancestor);

                    if (nameMatches && predicateMatches)
                    {
                        return ancestor;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public static T? FindDescendant<T>(this DependencyObject parent, string? name = null) where T : DependencyObject
        {
            if (parent == null) return null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            // Проходимся по всем детям
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T descendant)
                {
                    // Якщо ім'я не вказано, або воно збігається — повертаємо елемент
                    if (string.IsNullOrEmpty(name) || (descendant is FrameworkElement fe && fe.Name == name))
                    {
                        return descendant;
                    }
                }
                // Продолжаем поиск в глубину, если не нашли на текущем уровне
                var subChild = FindDescendant<T>(child, name);
                if (subChild != null) return subChild;
            }
            return null; // Ничего не нашли
        }

        public static T? FindElementAtPoint<T>(this UIElement root, Point point, string? name = null, 
            Func<T, bool>? predicate = null)where T : DependencyObject
        {
            T? found = null;

            VisualTreeHelper.HitTest(
                root,
                null,
                result =>
                {
                    var match = result.VisualHit.FindAncestor<T>(name, predicate);
                    if (match != null)
                    {
                        found = match;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(point));

            return found;
        }
    }
}


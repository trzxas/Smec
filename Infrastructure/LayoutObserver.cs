using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Smec.Helpers;
using Smec.Models;
using Smec.ViewModels;
using Point = System.Windows.Point;

namespace Smec.Infrastructure
{
    // Уже не используется но остался ка образец создания свойств зависимостей
    //public static class LayoutObserver
    //{
    //    // создаем специальное свойство зависимости для отслеживания визуального изменения
    //    public static readonly DependencyProperty ObserveProperty =
    //        DependencyProperty.RegisterAttached("Observe", typeof(bool), typeof(LayoutObserver),
    //            new PropertyMetadata(false, OnObserveChanged));

    //    public static bool GetObserve(DependencyObject obj) => (bool)obj.GetValue(ObserveProperty);
    //    public static void SetObserve(DependencyObject obj, bool value) => obj.SetValue(ObserveProperty, value);

    //    private static void OnObserveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    //    {
    //        if (d is FrameworkElement element && (bool)e.NewValue)
    //        {
    //            element.LayoutUpdated += (s, args) =>
    //            {
    //                if (element.DataContext is NodePort portModel)
    //                {
    //                    var mainCanvas = element.FindAncestor<Canvas>();
    //                    if (mainCanvas == null) return;
    //                    if (!element.IsDescendantOf(mainCanvas)) return;

    //                    // Находим Line внутри Grid порта
    //                    Line? portLine = element is Line line ? line : element.FindDescendant<Line>();
    //                    if (portLine == null) return;

    //                    // Конец пина — это всегда X2, Y2, так сделано в разметке
    //                    Point localTargetPoint = new(portLine.X2, portLine.Y2);

    //                    // Переводим локальную точку (X2,Y2) линии в абсолютные координаты сцены
    //                    Point absoluteLocation = portLine.TransformToAncestor(mainCanvas).Transform(localTargetPoint);

    //                    // Записываем результат напрямую в свойство Location Порта
    //                    portModel.Location = absoluteLocation;
    //                }
    //            };
    //        }
    //    }

    //}
}



using Emulation.Helpers;
using Emulation.Models;
using Emulation.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace Emulation
{
    /// <summary>
    /// Для перемещения узлов используем DragAndDrop процедуру.
    /// Нужно не забыть разрешить в родительской панели (в Canvas) принимать DragAndDrop и назначить методы обработки событий 
    /// AllowDrop="True" Drop="NodeArea_Drop" DragOver="NodeArea_DragOver"
    ///  
    /// При нажатии левой кнопки мыши над необходимым узлом, срабатывает вызов метода Node_MouseLeftButtonDown в главном окне
    /// В этом методе проверяем (на всякий случай), что  элемент есть ContentPresenter и его DataContext = Node
    /// Запоминаем позицию курсора в месте клика в этом элементе
    /// Формируем объект DataObject в который заносим информацию о позиции клика и самом Node, который связан с визуальным элементом
    /// Вызываем (пока на всякий случай, в нем можно выполнить определенные действия перед началом) метод из VM перед началом DragAndDrop. 
    /// Начинаем процедуру DragDrop.DoDragDrop, в качестве парметров передаются: 
    ///     1. Источник (это Визуальный Элемент), 
    ///     2. Информация(та что переносится, сейчас там объект DragPointsData, в котором сохраняем позицию клика относительно самого элемента и его родительской панели.)
    ///     3. Разрешенные  DragDrop эффекты, сечас там 2 эффекта: DragDropEffects.Copy | DragDropEffects.Move
    /// Возврат из данного метода с результатом типа эффекта будет только после завершения процедуры DragDrop.
    /// 
    /// С этого момента начинаем принимать сообщения DragOver в родительской панели и обрабатывать в назначенном методе NodeArea_DragOver
    /// В этом методе проверяем через вспомогательный метод CheckMode нажата ли клавиша Ctrl и если да то переключаемся на еффект копирования, иначе простое перемещение.
    /// Также вызываем метод из VM в который передаем для сохранения текущую позицию мыши, полученную через вспомогательный метод GetPosition
    /// 
    /// После отпускания левой кнопки мыши сработает в панели событие Drop к которому прикреплен метод главного окна NodeArea_Drop.
    /// Функционал этого метода полностью совпадает с Node_DragOver, поэтому вызываем его еще раз (получим там окончательный эффект и положение мыши).
    /// После этого произойдет возврат в Node_MouseLeftButtonDown, в котором начиналась процедура DragAndDrop.
    /// В качестве возвращаемого значения получаем результирующий эффект и вызываем метод из VM для завершения DragDrop
    /// CompleteDragDrop(node, result), в котором в качестве параметров предаем этот эффект и изначальный Node.
    /// 
    /// В этом методе выполняем соотвествующие действия в зависимости от эффекта
    /// После этого процедура считается завершенной
    /// 
    /// ! Важно
    /// При таком подходе все, что связано с интерфейсной частью будет выполняться в методах окна, а все изменения модели будут в методах VM. 
    /// Получаем чистую архитектуру MVVM и возможность тестирования. Не совсем! Можно вообще обойтись без этого 
    /// 
    /// Недостатки/Недоделки:
    /// При копировании изначальный элемент (и его Node) будут иметь координаты с вновь созданным (они наложаться). 
    /// Нужно сохранять изначальное положение исходного элемента 
    /// и если эффект Copy то восстанавливать его положение. СДЕЛАНО!
    /// 
    /// Если запретить копирование через Drug&Drop то код немного упроститься
    /// </summary>

    public partial class MainWindow : Window
    {
        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs me)
        {
            if (me.OriginalSource is DependencyObject clickedControl)
            {
                // Находим главный контрол Node
                Border? mainControl = clickedControl.FindAncestor<Border>("MainNodeControl");

                if (mainControl!=null && mainControl.DataContext is Node node && this.DataContext is SysViewModel viewModel)
                {
                        // 1. Вычисляем сдвиг курсора относительно самого элемента
                        Point clickOffset = me.GetPosition(mainControl);
                        Panel? parent = mainControl.FindAncestor<Panel>("NodeArea");
                        if (parent == null) return;

                        Point originalLocation = me.GetPosition(parent);
                        originalLocation.Offset(-clickOffset.X, -clickOffset.Y);

                        // 2. Упаковываем данные для перетаскивания в спец. контейнер
                        System.Windows.DataObject dragData = new();
                        DragPointsData dragPointsData = new() { ClickOffset = clickOffset, OriginalLocation = originalLocation };
                        dragData.SetData(typeof(DragPointsData), dragPointsData);

                        // Запускаем Drag & Drop (это блокирующий вызов, пока пользователь не отпустит мышь)
                        viewModel.StartDrag(node);

                        // Указываем допустимые эффекты DragDropEffects.Copy | DragDropEffects.Move
                        var result = DragDrop.DoDragDrop(mainControl, dragData, DragDropEffects.Move | DragDropEffects.Copy);

                        // Сообщаем VM, что drop успешно завершен, и передаем финальный эффект
                        viewModel.CompleteDragDrop(result, originalLocation);

                        me.Handled = true;
    
                }
            }
        }

        private void NodeArea_DragOver(object sender, DragEventArgs de)
        {

            if (sender is Panel panel && this.DataContext is SysViewModel viewModel)
            {
                CheckMode(de);

                if (de.Data.GetDataPresent(typeof(DragPointsData)))
                {
                    var data = (DragPointsData)de.Data.GetData(typeof(DragPointsData));
                    Point originalLocation = data.OriginalLocation;
                    Point currentLication = GetPosition(panel, de);

                    // Передаем текущую координату левого верхнего угла ноды
                    viewModel.SetDropPoint(currentLication, de.Effects, originalLocation);
                }
            }
            de.Handled = true;
        }

        private void NodeArea_Drop(object sender, DragEventArgs de)
        {
            NodeArea_DragOver(sender, de);
        }

        private static void CheckMode(DragEventArgs de)
        {
            // ПРОВЕРКА: нажат ли клавиша Control 
            bool isCtrlPressed = de.KeyStates.HasFlag(DragDropKeyStates.ControlKey);
            de.Effects = isCtrlPressed ? DragDropEffects.Copy : DragDropEffects.Move;
        }

        /// <summary>
        /// Определение положения мыши относительно заданной панели
        /// </summary>
        /// <param name="panel">Панель относительно которой будет вычисляться положение мыши</param>
        /// <param name="de">аргумент, из которого можно получить текущее положение мыши</param>
        /// <returns></returns>
        private static Point GetPosition(Panel panel, DragEventArgs de)
        {
            Point mousePosOnCanvas = de.GetPosition(panel);
           
            if (de.Data.GetDataPresent(typeof(DragPointsData)))
            {
                var data = (DragPointsData)de.Data.GetData(typeof(DragPointsData));
                Point clickOffset = data.ClickOffset;

                // Возвращаем ПРАВИЛЬНУЮ финальную точку для левого верхнего угла узла
                return new Point(
                    mousePosOnCanvas.X - clickOffset.X,
                    mousePosOnCanvas.Y - clickOffset.Y
                );
            }
            // Дефолтное значение на случай, если данных нет (защита от ошибки компиляции)
            return mousePosOnCanvas;
        }

        public class DragPointsData
        {
            public Point ClickOffset { get; set; }
            public Point OriginalLocation { get; set; }
        }
    }
}

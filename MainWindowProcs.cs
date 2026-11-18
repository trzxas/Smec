using Smec.Adorners;
using Smec.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Smec
{
    /// <summary>
    /// Скорее всего уже не актуально!
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
    /// </summary>
    /// 

    //<summary> Порядок сообщений на примере прохождения Mousedown  в Window->Grid->Button 
    // На первом этапе идет тунелирование Tunneling (от родителя к детям) , события начинаются на Preview***
    // А затем всплытие Bubbling (от ребенка к предкам).

    // Tunneling
    //1. Window.PreviewMouseDown (спуск)
    //2. Grid.PreviewMouseDown (спуск)
    //3. Button.PreviewMouseDown (спуск)

    // Bubbling
    //1. Button.MouseDown (всплытие)
    //2. Grid.MouseDown (всплытие)
    //3. Window.MouseDown(всплытие)

    // На любом из этапов можно запретить дальнейшее перемещение указав в обработчике e.Handled=true

    // Событие Click  — это высокоуровневое всплывающее событие.
    // У него нет туннельной фазы (события PreviewClick в WPF не существует). Оно использует стратегию маршрутизации Bubbling (всплытие).
    // Фаза тунелирования
    //1. Window.PreviewMouseLeftButtonDown — нажатие мыши начинает спускаться сверху.
    //2. Grid.PreviewMouseLeftButtonDown — спускается дальше.
    //3. Button.PreviewMouseLeftButtonDown — доходит до кнопки.

    // Обрабатывается MouseLeftButtonDown и создается новое Событие которое идет по маршруту всплытия
    //1. (Внутри кнопки срабатывает MouseLeftButtonDown, кнопка перехватывает его, ставит e.Handled = true и генерирует новое событие — Click)
    //2. Button.Click — срабатывает сначала на самой кнопке.
    //3. Grid.Click — всплывает к родителю(если подписаться через ButtonBase.Click= "Handler").
    //4. Window.Click — доходит до самого окна в последнюю очередь.
    //</summary

    public partial class MainWindow : Window
    {
        // Единое состояние для перетаскивания (и с холста, и из левой панели)
        private Point _dragStartPoint;
        private Point _initClickOffset;
        private FrameworkElement? _dragSourceControl;
        private NodeVm? _draggedNode;
        private bool _isClickOnAlreadySelectedNode;

        // Флаг состояния интерактивного создания (проброса) линка между портами
        private bool _isDrawingLink;

        // Служебные Поля в режиме выделения рамкой
        private bool _isSelectingWithBox;  // Флаг режима
        private Point _selectionBoxStartPoint; // Начальная точка выделения 


        #region Обработка событий главной панели
        private void WorkGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs me)
        {
            if (_viewModel.IsSimulating) return;

            // 1. Кликнули по пустому месту холста (мимо узлов и связей) — сбрасываем выделение
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                ComponentsLayer.SelectedItems.Clear();
                LinksLayer.SelectedItems.Clear();
            }

            // 2. Запускаем рисование рамки выделения (SelectionBox)
            _selectionBoxStartPoint = me.GetPosition(WorkGrid);
            _isSelectingWithBox = true;

            SelectionBox.Margin = new Thickness(_selectionBoxStartPoint.X, _selectionBoxStartPoint.Y, 0, 0);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
            SelectionBox.Visibility = Visibility.Visible;

            // Захватываем мышь на ComponentsLayer, чтобы ваши существующие Node_PreviewMouseMove 
            // и Node_PreviewMouseLeftButtonUp продолжили вести и завершать рамку
            ComponentsLayer.CaptureMouse();
            me.Handled = true;
        }

        private void WorkGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {

            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            if (sender is Grid grid)
            {
                // 1. Запоминаем позицию курсора относительно содержимого (Grid) и относительно окна (ScrollViewer)
                Point mousePosInGrid = e.GetPosition(grid);
                Point mousePosInScrollViewer = e.GetPosition(MainScrollViewer);

                // 2. Меняем масштаб через ViewModel
                double oldZoom = _viewModel.ZoomLevel;
                if (e.Delta > 0)
                    _viewModel.ZoomIn();
                else
                    _viewModel.ZoomOut();

                double newZoom = _viewModel.ZoomLevel;

                // Если масштаб реально изменился (не уперлись в мин/макс лимит)
                if (Math.Abs(newZoom - oldZoom) > 0.0001)
                {
                    // 3. Принудительно обновляем макет, чтобы ScrollViewer пересчитал ExtentWidth/ExtentHeight
                    MainScrollViewer.UpdateLayout();

                    // 4. Вычисляем новые смещения скролла:
                    // Новая координата точки на экране минус позиция курсора в окне ScrollViewer
                    double targetOffsetX = (mousePosInGrid.X * newZoom) - mousePosInScrollViewer.X;
                    double targetOffsetY = (mousePosInGrid.Y * newZoom) - mousePosInScrollViewer.Y;

                    MainScrollViewer.ScrollToHorizontalOffset(targetOffsetX);
                    MainScrollViewer.ScrollToVerticalOffset(targetOffsetY);
                }

                e.Handled = true;
            }

        }

        private void WorkGrid_DragEnter(object sender, DragEventArgs de)
        {
            if (de.Data.GetDataPresent(typeof(DragPointsData)) &&
                de.Data.GetData(typeof(DragPointsData)) is DragPointsData dragData &&
                _draggedNode != null &&
                _dragAdorner == null &&
                (de.AllowedEffects & DragDropEffects.Copy) == DragDropEffects.Copy)
            {
                _adornerLayer = AdornerLayer.GetAdornerLayer(WorkGrid);
                if (_adornerLayer != null)
                {
                    var selector = TryFindResource("NodeTemplateSelector") as DataTemplateSelector;
                    var template = TryFindResource("NodeTemplate") as DataTemplate;

                    _dragAdorner = new NodeDragAdorner(WorkGrid, _draggedNode, selector, template);
                    _adornerLayer.Add(_dragAdorner);
                }
            }
            de.Handled = true;
        }

        private void WorkGrid_DragLeave(object sender, DragEventArgs de)
        {
            // Удаляем фантом, только если курсор действительно покинул пределы WorkGrid
            Point pos = de.GetPosition(WorkGrid);
            if (pos.X < 0 || pos.Y < 0 || pos.X > WorkGrid.ActualWidth || pos.Y > WorkGrid.ActualHeight)
            {
                RemoveDragAdorner();
            }
        }

        private void WorkGrid_DragOver(object sender, DragEventArgs de)
        {
            if (de.Data.GetDataPresent(typeof(DragPointsData)) && de.Data.GetData(typeof(DragPointsData)) is DragPointsData dragData)
            {
                // Получаем координаты мыши относительно слоя компонентов (с учётом зума, скролла и DPI)
                Point mousePos = de.GetPosition(WorkGrid);

                // Вычитаем смещение клика (для Stock оно равно (0, 0))
                Point targetCenter = new(
                    mousePos.X - dragData.InitClickOffset.X,
                    mousePos.Y - dragData.InitClickOffset.Y);

                _viewModel.SetDropPoint(targetCenter);

                if (_dragAdorner != null)
                {
                    double grid = _viewModel.GridSettings.GridSize;
                    _dragAdorner.SetCenterPosition(new Point(
                        _viewModel.LastGridPoint.X * grid,
                        _viewModel.LastGridPoint.Y * grid));
                }

            }
            de.Handled = true;
        }

        private void WorkGrid_Drop(object sender, DragEventArgs de)
        {
            RemoveDragAdorner(); // Мгновенно убираем полупрозрачный фантом в момент броска!
            WorkGrid_DragOver(sender, de);
        }
        #endregion

        #region Обработка событий списка NodesListBox — выделение, перемещение группы/узла и тд

        private void NodeListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs me)
        {
            if (sender is not ListBox listBox || me.OriginalSource is not DependencyObject clickedControl)
                return;

            // Во время симуляции запрещаем любые изменения на холсте
            if (_viewModel.IsSimulating)
            {
                listBox.SelectedItems.Clear();
                return;
            }

            // Приводим Run (если кликнули по буквам текста) к родительскому FrameworkElement
            var element = (clickedControl as FrameworkElement)
                          ?? (clickedControl as FrameworkContentElement)?.Parent as FrameworkElement;

            if (element == null)
                return;

            // Сразу находим контейнер элемента (ListBoxItem) и его NodeVm — он пригодится и для пина, и для узла
            if (listBox.ContainerFromElement(element) is not ListBoxItem { DataContext: NodeVm nodeVm } listBoxItem)
                return;

            // 1. Проверяем, не кликнули ли мы по ПОРТУ (PinVm)
            if (element.DataContext is PinVm sourcePin)
            {
                _isDrawingLink = true;

                // Передаём и узел-владелец (nodeVm), и сам пин (sourcePin)!
                _viewModel.StartLinkCreation(nodeVm, sourcePin);

                listBox.Cursor = TryFindResource("PlugArrowCursor") as Cursor ?? Cursors.Cross;
                listBox.CaptureMouse();
                me.Handled = true;
                return;
            }

            // 2. Иначе — это клик по самому компоненту (NodeVm)
            if (Keyboard.Modifiers == ModifierKeys.None)
                LinksLayer.SelectedItems.Clear();

            _dragStartPoint = me.GetPosition(null);

            // Вычисляем точное смещение клика относительно математического центра (0, 0) компонента на WorkGrid!
            Point mouseOnWorkGrid = me.GetPosition(WorkGrid);
            _initClickOffset = new Point(
                mouseOnWorkGrid.X - nodeVm.PixelX,
                mouseOnWorkGrid.Y - nodeVm.PixelY
            );

            _dragSourceControl = listBoxItem;
            _draggedNode = nodeVm;

            if (listBoxItem.IsSelected && Keyboard.Modifiers == ModifierKeys.None)
            {
                _isClickOnAlreadySelectedNode = true;
                me.Handled = true;
            }
            else
            {
                _isClickOnAlreadySelectedNode = false;
            }
        }

        //if (_isDrawingLink)
        //            {
        //                _viewModel.UpdateLinkCreation(e.GetPosition(WorkGrid));
        //                e.Handled = true;
        //                return;
        //            }

        private void NodeListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel.IsSimulating)
            {
                // Если режим симуляции то отменяем все режимы редактирования
                if (_isDrawingLink)
                {
                    _isDrawingLink = false;
                    _viewModel.CompleteLinkCreation();
                    ComponentsLayer.Cursor = null;
                    ComponentsLayer.ReleaseMouseCapture();


                }
                if (_isSelectingWithBox)
                {
                    _isSelectingWithBox = false;
                    SelectionBox.Visibility = Visibility.Collapsed;
                    ComponentsLayer.ReleaseMouseCapture();
                }
                ResetDragState();
                return;
            }

            if (sender is not ListBox listBox) return;

            // 1. Если сейчас тянем СВЯЗЬ (Link)
            if (_isDrawingLink && e.LeftButton == MouseButtonState.Pressed)
            {
                _viewModel.UpdateLinkCreation(e.GetPosition(WorkGrid));
                e.Handled = true; // <-- Блокируем дальнейшее всплытие MouseMove
                return;
            }

            // 2. Если сейчас тянем рамку выделения
            if (_isSelectingWithBox && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSelectionBox(e.GetPosition(WorkGrid));
                return;
            }

            // 3. Если тянем узел или группу узлов
            if (CanStartDrag(e))
            {
                _isClickOnAlreadySelectedNode = false;

                var selectedNodes = listBox.SelectedItems.OfType<NodeVm>().ToList();
                if (!selectedNodes.Contains(_draggedNode!))
                    selectedNodes.Add(_draggedNode!);

                ExecuteDragDrop(_dragSourceControl!, _draggedNode!, selectedNodes, _initClickOffset, DragDropEffects.Move);
            }
        }

        private void NodeListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                // Завершаем протягивание СВЯЗИ
                if (_isDrawingLink)
                {
                    _isDrawingLink = false;
                    listBox.Cursor = null;
                    listBox.ReleaseMouseCapture();

                    _viewModel.CompleteLinkCreation();

                    ResetDragState();
                    e.Handled = true;
                    return;
                }

                // Завершаем выделение рамкой
                if (_isSelectingWithBox)
                {
                    _isSelectingWithBox = false;
                    SelectionBox.Visibility = Visibility.Collapsed;
                    listBox.ReleaseMouseCapture();
                }
                // Если просто кликнули по узлу внутри группы без перетаскивания
                else if (_isClickOnAlreadySelectedNode && _draggedNode != null)
                {
                    listBox.SelectedItems.Clear();
                    listBox.SelectedItems.Add(_draggedNode);
                }
            }

            ResetDragState();
            e.Handled = true;
        }

        private void NodeListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 1. Запрещаем сдвиг узлов стрелками во время симуляции или если ничего не выделено
            if (_viewModel.IsSimulating || ComponentsLayer.SelectedItems.Count == 0)
                return;

            // 2. Определяем шаг сдвига в ЦЕЛЫХ КЛЕТКАХ СЕТКИ:
            // Ctrl + Стрелка -> сдвиг на 1 клетку сетки
            // Ctrl + Shift (или Ctrl + Alt) + Стрелка -> крупный сдвиг на 5 клеток сетки
            int gridStep;

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
            {
                gridStep = 4; // Ускоренный сдвиг на 4 клетки сетки (вместо 1px)
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                gridStep = 1; // Стандартный сдвиг ровно на 1 клетку сетки
            }
            else
            {
                return; // Без модификаторов отдаем управление самому ListBox
            }

            switch (e.Key)
            {
                case Key.Left:
                    _viewModel.MoveSelectedNodes(-gridStep, 0);
                    e.Handled = true;
                    break;

                case Key.Right:
                    _viewModel.MoveSelectedNodes(gridStep, 0);
                    e.Handled = true;
                    break;

                case Key.Up:
                    _viewModel.MoveSelectedNodes(0, -gridStep);
                    e.Handled = true;
                    break;

                case Key.Down:
                    _viewModel.MoveSelectedNodes(0, gridStep);
                    e.Handled = true;
                    break;
            }
        }


        private void NodeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.UpdateSelection(ComponentsLayer.SelectedItems.OfType<NodeVm>());
        }

        #endregion

        #region Обработка событий списка Links 
        private void Links_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.IsSimulating)
            {
                e.Handled = true;
                return;
            }

            // Если не зажат Ctrl — снимаем выделение с нод при клике на провод
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                ComponentsLayer.SelectedItems.Clear();
            }

            LinksLayer.Focus();
        }

        private void Links_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Во время симуляции запрещаем редактирование
            if (_viewModel.IsSimulating)
            {
                e.Handled = true;
                return;
            }

            // Ищем ListBoxItem того провода, по которому кликнули ПКМ
            if (e.OriginalSource is DependencyObject clicked && LinksLayer.ContainerFromElement(clicked) is ListBoxItem item)
            {
                // Если кликнули по НЕвыделенному проводу — сбрасываем старое выделение и выделяем только этот провод.
                // Если же кликнули по одному из УЖЕ выделенной группы проводов — оставляем группу выделенной, чтобы удалить её целиком!
                if (!item.IsSelected)
                {
                    ComponentsLayer.SelectedItems.Clear();
                    LinksLayer.SelectedItems.Clear();
                    item.IsSelected = true;
                }
            }
        }

        private void Links_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Запрещаем ScrollViewer автоматически прокручивать холст к (0, 0) при выделении провода или узла
            e.Handled = true;
        }

        


        #endregion

        #region Обработка событий списка StockListBox — перетягивание для создания нового узла

        private void StockSource_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _initClickOffset = default;

            if (sender is ListBox listBox)
            {
                var item = ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) as ListBoxItem;
                _dragSourceControl = item;
                _draggedNode = item?.DataContext as NodeVm;
            }
            else if (sender is FrameworkElement element)
            {
                _dragSourceControl = element;
                _draggedNode = element.DataContext as NodeVm;
            }
        }

        private void StockSource_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (CanStartDrag(e) && _draggedNode != null && sender is FrameworkElement dragSource)
            {
                //1-вариант. Нулевое смещение (0, 0) привязывает геометрический центр компонента строго к курсору
                // var zeroOffset = new Point(0, 0);
                
                //2-вариант. Сдвигаем точку захвата в верхний левый угол детали с учетом пинов
                //var topLeftOffset = new Point(-_draggedNode.HalfWidth, -_draggedNode.HalfHeight);
                
                //3-вариант. Сдвигаем точку захвата в верхний левый угол детали с учетом пинов
                var bodyTopLeftOffset = new Point(_draggedNode.BodyLeft, _draggedNode.BodyTop);
                
                ExecuteDragDrop(dragSource, _draggedNode, null, bodyTopLeftOffset, DragDropEffects.Copy);
            }
        }

        private void StockSource_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ResetDragState();


        #endregion

        #region Общие вспомогательные методы 



        private bool CanStartDrag(MouseEventArgs e)
        {
            if (_viewModel.IsSimulating) return false;

            if (e.LeftButton != MouseButtonState.Pressed || _draggedNode == null || _dragSourceControl == null)
                return false;

            Vector diff = _dragStartPoint - e.GetPosition(null);
            return Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
        }

        private void ExecuteDragDrop(
            FrameworkElement dragSource,
            NodeVm primaryNode,
            IEnumerable<NodeVm>? selectedNodes,
            Point clickOffset,
            DragDropEffects effect)
        {
            DataObject dragData = new();
            dragData.SetData(typeof(DragPointsData), new DragPointsData { InitClickOffset = clickOffset });

            _viewModel.StartDrag(primaryNode, selectedNodes);
            var result = DragDrop.DoDragDrop(dragSource, dragData, effect);
            _viewModel.CompleteDragDrop(result);

            ResetDragState();
        }

        private void ResetDragState()
        {
            _isClickOnAlreadySelectedNode = false;
            _draggedNode = null;
            _dragSourceControl = null;
        }

        private void RemoveDragAdorner()
        {
            if (_dragAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
                _adornerLayer = null;
            }
        }

        public class DragPointsData
        {
            public Point InitClickOffset { get; set; }
        }

        // Процедура обработки выбора рамкой
        private void UpdateSelectionBox(Point currentPos)
        {
            double x = Math.Min(_selectionBoxStartPoint.X, currentPos.X);
            double y = Math.Min(_selectionBoxStartPoint.Y, currentPos.Y);
            double width = Math.Abs(_selectionBoxStartPoint.X - currentPos.X);
            double height = Math.Abs(_selectionBoxStartPoint.Y - currentPos.Y);

            SelectionBox.Margin = new Thickness(x, y, 0, 0);
            SelectionBox.Width = width;
            SelectionBox.Height = height;

            Rect selectionRect = new(x, y, width, height);
            bool keepOldSelection = Keyboard.Modifiers != ModifierKeys.None;

            // 1. Выделение УЗЛОВ (строго по границам корпуса без выступающих пинов)
            foreach (var item in ComponentsLayer.Items)
            {
                if (ComponentsLayer.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container && item is NodeVm nodeVm)
                {
                    Rect bodyBounds = nodeVm.BodyGeometry.Bounds;
                    Rect itemBounds = !bodyBounds.IsEmpty
                        ? new Rect(
                            nodeVm.PixelX + bodyBounds.Left,
                            nodeVm.PixelY + bodyBounds.Top,
                            bodyBounds.Width,
                            bodyBounds.Height)
                        : new Rect(
                            nodeVm.PixelX - nodeVm.HalfWidth,
                            nodeVm.PixelY - nodeVm.HalfHeight,
                            nodeVm.TotalWidth > 0 ? nodeVm.TotalWidth : _viewModel.GridSettings.GridSize,
                            nodeVm.TotalHeight > 0 ? nodeVm.TotalHeight : _viewModel.GridSettings.GridSize);

                    if (selectionRect.IntersectsWith(itemBounds))
                    {
                        if (!container.IsSelected) container.IsSelected = true;
                    }
                    else if (!keepOldSelection && container.IsSelected)
                    {
                        container.IsSelected = false;
                    }
                }
            }

            // 2. Выделение ПРОВОДОВ (достаточно задеть рамкой любой отрезок провода)
            foreach (var item in LinksLayer.Items)
            {
                if (LinksLayer.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container && item is LinkVm linkVm)
                {
                    bool isHit = IsLinkIntersectingRect(linkVm.ScreenPoints, selectionRect);

                    if (isHit)
                    {
                        if (!container.IsSelected) container.IsSelected = true;
                    }
                    else if (!keepOldSelection && container.IsSelected)
                    {
                        container.IsSelected = false;
                    }
                }
            }
        }

        private static bool IsLinkIntersectingRect(PointCollection points, Rect rect)
        {
            if (points == null || points.Count == 0) return false;
            if (points.Any(rect.Contains)) return true;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Point a = points[i];
                Point b = points[i + 1];
                if (SegmentsIntersect(a, b, rect.TopLeft, rect.TopRight) ||
                    SegmentsIntersect(a, b, rect.TopRight, rect.BottomRight) ||
                    SegmentsIntersect(a, b, rect.BottomRight, rect.BottomLeft) ||
                    SegmentsIntersect(a, b, rect.BottomLeft, rect.TopLeft))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SegmentsIntersect(Point p1, Point p2, Point p3, Point p4)
        {
            double d1 = Cross(p3, p4, p1);
            double d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3);
            double d4 = Cross(p1, p2, p4);

            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));

            static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }


        #endregion


    }
}

using Smec.Models;
using Smec.ViewModels;
using DragDropEffects = System.Windows.DragDropEffects;
using Point = System.Windows.Point;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
        private NodeVm? _primaryDraggedNode;
        private readonly Dictionary<NodeVm, GridPoint> _draggedNodesInitialLocations = [];
        private GridPoint _lastGridPoint;
        public GridPoint LastGridPoint => _lastGridPoint;


        public void StartDrag(NodeVm primaryNode,  IEnumerable<NodeVm>? selectedNodes = null)
        {
            _primaryDraggedNode = primaryNode;
            _draggedNodesInitialLocations.Clear();

            // Сохраняем смещение курсора относительно (0, 0) ведущего компонента
            _lastGridPoint = primaryNode.Model.Location;

            // Запоминаем стартовые координаты в узлах сетки (GridPoint) для всей группы
            var nodesToTrack = selectedNodes ?? [primaryNode];
            foreach (var node in nodesToTrack)
            {
                _draggedNodesInitialLocations[node] = node.Model.Location;
            }
        }

        // Переводит экранные пиксели (от мыши на Canvas) в ближайший целый узел сетки
        private GridPoint ScreenToGrid(Point screenPoint)
        {
            double grid = GridSettings.GridSize;
            int gx = (int)Math.Round(screenPoint.X / grid);
            int gy = (int)Math.Round(screenPoint.Y / grid);
            return new GridPoint(gx, gy);
        }

        /// <summary>
        /// Обновляет координаты при движении над холстом (работает и для Copy, и для Move).
        /// </summary>
        public void SetDropPoint(Point targetCenterPixel)
        {
            if (_primaryDraggedNode == null || !_draggedNodesInitialLocations.TryGetValue(_primaryDraggedNode, out GridPoint primaryStart))
                return;

            // Сразу переводим целевой центр детали в узел сетки (GridPoint)
            GridPoint currentGridPoint = ScreenToGrid(targetCenterPixel);

            if (currentGridPoint == _lastGridPoint)
                return;

            _lastGridPoint = currentGridPoint;

            int deltaX = currentGridPoint.X - primaryStart.X;
            int deltaY = currentGridPoint.Y - primaryStart.Y;

            foreach (var (nodeVm, initialGridPos) in _draggedNodesInitialLocations)
            {
                var targetGridPos = new GridPoint(initialGridPos.X + deltaX, initialGridPos.Y + deltaY);
                nodeVm.SetGridLocation(targetGridPos);
            }
        }

        /// <summary>
        /// Универсальное завершение или отмена перетаскивания.
        /// </summary>
        public void CompleteDragDrop(DragDropEffects result)
        {
            if (_primaryDraggedNode == null)
                return;

            switch (result)
            {
                case DragDropEffects.Move:
                    // Проверяем, действительно ли узел сдвинулся с исходной точки
                    if (_draggedNodesInitialLocations.TryGetValue(_primaryDraggedNode, out var startPos) &&
                        startPos != _primaryDraggedNode.Model.Location)
                    {
                        VmStatus = ModelStatus.VisualChanged;
                    }
                    break;

                case DragDropEffects.Copy:
                    // 1. Создаём копию модели в новой точке сетки
                    var newNode = _primaryDraggedNode.Model.CreateDefault();
                    newNode.Name = newNode.CategoryName;
                    newNode.Location = _lastGridPoint;

                    CurrentModel?.Nodes.Add(newNode);

                    // 3. Возвращаем исходный элемент (который тащили с зажатым Ctrl) на его старое место
                    // Пока не используется этот режим!
                    foreach (var (nodeVm, initialGridPos) in _draggedNodesInitialLocations)
                    {
                        nodeVm.SetGridLocation(initialGridPos);
                    }

                    VmStatus = ModelStatus.VisualChanged;
                    break;

                case DragDropEffects.None:
                    // Откатываем все задействованные узлы на их исходные позиции в сетке (через SetGridLocation!)
                    foreach (var (nodeVm, initialGridPos) in _draggedNodesInitialLocations)
                    {
                        nodeVm.SetGridLocation(initialGridPos);
                    }
                    break;
            }

            _draggedNodesInitialLocations.Clear();
            _primaryDraggedNode = null;
        }

       
    }
}

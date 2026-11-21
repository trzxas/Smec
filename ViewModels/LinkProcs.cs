using Smec.Models;
using System.Collections;
using System.Windows.Input;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
        private double PortSnapRadius => GridSettings.GridSize * 0.8; // Радиус магнитного прилипания в пикселях


        private NodeVm? _highlightedTargetNode;
        private PinVm? _highlightedPin;

        private NodeVm? _pendingSourceNode;
        private PinVm? _pendingSourcePin;

        public bool IsCreatingLink { get; private set; }
        public System.Windows.Point PendingStartPoint { get; private set; }
        public System.Windows.Point PendingEndPoint { get; private set; }

        /// <summary>
        /// 1. Начало протягивания связи от исходного пина (вызывается при клике по PinVm)
        /// </summary>
        public void StartLinkCreation(NodeVm sourceNode, PinVm sourcePin)
        {
            if (IsSimulating || CurrentModel == null)
                return;

            // Сбрасываем возможную зависшую подсветку от предыдущей незавершённой протяжки
            CancelLinkCreation();

            _pendingSourceNode = sourceNode;
            _pendingSourcePin = sourcePin;

            // 1. Подсвечиваем начальный пин (сработает DataTrigger в PortLineStyle)
            sourcePin.HighlightStatus = ObjectStatus.Normal;

            // 2. Вычисляем стартовую экранную точку на конце пина
            var startPos = sourceNode.GetPinScreenPosition(sourcePin.Definition.Number);
            PendingStartPoint = startPos;
            PendingEndPoint = startPos;
            IsCreatingLink = true;

            OnPropertyChanged(nameof(IsCreatingLink));
            OnPropertyChanged(nameof(PendingStartPoint));
            OnPropertyChanged(nameof(PendingEndPoint));
        }

        /// <summary>
        /// 2. Обновление координат второго конца провода, поиск и подсветка ближайшего порта
        /// </summary>
        public void UpdateLinkCreation(System.Windows.Point mousePosition)
        {
            if (!IsCreatingLink || _pendingSourceNode == null || _pendingSourcePin == null)
                return;

            var candidate = FindNearestCompatiblePin(_pendingSourcePin, mousePosition, PortSnapRadius);

            // Сбрасываем подсветку со старого пина в null (не трогая стартовый!)
            if (_highlightedPin != null && _highlightedPin != _pendingSourcePin && _highlightedPin != candidate?.Pin)
            {
                _highlightedPin.HighlightStatus = null;
            }

            _highlightedTargetNode = candidate?.Node;
            _highlightedPin = candidate?.Pin;

            if (_highlightedPin != null)
            {
                bool isTwoOutputs = _pendingSourcePin.PortModel?.Mode == PortMode.Out &&
                                    _highlightedPin.PortModel?.Mode == PortMode.Out;

                _highlightedPin.HighlightStatus = isTwoOutputs
                    ? ObjectStatus.Warning
                    : ObjectStatus.Normal;

                PendingEndPoint = candidate!.Value.ScreenPos;
            }
            else
            {
                PendingEndPoint = mousePosition;
            }

            OnPropertyChanged(nameof(PendingEndPoint));
        }

        /// <summary>
        /// 3. Завершение протягивания (создание постоянного Link или отмена, если порт не выбран)
        /// </summary>
        public void CompleteLinkCreation()
        {
            if (!IsCreatingLink || _pendingSourceNode == null || _pendingSourcePin == null)
                return;

            if (_highlightedPin != null && _highlightedTargetNode != null && CurrentModel != null)
            {
                var portA = _pendingSourcePin.PortModel ?? new NodePort
                {
                    PinNumber = _pendingSourcePin.Definition.Number,
                    Owner = _pendingSourceNode.Model
                };

                var portB = _highlightedPin.PortModel ?? new NodePort
                {
                    PinNumber = _highlightedPin.Definition.Number,
                    Owner = _highlightedTargetNode.Model
                };

                var newLink = new Link
                {
                    PortA = portA,
                    PortB = portB,
                    Activity = true
                };

                CurrentModel.Links.Add(newLink);
                VmStatus = ModelStatus.LogicalChanged;
            }

            CancelLinkCreation();
        }

        public void CancelLinkCreation()
        {
            _pendingSourcePin?.HighlightStatus = null;
            _highlightedPin?.HighlightStatus = null;

            _pendingSourceNode = null;
            _pendingSourcePin = null;
            _highlightedPin = null;
            _highlightedTargetNode = null;
            IsCreatingLink = false;

            OnPropertyChanged(nameof(IsCreatingLink));
        }

        /// <summary>
        /// Поиск ближайшего совместимого пина в заданном радиусе (в пикселях экрана).
        /// </summary>
        private (NodeVm Node, PinVm Pin, System.Windows.Point ScreenPos)? 
            FindNearestCompatiblePin(PinVm sourcePin, System.Windows.Point mousePosition, double maxDistancePixels)
        {
            if (CurrentModel == null)
                return null;

            (NodeVm Node, PinVm Pin, System.Windows.Point ScreenPos)? bestMatch = null;
            double minDistSq = maxDistancePixels * maxDistancePixels;

            foreach (var nodeVm in Components)
            {
                foreach (var candidatePin in nodeVm.Pins)
                {
                    if (candidatePin == sourcePin)
                        continue;

                    if (sourcePin.PortModel != null && candidatePin.PortModel != null &&
                        !CanConnect(sourcePin.PortModel, candidatePin.PortModel))
                    {
                        continue;
                    }

                    System.Windows.Point pinScreenPos = nodeVm.GetPinScreenPosition(candidatePin.Definition.Number);

                    double dx = pinScreenPos.X - mousePosition.X;
                    double dy = pinScreenPos.Y - mousePosition.Y;
                    double distSq = dx * dx + dy * dy;

                    if (distSq <= minDistSq)
                    {
                        minDistSq = distSq;
                        bestMatch = (nodeVm, candidatePin, pinScreenPos);
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Проверка, можно ли соединить два порта
        /// </summary>
        private bool CanConnect(NodePort portA, NodePort portB)
        {
            if (CurrentModel == null) return false;

            // Нельзя соединить порт сам с собой
            if (portA == portB) return false;

            // Защита от дублирования проводов по ссылке и по паре (Owner + PinNumber)
            bool alreadyExists = CurrentModel.Links.Any(l =>
                (l.PortA == portA && l.PortB == portB) ||
                (l.PortA == portB && l.PortB == portA)
                 // Это лишняя проверка!
                 //|| (l.PortA.Owner == portA.Owner && l.PortA.PinNumber == portA.PinNumber &&
                 // l.PortB.Owner == portB.Owner && l.PortB.PinNumber == portB.PinNumber) 
                 //|| (l.PortA.Owner == portB.Owner && l.PortA.PinNumber == portB.PinNumber &&
                 // l.PortB.Owner == portA.Owner && l.PortB.PinNumber == portA.PinNumber)
                 );

            return !alreadyExists;
        }


        public void RemoveLinks(IEnumerable<LinkVm> linksToRemove)
        {
            if (IsSimulating || CurrentModel == null) return;

            foreach (var link in linksToRemove.ToList())
            {
                CurrentModel.Links.Remove(link.Model);
            }
        }

        // 1. Общий хелпер извлечения связей из параметра команды
        private static List<LinkVm> ExtractLinks(object? param)
        {
            if (param is object[] arr)
            {
                var selected = arr.OfType<System.Collections.IList>().SelectMany(l => l.OfType<LinkVm>()).ToList();
                if (selected.Count > 0) return selected;

                return [.. arr.OfType<LinkVm>()];
            }

            return param switch
            {
                System.Collections.IList list => [.. list.OfType<LinkVm>()],
                LinkVm singleLink => [singleLink],
                _ => []
            };
        }

        // 2. Проверка возможности удаления выделенных связей
        public bool CanDeleteSelectedLinks(object? param)
        {
            if (IsSimulating || CurrentModel == null) return false;
            return ExtractLinks(param).Count > 0;
        }
        // 3. Метод удаления связей
        public void DeleteSelectedLinks(object? param)
        {
            if (IsSimulating || CurrentModel == null) return;

            var links = ExtractLinks(param);
            if (links.Count > 0)
            {
                RemoveLinks(links);
            }
        }


        // Проверка возможности активации/деактивации выделенных связей (разрешено в покое и на паузе)
        public bool CanModifySelectedLinks(object? param)
        {
            if ((IsSimulating && !IsPaused) || CurrentModel == null) return false;
            return ExtractLinks(param).Count > 0;
        }

        // Метод переключения активности (Activity) связей
        public void ToggleLinksActivity(object? param)
        {
            if ((IsSimulating && !IsPaused) || CurrentModel == null) return;

            var links = ExtractLinks(param);
            if (links.Count == 0) return;

            bool targetState = links.Any(l => !l.Activity);
            foreach (var link in links)
            {
                link.Model.Activity = targetState;
            }

            // Если схема на паузе — мгновенно пересчитываем цепи и обновляем цвета проводов
            if (IsPaused)
            {
                foreach (var link in CurrentModel.Links.Where(l => !l.Activity))
                    link.ResetSimulatedState();

                foreach (var node in CurrentModel.Nodes)
                {
                    foreach (var port in node.Ports)
                    {
                        if (port.Mode == PortMode.In || (port.Mode == PortMode.InOut && !port.IsDriving))
                            port.State = PortState.Undefined;
                    }
                }

                BuildElectricalNets();
                foreach (var net in _compiledNets)
                    net.Evaluate();

                UpdateJunctions();
            }
        }

        #region Команды контекстного меню связей

        private void ExecuteSetLinksRoutingMode(object? parameter)
        {
            if (parameter is object[] { Length: 2 } args &&
                args[1] is LinkRoutingMode mode)
            {
                foreach (var link in ExtractLinks(args[0]))
                    link.RoutingMode = mode;
            }
        }

        private bool CanExecuteResetLinkOffsets(object? parameter) =>
            ExtractLinks(parameter).Any(l => l.HasSegmentOffsets);

        private void ExecuteResetLinkOffsets(object? parameter)
        {
            foreach (var link in ExtractLinks(parameter))
                link.ResetSegmentOffsets();
        }

        private bool CanEditLinkGeometry(object? param) =>!IsSimulating && CurrentModel != null;

        #endregion

    }
}

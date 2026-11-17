using Smec.Models;

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

            // 1. Передаём сразу _pendingSourcePin (в нём уже есть и NodeId, и PinNumber, и PortModel.Mode)
            var candidate = FindNearestCompatiblePin(_pendingSourcePin, mousePosition, PortSnapRadius);

            // 2. Сбрасываем подсветку со старого пина в null (не трогая стартовый!)
            if (_highlightedPin != null && _highlightedPin != _pendingSourcePin && _highlightedPin != candidate?.Pin)
            {
                _highlightedPin.HighlightStatus = null;
            }

            // 3. Запоминаем и целевой узел, и целевой пин
            _highlightedTargetNode = candidate?.Node;
            _highlightedPin = candidate?.Pin;

            if (_highlightedPin != null)
            {
                // Проверяем режим Out+Out через PortModel (если он привязан к PinVm)
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

            // Если курсор был примагничен к целевому пину — создаём постоянную связь!
            if (_highlightedPin != null && _highlightedTargetNode != null && CurrentModel != null)
            {
                // Берём существующие NodePort из пинов (где уже есть Owner, Mode и State),
                // либо создаём новые с привязкой к узлам
                var portA = _pendingSourcePin.PortModel ?? new NodePort
                {
                    //NodeId = _pendingSourceNode.Model.Id,
                    PinNumber = _pendingSourcePin.Definition.Number,
                    //Owner = _pendingSourceNode.Model
                    Owner=_pendingSourcePin.PortModel?.Owner
                };

                var portB = _highlightedPin.PortModel ?? new NodePort
                {
                    //NodeId = _highlightedTargetNode.Model.Id,
                    PinNumber = _highlightedPin.Definition.Number,
                    //Owner = _highlightedTargetNode.Model
                    Owner = _highlightedPin.PortModel?.Owner
                };

                var newLink = new Link
                {
                    PortA = portA,
                    PortB = portB,
                    Activity = true
                };

                // Добавляем ТОЛЬКО в CurrentModel.Links —
                // обработчик OnModelLinksChanged сам создаст LinkVm и отрисует провод на Canvas!
                CurrentModel.Links.Add(newLink);
                VmStatus = ModelStatus.LogicalChanged;
            }

            CancelLinkCreation(); // Сбрасываем временное состояние и подсветку пинов
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
        private (NodeVm Node, PinVm Pin, System.Windows.Point ScreenPos)? FindNearestCompatiblePin(
            PinVm sourcePin,
            System.Windows.Point mousePosition,
            double maxDistancePixels)
        {
            if (CurrentModel == null)
                return null;

            (NodeVm Node, PinVm Pin, System.Windows.Point ScreenPos)? bestMatch = null;
            double minDistSq = maxDistancePixels * maxDistancePixels;

            foreach (var nodeVm in Components)
            {
                foreach (var candidatePin in nodeVm.Pins)
                {
                    // Пропускаем тот же самый пин, с которого начали тянуть провод
                    if (candidatePin == sourcePin)
                        continue;

                    // Используем вашу существующую логику проверки совместимости портов!
                    if (sourcePin.PortModel != null && candidatePin.PortModel != null &&
                        !CanConnect(sourcePin.PortModel, candidatePin.PortModel))
                    {
                        continue;
                    }

                    // Получаем актуальные мировые пиксельные координаты пина на холсте
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

            // Защита от дублирования проводов между теми же двумя портами
            bool alreadyExists = CurrentModel.Links.Any(l =>
                (l.PortA == portA && l.PortB == portB) ||
                (l.PortA == portB && l.PortB == portA));

            return !alreadyExists;
        }




        public void RemoveLinks(IEnumerable<LinkVm> linksToRemove)
        {
            if (IsSimulating || CurrentModel == null) return;

            foreach (var link in linksToRemove.ToList())
            {
                link.Model.RemoveLogicalConnection();
                CurrentModel.Links.Remove(link.Model);
            }
        }

        // 1. Общий хелпер извлечения связей из параметра команды
        private static List<LinkVm> ExtractLinks(object? param) => param switch
        {
            // Если список
            System.Collections.IList list => [.. list.OfType<LinkVm>()],
            // Если одиночный
            LinkVm singleLink => [singleLink],
            // На всякий случай
            _ => []
        };

        // 2. Общая проверка возможности изменения/удаления выделенных связей
        public bool CanModifySelectedLinks(object? param)
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

        // 4. Метод переключения активности (Activity) связей
        public void ToggleLinksActivity(object? param)
        {
            if (IsSimulating || CurrentModel == null) return;

            var links = ExtractLinks(param);
            if (links.Count == 0) return;

            bool targetState = links.Any(l => !l.Activity);
            foreach (var link in links)
            {
                link.Model.Activity = targetState;
            }
        }

    }
}

using Smec.Models;
using Smec.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Label = System.Windows.Controls.Label;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {


        // Главное окно по этому событию в ListBoxNode выделяет переданные как параметр элементы
        public event Action<IEnumerable<NodeVm>>? RequestSelectNodes;


        public List<NodeVm> SelectedNodes { get; } = [];

        public void UpdateSelection(IEnumerable<NodeVm> selected)
        {
            SelectedNodes.Clear();
            SelectedNodes.AddRange(selected);

            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveNode(object? obj)
        {
            if (obj is not NodeVm clickedNode) return;

            // Если кликнутый узел есть в списке выделенных — удаляем всю выделенную группу
            if (SelectedNodes.Contains(clickedNode))
            {
                DeleteNodes(SelectedNodes);
            }
            else
            {
                // Иначе удаляем только тот узел, по которому кликнули правой кнопкой
                DeleteNodes([clickedNode]);
            }
        }

        /// <summary>
        /// Синхронно удаляет узлы и все привязанные к ним провода из Model
        /// </summary>
        public void DeleteNodes(IEnumerable<NodeVm> nodeVmsToDelete)
        {
            if (CurrentModel == null)
                return;

            // Просто удаляем Node из CurrentModel.Nodes —
            // SysModel сама удалит привязанные Links, а OnModelNodesChanged и OnModelLinksChanged сами очистят UI!
            foreach (var nodeVm in nodeVmsToDelete.ToList())
            {
                CurrentModel.Nodes.Remove(nodeVm.Model);
            }

            VmStatus = ModelStatus.LogicalChanged; // Удаление узлов — это логическое изменение модели, которое повлечёт за собой визуальные изменения
            UpdateContentBounds(); // Обновляем границ  ы холста после удаления узлов
        }


        private void CloneNode(object? obj)
        {
            if (obj is not NodeVm clickedNode) return;

            // Если узел входит в выделенную группу — клонируем всю группу, иначе только его
            if (SelectedNodes.Contains(clickedNode))
            {
                CloneNodes(SelectedNodes);
            }
            else
            {
                CloneNodes([clickedNode]);
            }
        }

        // <summary>
        /// Универсальный метод клонирования одного или группы узлов (Ctrl+D / контекстное меню).
        /// </summary>
        public void CloneNodes(IEnumerable<NodeVm> nodesToClone)
        {
            if (CurrentModel?.Nodes == null)
                return;

            var list = nodesToClone.ToList();
            if (list.Count == 0)
                return;

            var clonedModels = new List<Node>();

            foreach (var sourceVm in list)
            {
                // Используем Clone(), чтобы перенести все параметры (частоту генератора, номинал, поворот, зеркалирование)
                // При этом CopyPropertiesTo сам сбросит Designator, а Nodes.Add выдаст новый свободный номер!
                if (sourceVm.Model.Clone() is Node cloneNode)
                {
                    // Просто добавляем в модель — OnModelNodesChanged сам создаст NodeVm и добавит в Components!
                    CurrentModel.Nodes.Add(cloneNode);
                    clonedModels.Add(cloneNode);
                }
            }

            if (clonedModels.Count > 0)
            {
                // Находим автоматически созданные обёртки NodeVm в Components для передачи на выделение
                var newlyClonedVms = Components
                    .Where(vm => clonedModels.Contains(vm.Model))
                    .ToList();

                VmStatus = ModelStatus.LogicalChanged;
                UpdateContentBounds();
                RequestSelectNodes?.Invoke(newlyClonedVms);
            }
        }


        /// <summary>
        /// Сдвигает выделенные узлы на заданное количество клеток сетки (например, по стрелкам клавиатуры).
        /// </summary>
        public void MoveSelectedNodes(int deltaGridX, int deltaGridY)
        {
            if (SelectedNodes.Count == 0 || (deltaGridX == 0 && deltaGridY == 0))
                return;

            // Чтобы при упирании в левый/верхний край (0, 0) группа узлов не "сплющивалась" друг в друга,
            // ограничиваем общий сдвиг по самому крайнему узлу в выделении:
            int minX = SelectedNodes.Min(n => n.Model.Location.X);
            int minY = SelectedNodes.Min(n => n.Model.Location.Y);

            int actualDeltaX = Math.Max(-minX, deltaGridX);
            int actualDeltaY = Math.Max(-minY, deltaGridY);

            if (actualDeltaX == 0 && actualDeltaY == 0)
                return;

            foreach (var nodeVm in SelectedNodes)
            {
                var current = nodeVm.Model.Location;
                var newLocation = new GridPoint(current.X + actualDeltaX, current.Y + actualDeltaY);

                // Обновляем через SetGridLocation, чтобы сдвинулись и PixelX/Y, и привязанные провода!
                nodeVm.SetGridLocation(newLocation);
            }

            VmStatus = ModelStatus.VisualChanged;
            UpdateContentBounds();
        }


        private void ShowNodeProperties(object? obj)
        {
            if (obj is not NodeVm selectedNode) return;

            Node? modelNode = selectedNode.Model;

            // Создаем и открываем окно свойств, передавая туда выбранный элемент
            var propertiesWindow = new System.Windows.Window
            {
                DataContext = modelNode // Задаем DataContext окна

            };
            var stackPanel = new StackPanel();
            foreach (var prop in modelNode.GetType().GetProperties())
            {
                var stackDataPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
                var label = new Label { Content = prop.Name };
                var textBox = new TextBox
                {
                    Text = prop.GetValue(modelNode)?.ToString() ?? string.Empty,
                    IsReadOnly = !prop.CanWrite // Если свойство не имеет сеттера, делаем TextBox только для чтения
                };
                // Обновляем значение свойства при изменении текста в TextBox
                // Сохраняем стандартную рамку, чтобы вернуть её при успешном вводе
                var defaultBorderBrush = textBox.BorderBrush;
                textBox.TextChanged += (s, e) =>
                                {
                                    if (!prop.CanWrite) return;

                                    try
                                    {
                                        Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                                        // Если поле пустое и свойство допускает null (например, TimeSpan? или int?)
                                        if (string.IsNullOrWhiteSpace(textBox.Text))
                                        {
                                            if (Nullable.GetUnderlyingType(prop.PropertyType) != null || targetType == typeof(string))
                                            {
                                                prop.SetValue(modelNode, null);
                                                textBox.BorderBrush = defaultBorderBrush;
                                            }
                                            return;
                                        }

                                        // Парсим значение в зависимости от типа
                                        object convertedValue = targetType switch
                                        {
                                            Type t when t == typeof(TimeSpan) => TimeSpan.Parse(textBox.Text),
                                            Type t when t == typeof(Guid) => Guid.Parse(textBox.Text),
                                            Type t when t.IsEnum => Enum.Parse(targetType, textBox.Text, ignoreCase: true),
                                            _ => Convert.ChangeType(textBox.Text, targetType, System.Globalization.CultureInfo.CurrentCulture)
                                        };

                                        prop.SetValue(modelNode, convertedValue);

                                        // Ввод корректен — возвращаем обычную рамку
                                        textBox.BorderBrush = defaultBorderBrush;
                                    }
                                    catch
                                    {
                                        // Пока пользователь печатает (например, "12:" или "-"), 
                                        // подсвечиваем рамку красным, но не роняем программу
                                        textBox.BorderBrush = System.Windows.Media.Brushes.Red;
                                    }
                                };

                stackDataPanel.Children.Add(label);
                stackDataPanel.Children.Add(textBox);
                stackPanel.Children.Add(stackDataPanel);
            }
            var buttonStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            buttonStackPanel.Children.Add(new Button
            {
                Content = "Close",
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 75,
                Command = new RelayCommand(_ => propertiesWindow.Close())
            });

            buttonStackPanel.Children.Add(new Button
            {
                Content = "Save",
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 75,

                Command = new RelayCommand(_ => SaveProperties(selectedNode))
            });
            stackPanel.Children.Add(buttonStackPanel);

            propertiesWindow.Content = stackPanel;
            propertiesWindow.ShowDialog();
        }

        //TODO Implement SaveProperties method to handle saving logic
        private void SaveProperties(NodeVm selectedNode)
        {
            throw new NotImplementedException();
        }
    }
}

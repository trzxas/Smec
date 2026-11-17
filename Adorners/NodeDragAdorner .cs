using Smec.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Smec.Adorners
{
    public class NodeDragAdorner : Adorner
    {
        private readonly ContentPresenter _contentPresenter;
        private Point _centerPosition;

        public NodeDragAdorner(UIElement adornedElement, NodeVm nodeVm, DataTemplateSelector? selector, DataTemplate? fallbackTemplate)
            : base(adornedElement)
        {
            IsHitTestVisible = false; // Обязательно! Иначе Adorner перехватит события DragOver/Drop на себя!

            _contentPresenter = new ContentPresenter
            {
                Content = nodeVm,
                ContentTemplateSelector = selector,
                ContentTemplate = selector == null ? fallbackTemplate : null,
                Opacity = 0.65
            };

            AddVisualChild(_contentPresenter);
        }

        public void SetCenterPosition(Point centerPosition)
        {
            if (_centerPosition == centerPosition)
                return;

            _centerPosition = centerPosition;
            InvalidateArrange();
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _contentPresenter;

        protected override Size MeasureOverride(Size constraint)
        {
            _contentPresenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Размещаем центр (0, 0) шаблона строго в _centerPosition!
            _contentPresenter.Arrange(new Rect(_centerPosition, _contentPresenter.DesiredSize));
            return finalSize;
        }
    }
}

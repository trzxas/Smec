using Smec.ViewModels;

namespace Smec.CustomControls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;


    public class LinkSegmentThumb : Thumb
    {
        public LinkSegmentThumb()
        {
            DragDelta += OnThumbDragDelta;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            e.Handled = true;
        }

        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is not LinkHandleVm handleVm)
                return;

            DependencyObject current = VisualTreeHelper.GetParent(this);
            LinkVm? linkVm = null;
            Canvas? parentCanvas = null;

            while (current != null)
            {
                if (linkVm == null && current is FrameworkElement fe && fe.DataContext is LinkVm vm)
                    linkVm = vm;

                if (parentCanvas == null && current is Canvas canvas && canvas.DataContext is not LinkVm)
                    parentCanvas = canvas;

                if (linkVm != null && parentCanvas != null)
                    break;

                current = VisualTreeHelper.GetParent(current);
            }

            if (linkVm != null && parentCanvas != null)
            {
                Point pos = Mouse.GetPosition(parentCanvas);
                linkVm.MoveHandleTo(handleVm.Index, pos);
                e.Handled = true;
            }
        }
    }
}



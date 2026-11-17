using Smec.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Smec.CustomControls
{
    public class LinkSegmentThumb : Thumb
    {
        public LinkSegmentThumb()
        {
            DragDelta += OnThumbDragDelta;
        }

        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is not LinkVm linkVm)
                return;

            if (VisualTreeHelper.GetParent(this) is Canvas parentCanvas)
            {
                System.Windows.Point pos = Mouse.GetPosition(parentCanvas);
                linkVm.MoveSegmentTo(pos);
                e.Handled = true;
            }
        }
    }
}


using Smec.Models;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
        public double ZoomLevel
        {
            get;
            set
            {
                double clamped = Math.Clamp(value, 0.1, 10.0);
                if (Math.Abs(field - clamped) > 0.001)
                {
                    SetProperty(ref field, clamped);

                    // Заменяем ресурс новым экземпляром ScaleTransform, если используем Zoom как ресурс из App.xaml
                    //System.Windows.Application.Current?.Resources["GlobalScaleTransform"] =
                    //        new ScaleTransform(field, field);
                }
            }
        } = 1d;

        // Методы для кнопок "+" / "-" / "Сброс" и колесика мыши.
        // Увеличиваем или уменьшаем на 10% от текущего масштаба
        public void ZoomIn() => ZoomLevel *= 1.1;
        public void ZoomOut() => ZoomLevel /= 1.1;
        public void ResetZoom() => ZoomLevel = 1.0;

        
        private const double WorkspacePadding = 300.0;
        private const double MinWorkspaceWidth = 1000.0;
        private const double MinWorkspaceHeight = 800.0;

        public double ContentWidth
        {
            get;
            private set => SetProperty(ref field, value);
        } = MinWorkspaceWidth;

        public double ContentHeight
        {
            get;
            private set => SetProperty(ref field, value);
        } = MinWorkspaceHeight;

        public void UpdateContentBounds()
        {
            double maxX = MinWorkspaceWidth;
            double maxY = MinWorkspaceHeight;

            // Считаем по визуальным компонентам в ПИКСЕЛЯХ (с учётом их габаритов!)
            foreach (var nodeVm in Components)
            {
                double rightEdge = nodeVm.PixelX + Math.Max(nodeVm.HitBoxWidth, nodeVm.HitBoxHeight) / 2.0 + WorkspacePadding;
                double bottomEdge = nodeVm.PixelY + Math.Max(nodeVm.HitBoxWidth, nodeVm.HitBoxHeight) / 2.0 + WorkspacePadding;

                if (rightEdge > maxX)
                    maxX = rightEdge;

                if (bottomEdge > maxY)
                    maxY = bottomEdge;
            }

            // Также учитываем точки изгиба проводов (если провод вытянут правее или ниже всех деталей)
            double grid = GridSettings.GridSize;
            foreach (var linkVm in Links)
            {
                foreach (var bend in linkVm.Model.Points)
                {
                    double bendX = bend.X * grid + WorkspacePadding;
                    double bendY = bend.Y * grid + WorkspacePadding;

                    if (bendX > maxX) maxX = bendX;
                    if (bendY > maxY) maxY = bendY;
                }
            }

            ContentWidth = maxX;
            ContentHeight = maxY;
        }

    }
}

using System.IO;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursor = System.Windows.Input.Cursor;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace Smec.CustomControls
{
    public class VectorCursorExtension : MarkupExtension
    {
        public Geometry? Geometry { get; set; }
        public Brush Fill { get; set; } = Brushes.LimeGreen;
        public Brush Stroke { get; set; } = Brushes.DarkGreen;
        public double StrokeThickness { get; set; } = 1.2;
        public int Size { get; set; } = 24;
        public int HotSpotX { get; set; } = 0;
        public int HotSpotY { get; set; } = 0;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Geometry == null)
                return Cursors.Arrow;

            // 1. Получаем коэффициент масштабирования DPI экрана (например, 1.0, 1.25, 1.5)
            double dpiScale = 1.0;
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                var dpi = VisualTreeHelper.GetDpi(System.Windows.Application.Current.MainWindow);
                dpiScale = dpi.DpiScaleX;
            }

            int scaledSize = (int)Math.Round(Size * dpiScale);
            int scaledHotSpotX = (int)Math.Round(HotSpotX * dpiScale);
            int scaledHotSpotY = (int)Math.Round(HotSpotY * dpiScale);

            // 2. Рендерим геометрию сразу в реальном DPI экрана (96 * dpiScale), чтобы линии были чёткими без мыла
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var pen = new Pen(Stroke, StrokeThickness) { LineJoin = PenLineJoin.Miter };
                dc.DrawGeometry(Fill, pen, Geometry);
            }

            var rtb = new RenderTargetBitmap(scaledSize, scaledSize, 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
            rtb.Render(visual);

            // 3. Передаём отмасштабированные координаты HotSpot в заголовок .cur
            return ConvertBitmapToCursor(rtb, scaledHotSpotX, scaledHotSpotY);
        }

        

        private static Cursor ConvertBitmapToCursor(BitmapSource bitmap, int hotSpotX, int hotSpotY)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Заголовок CUR
            writer.Write((short)0);           // Reserved
            writer.Write((short)2);           // Type: 2 = Cursor
            writer.Write((short)1);           // Image count: 1

            // Описание изображения
            writer.Write((byte)width);
            writer.Write((byte)height);
            writer.Write((byte)0);            // Color count
            writer.Write((byte)0);            // Reserved
            writer.Write((short)hotSpotX);    // Hotspot X
            writer.Write((short)hotSpotY);    // Hotspot Y

            int bmpInfoHeaderSize = 40;
            int xorMaskSize = pixels.Length;
            int andMaskStride = ((width + 31) / 32) * 4;
            int andMaskSize = andMaskStride * height;
            int imageSize = bmpInfoHeaderSize + xorMaskSize + andMaskSize;

            writer.Write(imageSize);
            writer.Write(22);                 // Offset to image data

            // BITMAPINFOHEADER
            writer.Write(bmpInfoHeaderSize);
            writer.Write(width);
            writer.Write(height * 2);         // Height * 2 (XOR + AND masks)
            writer.Write((short)1);           // Planes
            writer.Write((short)32);          // BitCount (BGRA)
            writer.Write(0);                  // Compression (BI_RGB)
            writer.Write(xorMaskSize + andMaskSize);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            // Пиксели в формате CUR пишутся снизу вверх
            for (int y = height - 1; y >= 0; y--)
            {
                writer.Write(pixels, y * stride, stride);
            }

            // Пустая AND-маска
            writer.Write(new byte[andMaskSize]);

            ms.Position = 0;
            return new Cursor(ms);
        }
    }
}

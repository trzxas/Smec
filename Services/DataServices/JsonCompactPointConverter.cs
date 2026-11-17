using System.Windows;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Point = System.Windows.Point;

namespace Smec.Services.DataServices
{
    public class JsonCompactPointConverter : JsonConverter<Point>
    {
        public override System.Windows.Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                var root = doc.RootElement;
                double x = root.GetProperty("X").GetDouble();
                double y = root.GetProperty("Y").GetDouble();
                return new Point(x, y);
            }
        }

        public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
        {
            // Формируем сырую компактную строку в формате JSON без пробелов и переносов.
            // Обязательно используем InvariantCulture, чтобы координаты double записались через точку, а не запятую.
            string rawJson = $"{{\"X\":{value.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"Y\":{value.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";

            // Записываем сформированную строку как сырое значение. 
            // Сериализатор вставит её в итоговый файл монолитно, полностью игнорируя правило WriteIndented!
            writer.WriteRawValue(rawJson);
        }
    }
}

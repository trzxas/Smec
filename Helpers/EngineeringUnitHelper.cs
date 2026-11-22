using System.Globalization;

namespace Smec.Helpers
{
    public static class EngineeringUnitHelper
    {
        private static readonly (double Factor, string Prefix)[] SiPrefixes =
        [
            (1e12, "T"),
            (1e9,  "G"),
            (1e6,  "M"),
            (1e3,  "k"),
            (1.0,  ""),
            (1e-3, "m"),
            (1e-6, "µ"),
            (1e-9, "n"),
            (1e-12,"p")
        ];

        /// <summary>
        /// Форматирует число в строку с приставкой СИ (например: 1000, "Ом" -> "1 кОм").
        /// </summary>
        public static string Format(double value, string unit)
        {
            if (value <= 0 || double.IsNaN(value)) return $"0 {unit}";

            foreach (var (factor, prefix) in SiPrefixes)
            {
                if (value >= factor * 0.999)
                {
                    double scaled = value / factor;
                    return $"{scaled:0.##} {prefix}{unit}";
                }
            }

            return $"{value:G3} {unit}";
        }

        /// <summary>
        /// Парсит строку с приставкой ("10k", "10 кОм", "100n", "0,91 Гц") в базовое число СИ.
        /// </summary>
        public static bool TryParse(string? text, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Оставляем только числовую часть и первую букву приставки
            string clean = text.Trim().Replace(',', '.');
            int numEnd = 0;
            while (numEnd < clean.Length && (char.IsDigit(clean[numEnd]) || clean[numEnd] == '.' || clean[numEnd] == '-'))
                numEnd++;

            if (numEnd == 0 || !double.TryParse(clean[..numEnd], NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                return false;

            string suffix = clean[numEnd..].Trim();
            double multiplier = suffix.FirstOrDefault() switch
            {
                'G' or 'Г' => 1e9,
                'M' or 'М' => 1e6,
                'k' or 'K' or 'к' or 'К' => 1e3,
                'm' => 1e-3,
                'м' when suffix.StartsWith("мк", StringComparison.OrdinalIgnoreCase) => 1e-6,
                'м' => 1e-3,
                'u' or 'µ' => 1e-6,
                'n' or 'н' or 'Н' => 1e-9,
                'p' or 'п' or 'П' => 1e-12,
                _ => 1.0
            };

            result = number * multiplier;
            return true;
        }
    }
}


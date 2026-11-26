using System.Globalization;

namespace KeyEventLogger.Models;

/// <summary>
/// Filtre kutularına yazılan tarih/saat metnini çözer.
/// Yalnızca saat yazılırsa (ör. "14:05") bugünün tarihi kullanılır.
/// </summary>
public static class TimeRangeParser
{
    private static readonly string[] DateTimeFormats =
    {
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "d.M.yyyy HH:mm:ss", "d.M.yyyy HH:mm",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm",
    };

    private static readonly string[] TimeFormats = { "HH:mm:ss", "H:mm:ss", "HH:mm", "H:mm" };

    /// <summary>
    /// Metni tarih/saate çevirir. Boş metin "sınır yok" demektir ve value null olur.
    /// </summary>
    /// <returns>Metin boşsa veya geçerli bir tarih/saatse true.</returns>
    public static bool TryParse(string? text, DateTime today, out DateTime? value)
    {
        value = null;
        string input = (text ?? string.Empty).Trim();
        if (input.Length == 0)
            return true;

        if (DateTime.TryParseExact(input, DateTimeFormats, CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out DateTime full))
        {
            value = full;
            return true;
        }

        if (DateTime.TryParseExact(input, TimeFormats, CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out DateTime timeOnly))
        {
            value = today.Date + timeOnly.TimeOfDay;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Bitiş zamanı, yazıldığı birimin sonuna kadar kapsasın: "14:05" → 14:05:59.999,
    /// "14:05:30" → 14:05:30.999. Böylece o dakika/saniye içindeki kayıtlar dışarıda kalmaz.
    /// </summary>
    public static DateTime? ExtendToEndOfUnit(DateTime? value, string? text)
    {
        if (value is not { } v)
            return null;

        int colons = (text ?? string.Empty).Count(c => c == ':');
        return colons == 1 ? v.AddMinutes(1).AddTicks(-1)
             : colons == 2 ? v.AddSeconds(1).AddTicks(-1)
             : v;
    }
}

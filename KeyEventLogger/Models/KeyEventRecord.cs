using System.Globalization;
using Avalonia.Input;

namespace KeyEventLogger.Models;

public enum KeyEventKind
{
    KeyDown,  // Tuşa basıldı
    KeyUp,    // Tuş bırakıldı
}

/// <summary>
/// Tek bir klavye olayının kaydı. Oluşturulduktan sonra değiştirilemez.
/// </summary>
public sealed class KeyEventRecord
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public KeyEventRecord(DateTime timestamp, KeyEventKind kind, Key key, string? symbol, KeyModifiers modifiers)
    {
        Timestamp = timestamp;
        Kind = kind;
        Key = key;
        Modifiers = modifiers;
        Symbol = IsVisibleSymbol(symbol) ? symbol! : string.Empty;

        // Türkçe harfler (ç, ğ, ş, ı, ö, ü) için Key değeri çoğu zaman None gelir;
        // bu durumda tuşun adı olarak üretilen karakteri kullanıyoruz.
        KeyName = key != Key.None ? key.ToString()
                : Symbol.Length > 0 ? Symbol.ToUpper(Turkish)
                : "Bilinmeyen";
    }

    public DateTime Timestamp { get; }
    public KeyEventKind Kind { get; }
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }

    /// <summary>Tuşun adı (ör. "A", "Space", "Enter", "Ş"). İstatistikler bu ada göre tutulur.</summary>
    public string KeyName { get; }

    /// <summary>Tuşun ürettiği görünür karakter (ör. "a", "1", "ş"); yoksa boş.</summary>
    public string Symbol { get; }

    public string KindText => Kind == KeyEventKind.KeyDown ? "Basıldı" : "Bırakıldı";

    public string ModifiersText =>
        Modifiers == KeyModifiers.None ? "-" : Modifiers.ToString().Replace(", ", "+");

    /// <summary>Listede gösterilen satır.</summary>
    public string DisplayText =>
        $"{Timestamp:HH:mm:ss.fff}   {KindText,-9}   {KeyName,-12}   {QuotedSymbol,-5}   {ModifiersText}";

    /// <summary>Dosyaya yazılan satır (sekme ile ayrılmış).</summary>
    public string ToLogLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{Kind}\t{KeyName}\t{Symbol}\t{ModifiersText}";

    public override string ToString() => DisplayText;

    private string QuotedSymbol => Symbol.Length > 0 ? $"'{Symbol}'" : "";

    private static bool IsVisibleSymbol(string? symbol) =>
        !string.IsNullOrEmpty(symbol) && !char.IsControl(symbol[0]) && !char.IsWhiteSpace(symbol[0]);
}

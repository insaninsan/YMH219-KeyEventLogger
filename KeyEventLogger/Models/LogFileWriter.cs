using System.Text;

namespace KeyEventLogger.Models;

/// <summary>
/// Kayıtları yerel bir .txt dosyasına yazar. Her satır bir olaydır,
/// alanlar sekme karakteri ile ayrılır.
/// </summary>
public static class LogFileWriter
{
    public static async Task WriteAsync(Stream stream, IEnumerable<KeyEventRecord> records)
    {
        // UTF-8 (BOM'suz): Türkçe karakterler her editörde doğru görünür.
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync("# YMH219 Key Event Logger kayıtları");
        await writer.WriteLineAsync($"# Oluşturulma: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await writer.WriteLineAsync("# Zaman\tOlay\tTuş\tKarakter\tDeğiştirici");

        foreach (KeyEventRecord record in records)
            await writer.WriteLineAsync(record.ToLogLine());
    }
}

using System.Collections.ObjectModel;

namespace KeyEventLogger.Models;

/// <summary>
/// Kayıt durumunu (açık/kapalı), kayıt listesini ve istatistikleri yönetir.
/// Arayüzden bağımsızdır: pencere yakaladığı olayları Record() ile buraya verir.
/// </summary>
public class KeyLogger
{
    private readonly Dictionary<string, int> keyCounts = new();

    /// <summary>Kayıtlar. ObservableCollection olduğu için ListBox eklenen satırları kendiliğinden gösterir.</summary>
    public ObservableCollection<KeyEventRecord> Records { get; } = new();

    public bool IsRecording { get; private set; }

    /// <summary>true ise tuş bırakma (KeyUp) olayları da listeye eklenir.</summary>
    public bool IncludeKeyUp { get; set; }

    /// <summary>Kaydedilen toplam tuşa basma (KeyDown) sayısı.</summary>
    public int KeyDownCount { get; private set; }

    /// <summary>Kayıt başlatıldığında veya durdurulduğunda tetiklenir.</summary>
    public event EventHandler? RecordingStateChanged;

    /// <summary>Yeni kayıt eklendiğinde veya liste temizlendiğinde tetiklenir.</summary>
    public event EventHandler? StatisticsChanged;

    public void Start()
    {
        if (IsRecording)
            return;

        IsRecording = true;
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsRecording)
            return;

        IsRecording = false;
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Kayıt açıksa olayı listeye ekler.
    /// </summary>
    /// <returns>Olay listeye eklendiyse true.</returns>
    public bool Record(KeyEventRecord record)
    {
        if (!IsRecording)
            return false;

        if (record.Kind == KeyEventKind.KeyUp && !IncludeKeyUp)
            return false;

        Records.Add(record);

        if (record.Kind == KeyEventKind.KeyDown)
        {
            KeyDownCount++;
            keyCounts[record.KeyName] = keyCounts.GetValueOrDefault(record.KeyName) + 1;
        }

        StatisticsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        Records.Clear();
        keyCounts.Clear();
        KeyDownCount = 0;
        StatisticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>En sık basılan tuşları, basılma sayısına göre azalan sırada döndürür.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> GetTopKeys(int count) =>
        keyCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Take(count)
            .ToList();
}

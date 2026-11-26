using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using KeyEventLogger.Models;

namespace KeyEventLogger;

/// <summary>
/// Ana pencere: bu pencereye gelen klavye olaylarını yakalayıp KeyLogger'a verir,
/// butonları ve kayıt göstergesini yönetir, kayıtları dosyaya kaydeder.
/// </summary>
public partial class MainWindow : Window
{
    private const int TopKeyCount = 5;

    private static readonly IBrush RecordingBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly IBrush RecordingBackground = new SolidColorBrush(Color.FromRgb(254, 226, 226));
    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
    private static readonly IBrush IdleBackground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));

    private readonly KeyLogger logger = new();

    // Etkin zaman filtresi; ikisi de null ise filtre yok
    private DateTime? filterFrom;
    private DateTime? filterTo;

    private bool IsFiltering => filterFrom is not null || filterTo is not null;

    public MainWindow()
    {
        InitializeComponent();

        RecordsList.ItemsSource = logger.Records;

        logger.RecordingStateChanged += Logger_RecordingStateChanged;
        logger.StatisticsChanged += Logger_StatisticsChanged;

        // Tunnel: tuş olayı, odaktaki kontrole ulaşmadan önce pencereye gelir.
        // Böylece hangi kontrol odakta olursa olsun olay yakalanır.
        // Not: Bu yalnızca BU pencereye gelen olayları kapsar. İşletim sistemi genelinde
        // (global hook) dinleme YAPILMAZ; pencere arka plandayken hiçbir olay gelmez.
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, Window_KeyUp, RoutingStrategies.Tunnel);

        Activated += (_, _) => UpdateStatusForFocus();
        Deactivated += (_, _) => UpdateStatusForFocus();

        UpdateRecordingUi();
        UpdateStatistics();
        RefreshList();
        SetStatus("Kayda başlamak için ▶ Başlat'a basın.");
    }

    // ===================== Klavye olayları =====================

    private void Window_KeyDown(object? sender, KeyEventArgs e) => Capture(e, KeyEventKind.KeyDown);

    private void Window_KeyUp(object? sender, KeyEventArgs e) => Capture(e, KeyEventKind.KeyUp);

    private void Capture(KeyEventArgs e, KeyEventKind kind)
    {
        // Filtre kutularına yazılan saatler tuş kaydı olarak listeye girmesin
        if (e.Source is TextBox)
            return;

        var record = new KeyEventRecord(DateTime.Now, kind, e.Key, e.KeySymbol, e.KeyModifiers);

        if (logger.Record(record))
        {
            RecordsList.ScrollIntoView(record);  // En son kayıt hep görünsün
        }
        else if (!logger.IsRecording && kind == KeyEventKind.KeyDown)
        {
            SetStatus("Kayıt kapalı — tuşlar listeye eklenmiyor. Başlamak için ▶ Başlat'a basın.");
        }
    }

    // ===================== Butonlar =====================

    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        logger.Start();
        SetStatus("Kayıt açık — bu pencere odaktayken basılan tuşlar listeleniyor.");
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        logger.Stop();
        SetStatus($"Kayıt durduruldu. Listede {logger.Records.Count} kayıt var.");
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        logger.Clear();
        SetStatus("Ekrandaki kayıtlar temizlendi.");
    }

    private void IncludeKeyUpCheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        logger.IncludeKeyUp = IncludeKeyUpCheckBox.IsChecked == true;
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        // Filtre etkinse yalnızca ekranda görünen (filtrelenmiş) kayıtlar kaydedilir
        List<KeyEventRecord> toSave = GetDisplayedRecords().ToList();
        if (toSave.Count == 0)
        {
            SetStatus("Kaydedilecek kayıt yok.", isError: true);
            return;
        }

        try
        {
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Kayıtları kaydet",
                SuggestedFileName = $"tus_kayitlari_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Metin dosyası") { Patterns = new[] { "*.txt" } },
                },
            });

            if (file is null)
            {
                SetStatus("Kaydetme iptal edildi.");
                return;
            }

            await using (Stream stream = await file.OpenWriteAsync())
            {
                await LogFileWriter.WriteAsync(stream, toSave);
            }

            SetStatus($"{toSave.Count} kayıt dosyaya yazıldı{(IsFiltering ? " (filtrelenmiş)" : "")}: {file.Name}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Dosya kaydedilemedi: {ex.Message}", isError: true);
        }
    }

    // ===================== Zaman filtresi =====================

    private void ApplyFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        DateTime today = DateTime.Today;

        if (!TimeRangeParser.TryParse(FilterFromBox.Text, today, out DateTime? from) ||
            !TimeRangeParser.TryParse(FilterToBox.Text, today, out DateTime? to))
        {
            SetStatus("Zaman biçimi anlaşılamadı. Örnek: 14:05, 14:05:30 veya 25.11.2026 14:05", isError: true);
            return;
        }

        to = TimeRangeParser.ExtendToEndOfUnit(to, FilterToBox.Text);

        if (from is null && to is null)
        {
            SetStatus("Filtrelemek için başlangıç ve/veya bitiş zamanı girin.", isError: true);
            return;
        }
        if (from > to)
        {
            SetStatus("Başlangıç zamanı bitiş zamanından sonra olamaz.", isError: true);
            return;
        }

        filterFrom = from;
        filterTo = to;
        RefreshList();
        ReleaseFilterFocus();
        SetStatus($"Filtre uygulandı: {DescribeFilter()}");
    }

    private void ClearFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        filterFrom = null;
        filterTo = null;
        FilterFromBox.Text = string.Empty;
        FilterToBox.Text = string.Empty;
        RefreshList();
        ReleaseFilterFocus();
        SetStatus("Filtre kaldırıldı; tüm kayıtlar gösteriliyor.");
    }

    private void FilterBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyFilterButton_Click(sender, e);
        }
    }

    /// <summary>
    /// Butonlar odak almadığı için filtrelemeden sonra imleç kutuda kalır ve sonraki tuşlar
    /// kutuya yazılır (kaydedilmez). Odağı kutudan alarak tuşların tekrar kaydedilmesini sağlıyoruz.
    /// </summary>
    private void ReleaseFilterFocus() => FocusManager?.ClearFocus();

    /// <summary>Listeyi filtreye göre yeniden doldurur.</summary>
    private void RefreshList()
    {
        RecordsList.ItemsSource = IsFiltering ? logger.GetRecordsBetween(filterFrom, filterTo) : logger.Records;

        int shown = GetDisplayedRecords().Count();
        FilterInfoText.Text = IsFiltering ? $"Filtre: {shown} / {logger.Records.Count} kayıt" : string.Empty;
        ClearFilterButton.IsEnabled = IsFiltering;
    }

    private IEnumerable<KeyEventRecord> GetDisplayedRecords() =>
        RecordsList.ItemsSource as IEnumerable<KeyEventRecord> ?? Enumerable.Empty<KeyEventRecord>();

    private string DescribeFilter() =>
        $"{(filterFrom is { } f ? f.ToString("dd.MM.yyyy HH:mm:ss") : "başlangıç yok")} – " +
        $"{(filterTo is { } t ? t.ToString("dd.MM.yyyy HH:mm:ss") : "bitiş yok")}";

    // ===================== Arayüz güncelleme =====================

    private void Logger_RecordingStateChanged(object? sender, EventArgs e) => UpdateRecordingUi();

    private void Logger_StatisticsChanged(object? sender, EventArgs e)
    {
        UpdateStatistics();
        if (IsFiltering)
            RefreshList();  // Yeni kayıt filtre aralığına giriyorsa listede görünsün
    }

    private void UpdateRecordingUi()
    {
        bool recording = logger.IsRecording;

        StartButton.IsEnabled = !recording;
        StopButton.IsEnabled = recording;

        IndicatorDot.Fill = recording ? RecordingBrush : IdleBrush;
        IndicatorText.Foreground = recording ? RecordingBrush : IdleBrush;
        IndicatorBorder.Background = recording ? RecordingBackground : IdleBackground;
        IndicatorText.Text = recording ? "KAYIT AÇIK" : "KAYIT KAPALI";

        Title = recording ? "● KAYITTA — YMH219 - Key Event Logger" : "YMH219 - Key Event Logger";
    }

    private void UpdateStatistics()
    {
        TotalText.Text = $"Toplam tuşa basma: {logger.KeyDownCount}";
        RecordCountText.Text = $"Listedeki kayıt: {logger.Records.Count}";

        IReadOnlyList<KeyValuePair<string, int>> topKeys = logger.GetTopKeys(TopKeyCount);
        TopKeysText.Text = topKeys.Count == 0
            ? "(henüz yok)"
            : string.Join(Environment.NewLine,
                topKeys.Select((pair, i) => $"{i + 1}. {pair.Key,-10} {pair.Value,4}"));
    }

    private void UpdateStatusForFocus()
    {
        if (!logger.IsRecording)
            return;

        if (IsActive)
            SetStatus("Kayıt açık — bu pencere odaktayken basılan tuşlar listeleniyor.");
        else
            SetStatus("Pencere arka planda — diğer uygulamalarda basılan tuşlar kaydedilmez.");
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? ErrorBrush : Brushes.Black;
    }
}

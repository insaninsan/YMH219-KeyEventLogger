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
        SetStatus("Kayda başlamak için ▶ Başlat'a basın.");
    }

    // ===================== Klavye olayları =====================

    private void Window_KeyDown(object? sender, KeyEventArgs e) => Capture(e, KeyEventKind.KeyDown);

    private void Window_KeyUp(object? sender, KeyEventArgs e) => Capture(e, KeyEventKind.KeyUp);

    private void Capture(KeyEventArgs e, KeyEventKind kind)
    {
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
        if (logger.Records.Count == 0)
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
                await LogFileWriter.WriteAsync(stream, logger.Records.ToList());
            }

            SetStatus($"{logger.Records.Count} kayıt dosyaya yazıldı: {file.Name}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Dosya kaydedilemedi: {ex.Message}", isError: true);
        }
    }

    // ===================== Arayüz güncelleme =====================

    private void Logger_RecordingStateChanged(object? sender, EventArgs e) => UpdateRecordingUi();

    private void Logger_StatisticsChanged(object? sender, EventArgs e) => UpdateStatistics();

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

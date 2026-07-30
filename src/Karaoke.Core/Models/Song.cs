using CommunityToolkit.Mvvm.ComponentModel;

namespace Karaoke.Core.Models;

public partial class Song : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AudioFilePath { get; set; } = string.Empty;
    public string LyricsFilePath { get; set; } = string.Empty;
    public string BackgroundImagePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string Category { get; set; } = "Pop / General";
    
    [ObservableProperty]
    private int _playCount = 0;
    
    [ObservableProperty]
    private bool _isFavorite = false;

    [ObservableProperty]
    private int _trackNumber = 0;

    private string _coverImagePath = string.Empty;
    public string CoverImagePath
    {
        get => !string.IsNullOrEmpty(_coverImagePath) ? _coverImagePath : (!string.IsNullOrEmpty(BackgroundImagePath) ? BackgroundImagePath : $"https://picsum.photos/seed/{Uri.EscapeDataString($"{Artist}-{Title}")}/120/120");
        set => _coverImagePath = value;
    }
    
    public string DisplayName => $"{Artist} - {Title}";
}

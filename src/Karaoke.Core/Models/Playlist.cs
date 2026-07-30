namespace Karaoke.Core.Models;

public class Playlist
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string CoverImagePath { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int SongCount { get; set; } = 50;
}

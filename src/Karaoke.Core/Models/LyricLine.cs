namespace Karaoke.Core.Models;

public class LyricWord
{
    public string Text { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class LyricLine
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<LyricWord> Words { get; set; } = new();
    
    public bool IsCurrent(TimeSpan currentPosition)
    {
        return currentPosition >= StartTime && currentPosition <= EndTime;
    }
}

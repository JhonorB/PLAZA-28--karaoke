using Karaoke.Core.Models;

namespace Karaoke.Core.Interfaces;

public interface ILyricsParserService
{
    List<LyricLine> ParseLrc(string lrcContent);
    List<LyricLine> ParseLrcFile(string filePath);
    List<LyricLine> GenerateDummyLyrics(TimeSpan duration, string songTitle);
    Task<List<LyricLine>> FetchOrGenerateLyricsAsync(string songTitle, string artist, TimeSpan duration);
    void EnsureIntroTitle(List<LyricLine> lyrics, string songTitle);
}

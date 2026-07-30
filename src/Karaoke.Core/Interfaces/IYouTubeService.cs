using Karaoke.Core.Models;

namespace Karaoke.Core.Interfaces;

public interface IYouTubeService
{
    void SetApiKey(string apiKey);
    bool HasApiKey { get; }
    Task<List<Song>> SearchVideosAsync(string query, bool isInstrumental = false, int maxResults = 10);
    Task<string> GetAudioStreamUrlAsync(string videoIdOrUrl);
    Task<Song?> GetVideoInfoFromUrlAsync(string url);
    Task<List<LyricLine>?> GetClosedCaptionsAsync(string videoUrl);
}

using Karaoke.Core.Models;

namespace Karaoke.Core.Interfaces;

public interface ISongCatalogService
{
    Task<List<Song>> GetAllSongsAsync();
    Task<Song?> GetSongByIdAsync(string id);
    Task<List<Song>> SearchSongsAsync(string query);
    Task<List<Song>> GetGenreSongsAsync(string genre);
    Task<List<Song>> RefreshGenreSongsAsync(string genre);
    Task<List<Song>> GetGenreSongsByPageAsync(string genre, int page);
    Task<List<Song>> GetTopPlayedSongsAsync();
    Task<List<Song>> GetFavoriteSongsAsync();
    Task AddSongAsync(Song song);
    Task UpdateSongAsync(Song song);
    Task DeleteSongAsync(string id);
    Task InitializeDefaultCatalogAsync();
    void SetYouTubeService(IYouTubeService youTubeService);
}

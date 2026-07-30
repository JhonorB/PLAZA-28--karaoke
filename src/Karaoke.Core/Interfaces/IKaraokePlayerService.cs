using Karaoke.Core.Models;

namespace Karaoke.Core.Interfaces;

public enum PlayerState
{
    Stopped,
    Playing,
    Paused
}

public interface IKaraokePlayerService
{
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<PlayerState>? StateChanged;
    event EventHandler<Song?>? SongLoaded;

    PlayerState CurrentState { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan Duration { get; }
    Song? CurrentSong { get; }

    void LoadSong(Song song);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void UpdatePosition(TimeSpan position);
    void SetVolume(int volume);
}

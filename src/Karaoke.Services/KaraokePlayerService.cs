using System.Timers;
using Karaoke.Core.Interfaces;
using Karaoke.Core.Models;

namespace Karaoke.Services;

public class KaraokePlayerService : IKaraokePlayerService, IDisposable
{
    private readonly System.Timers.Timer _timer;
    private PlayerState _currentState = PlayerState.Stopped;
    private TimeSpan _currentPosition = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;
    private Song? _currentSong;
    private DateTime _lastTick;

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlayerState>? StateChanged;
    public event EventHandler<Song?>? SongLoaded;

    public PlayerState CurrentState => _currentState;
    public TimeSpan CurrentPosition => _currentPosition;
    public TimeSpan Duration => _duration;
    public Song? CurrentSong => _currentSong;

    public KaraokePlayerService()
    {
        _timer = new System.Timers.Timer(33); // ~30 FPS for smooth lyrics sweep
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_currentState == PlayerState.Playing)
        {
            var now = DateTime.UtcNow;
            var delta = now - _lastTick;
            _lastTick = now;

            _currentPosition = _currentPosition.Add(delta);

            if (_currentPosition >= _duration && _duration > TimeSpan.Zero)
            {
                Stop();
            }
            else
            {
                PositionChanged?.Invoke(this, _currentPosition);
            }
        }
    }

    public void LoadSong(Song song)
    {
        Stop();
        _currentSong = song;
        _duration = song.Duration;
        _currentPosition = TimeSpan.Zero;
        SongLoaded?.Invoke(this, _currentSong);
        PositionChanged?.Invoke(this, _currentPosition);
    }

    public void Play()
    {
        if (_currentSong == null) return;

        if (_currentState != PlayerState.Playing)
        {
            _currentState = PlayerState.Playing;
            _lastTick = DateTime.UtcNow;
            _timer.Start();
            StateChanged?.Invoke(this, _currentState);
        }
    }

    public void Pause()
    {
        if (_currentState == PlayerState.Playing)
        {
            _currentState = PlayerState.Paused;
            _timer.Stop();
            StateChanged?.Invoke(this, _currentState);
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _currentState = PlayerState.Stopped;
        _currentPosition = TimeSpan.Zero;
        StateChanged?.Invoke(this, _currentState);
        PositionChanged?.Invoke(this, _currentPosition);
    }

    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > _duration) position = _duration;

        _currentPosition = position;
        _lastTick = DateTime.UtcNow;
        PositionChanged?.Invoke(this, _currentPosition);
    }

    /// <summary>
    /// Actualiza la posición interna silenciosamente (sin disparar eventos),
    /// usado cuando el MediaElement ya es la fuente de verdad de la posición.
    /// </summary>
    public void UpdatePosition(TimeSpan position)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > _duration) position = _duration;
        _currentPosition = position;
        _lastTick = DateTime.UtcNow;
    }

    public void SetVolume(int volume)
    {
        // Will be hooked by UI MediaElement / LibVLC audio sink
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

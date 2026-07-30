using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Karaoke.Core.Interfaces;
using Karaoke.Core.Models;
using Karaoke.Data.Services;
using Karaoke.Services;
using Karaoke.Desktop.Views;

namespace Karaoke.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IKaraokePlayerService _playerService;
    private readonly ILyricsParserService _lyricsService;
    private readonly ISongCatalogService _catalogService;
    private readonly IYouTubeService _youTubeService;
    private readonly Dispatcher _dispatcher;

    private List<LyricLine> _currentLyrics = new();
    private List<Song> _sessionHistory = new();

    [ObservableProperty]
    private ObservableCollection<Song> _songs = new();

    [ObservableProperty]
    private ObservableCollection<Playlist> _playlists = new();

    [ObservableProperty]
    private bool _isPlaylistsView = true;

    [ObservableProperty]
    private string _currentViewTitle = "✨ Playlists & Descubrir";

    [ObservableProperty]
    private Song? _selectedSong;

    [ObservableProperty]
    private string _backgroundImagePath = "Fondo.png";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _currentLyricText = "🎵 Selecciona una canción o dale Play 🎵";

    [ObservableProperty]
    private string _nextLyricText = "El sistema está listo para cantar";

    [ObservableProperty]
    private double _lyricProgress = 0.0;

    [ObservableProperty]
    private double _lineDurationMs = 3000.0;

    [ObservableProperty]
    private string _timeDisplay = "00:00 / 00:00";

    [ObservableProperty]
    private double _currentPositionSeconds = 0;

    [ObservableProperty]
    private double _totalDurationSeconds = 100;

    [ObservableProperty]
    private bool _isPlaying = false;

    [ObservableProperty]
    private string _statusMessage = "Sistema Comercial Listo | Fondo Predeterminado: Fondo.png";

    [ObservableProperty]
    private bool _isInstrumentalFilter = false;

    [ObservableProperty]
    private bool _isVocalFilter = true;

    [ObservableProperty]
    private bool _isLooping = false;

    [ObservableProperty]
    private bool _isMuted = false;

    [ObservableProperty]
    private double _volume = 0.8;

    [ObservableProperty]
    private string _selectedNavTab = "Playlists";

    [ObservableProperty]
    private string _currentGenre = "";

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 100;

    [ObservableProperty]
    private int _pageBtn1Val = 1;

    [ObservableProperty]
    private int _pageBtn2Val = 2;

    [ObservableProperty]
    private int _pageBtn3Val = 3;

    [ObservableProperty]
    private int _pageBtn4Val = 4;

    [ObservableProperty]
    private int _pageBtn5Val = 5;

    [ObservableProperty]
    private bool _isPageBtn1Active = true;

    [ObservableProperty]
    private bool _isPageBtn2Active = false;

    [ObservableProperty]
    private bool _isPageBtn3Active = false;

    [ObservableProperty]
    private bool _isPageBtn4Active = false;

    [ObservableProperty]
    private bool _isPageBtn5Active = false;

    [ObservableProperty]
    private bool _isPageBtn1Visible = true;

    [ObservableProperty]
    private bool _isPageBtn2Visible = false;

    [ObservableProperty]
    private bool _isPageBtn3Visible = false;

    [ObservableProperty]
    private bool _isPageBtn4Visible = false;

    [ObservableProperty]
    private bool _isPageBtn5Visible = false;

    [ObservableProperty]
    private bool _isMultiPage = false;

    private List<Song> _currentDataset = new();
    private bool _suppressAutoPlay = true;

    [ObservableProperty]
    private bool _isVideoPopupOpen = false;

    [RelayCommand]
    private void ToggleVideoPopup()
    {
        IsVideoPopupOpen = !IsVideoPopupOpen;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleStatusText))]
    private bool _isSubtitlesEnabled = false;

    public string SubtitleStatusText => IsSubtitlesEnabled ? "Subtítulos: ON" : "Subtítulos: OFF";

    [RelayCommand]
    private void ToggleSubtitles()
    {
        IsSubtitlesEnabled = !IsSubtitlesEnabled;
        StatusMessage = IsSubtitlesEnabled ? "📝 Subtítulos sobre el video: ACTIVADOS." : "📝 Subtítulos sobre el video: DESACTIVADOS.";
    }

    [ObservableProperty]
    private string _logoImagePath = "";

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _playerService = new KaraokePlayerService();
        _lyricsService = new LyricsParserService();
        _catalogService = new SongCatalogService();
        _youTubeService = new YouTubeService();
        _catalogService.SetYouTubeService(_youTubeService);

        _playerService.PositionChanged += OnPositionChanged;
        _playerService.StateChanged += OnStateChanged;

        // Verify default Fondo.png exists
        if (File.Exists("Fondo.png"))
        {
            BackgroundImagePath = Path.GetFullPath("Fondo.png");
        }
        else if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fondo.png")))
        {
            BackgroundImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fondo.png");
        }
        
        string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Playlists", "logoplaza28.png");
        if (File.Exists(logoPath))
        {
            LogoImagePath = logoPath;
        }

        InitializePlaylists();
        UpdatePageButtons();
        _ = LoadCatalogAsync();
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdatePageButtons();
    }

    private void UpdatePageButtons()
    {
        int startPage = ((CurrentPage - 1) / 5) * 5 + 1;
        PageBtn1Val = startPage;
        PageBtn2Val = startPage + 1;
        PageBtn3Val = startPage + 2;
        PageBtn4Val = startPage + 3;
        PageBtn5Val = startPage + 4;

        IsPageBtn1Visible = PageBtn1Val <= TotalPages;
        IsPageBtn2Visible = PageBtn2Val <= TotalPages;
        IsPageBtn3Visible = PageBtn3Val <= TotalPages;
        IsPageBtn4Visible = PageBtn4Val <= TotalPages;
        IsPageBtn5Visible = PageBtn5Val <= TotalPages;

        IsMultiPage = TotalPages > 1;

        IsPageBtn1Active = CurrentPage == PageBtn1Val;
        IsPageBtn2Active = CurrentPage == PageBtn2Val;
        IsPageBtn3Active = CurrentPage == PageBtn3Val;
        IsPageBtn4Active = CurrentPage == PageBtn4Val;
        IsPageBtn5Active = CurrentPage == PageBtn5Val;
    }

    private void InitializePlaylists()
    {
        Playlists.Clear();
        Playlists.Add(new Playlist { Title = "🔥 Tendencias Actuales", Subtitle = "Los hits número 1 del momento y música de moda", Genre = "🔥 Tendencias Actuales", CoverImagePath = GetAssetPath("pop.png") });
        Playlists.Add(new Playlist { Title = "Rock Clásico", Subtitle = "Himnos del rock en español e inglés", Genre = "Rock Clásico", CoverImagePath = GetAssetPath("rock.png") });
        Playlists.Add(new Playlist { Title = "Pop Divas", Subtitle = "Éxitos de las reinas del pop actual", Genre = "Pop Divas", CoverImagePath = GetAssetPath("pop.png") });
        Playlists.Add(new Playlist { Title = "Baladas Románticas", Subtitle = "Baladas inolvidables para cantar al amor", Genre = "Baladas Románticas", CoverImagePath = GetAssetPath("baladas.png") });
        Playlists.Add(new Playlist { Title = "Éxitos de los 80s", Subtitle = "Clásicos retro y synthwave de oro", Genre = "Éxitos 80s", CoverImagePath = GetAssetPath("80s.png") });
        Playlists.Add(new Playlist { Title = "Reggaetón Fiesta", Subtitle = "Temazos urbanos y latinos para bailar", Genre = "Reggaetón Fiesta", CoverImagePath = GetAssetPath("reggaeton.png") });
        Playlists.Add(new Playlist { Title = "Salsa & Cumbia", Subtitle = "Éxitos tropicales para poner ambiente", Genre = "Salsa y Cumbia", CoverImagePath = GetAssetPath("salsa.png") });
    }

    private string GetAssetPath(string filename)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(baseDir, "Assets", "Playlists", filename);
        if (File.Exists(path)) return path;
        var devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\Assets\Playlists", filename));
        if (File.Exists(devPath)) return devPath;
        return filename;
    }

    private async Task LoadCatalogAsync()
    {
        await _catalogService.InitializeDefaultCatalogAsync();
        var catalog = await _catalogService.GetAllSongsAsync();
        
        if (!catalog.Any())
        {
            StatusMessage = "🔥 Cargando éxitos populares en línea desde YouTube...";
            var ytResults = await _youTubeService.SearchVideosAsync("Karaoke Éxitos Populares", IsInstrumentalFilter);
            PopulateSongsList(ytResults, 1);
            StatusMessage = $"🔥 Listo: Explorando éxitos populares de Karaoke en YouTube.";
            return;
        }

        PopulateSongsList(catalog, 1);
        StatusMessage = "✨ Sistema de Karaoke listo. Selecciona o dale Play a una canción.";
    }

    private void PopulateSongsList(IEnumerable<Song> newSongs, int startTrackNumber = 1, int? explicitTotalPages = null, bool isNewDataset = true)
    {
        _dispatcher.Invoke(() =>
        {
            if (isNewDataset)
            {
                _currentDataset = newSongs.ToList();
                CurrentPage = 1;
            }

            int count = _currentDataset.Count;
            if (explicitTotalPages.HasValue)
            {
                TotalPages = explicitTotalPages.Value;
            }
            else
            {
                TotalPages = count <= 150 ? 1 : (int)Math.Ceiling(count / 150.0);
            }
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            int skip = (CurrentPage - 1) * 150;
            var pageSlice = _currentDataset.Skip(skip).Take(150).ToList();

            Songs.Clear();
            int index = skip + 1;
            foreach (var s in pageSlice)
            {
                s.TrackNumber = index++;
                Songs.Add(s);
            }
            if (Songs.Any() && SelectedSong == null)
            {
                SelectedSong = Songs.First();
            }
            _suppressAutoPlay = false;
            UpdatePageButtons();
        });
    }

    [RelayCommand]
    private async Task SelectVocalFilterAsync()
    {
        IsVocalFilter = true;
        IsInstrumentalFilter = false;
        StatusMessage = "🎤 Filtro activo: Música con Voz + Letras Oficiales.";
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            await SearchAsync();
        }
    }

    [RelayCommand]
    private async Task SelectInstrumentalFilterAsync()
    {
        IsVocalFilter = false;
        IsInstrumentalFilter = true;
        StatusMessage = "🎹 Filtro activo: Instrumental sin Voz + Letras de Karaoke.";
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            await SearchAsync();
        }
    }

    [RelayCommand]
    private void ShowPlaylists()
    {
        SelectedNavTab = "Playlists";
        IsPlaylistsView = true;
        CurrentViewTitle = "✨ Playlists & Descubrir";
        StatusMessage = "⚡ Selecciona una playlist para comenzar a cantar.";
    }

    [RelayCommand]
    private void CreatePlaylist()
    {
        var dialog = new PlaylistDialog();
        if (dialog.ShowDialog() == true)
        {
            var newPl = new Playlist
            {
                Title = dialog.PlaylistTitle,
                Subtitle = string.IsNullOrWhiteSpace(dialog.PlaylistSubtitle) ? "🟢 Playlist personalizada" : dialog.PlaylistSubtitle,
                Genre = dialog.PlaylistTitle,
                CoverImagePath = string.IsNullOrWhiteSpace(dialog.PlaylistImagePath) ? GetAssetPath("rock.png") : dialog.PlaylistImagePath
            };
            Playlists.Insert(0, newPl);
            
            SelectedNavTab = "Playlists";
            IsPlaylistsView = true;
            CurrentViewTitle = "✨ Playlists & Descubrir";
            StatusMessage = $"✨ ¡Nueva playlist '{newPl.Title}' creada con éxito!";
        }
    }

    [RelayCommand]
    private void EditPlaylist(Playlist playlist)
    {
        if (playlist == null) return;
        var dialog = new PlaylistDialog(playlist.Title, playlist.Subtitle, playlist.CoverImagePath);
        if (dialog.ShowDialog() == true)
        {
            playlist.Title = dialog.PlaylistTitle;
            playlist.Subtitle = dialog.PlaylistSubtitle;
            playlist.Genre = dialog.PlaylistTitle;
            if (!string.IsNullOrWhiteSpace(dialog.PlaylistImagePath))
            {
                playlist.CoverImagePath = dialog.PlaylistImagePath;
            }
            StatusMessage = $"✏️ Playlist '{playlist.Title}' editada con éxito.";
            
            // Refrescar UI (Hack para forzar actualización)
            var index = Playlists.IndexOf(playlist);
            Playlists.RemoveAt(index);
            Playlists.Insert(index, playlist);
        }
    }

    [RelayCommand]
    private void DeletePlaylist(Playlist playlist)
    {
        if (playlist == null) return;
        var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar la playlist '{playlist.Title}'?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            Playlists.Remove(playlist);
            StatusMessage = $"🗑️ Playlist eliminada con éxito.";
        }
    }

    [RelayCommand]
    private async Task ShowFavoritesAsync()
    {
        SelectedNavTab = "Favorites";
        IsPlaylistsView = false;
        CurrentPage = 1;
        CurrentGenre = "";
        CurrentViewTitle = "❤️ Mis Canciones Favoritas";
        StatusMessage = "🔥 Cargando tus canciones favoritas...";
        var favs = await _catalogService.GetFavoriteSongsAsync();
        PopulateSongsList(favs, 1);
        StatusMessage = $"✨ {favs.Count} canciones favoritas en tu lista.";
    }

    [RelayCommand]
    private async Task ShowTopPlayedAsync()
    {
        SelectedNavTab = "TopPlayed";
        IsPlaylistsView = false;
        CurrentPage = 1;
        CurrentGenre = "";
        CurrentViewTitle = "🏆 Top Más Cantadas (Ranking de Éxitos)";
        StatusMessage = "🔥 Ejecutando algoritmo de ranking basado en reproducciones...";
        var top = await _catalogService.GetTopPlayedSongsAsync();
        PopulateSongsList(top, 1);
        StatusMessage = $"✨ Ranking calculado: Top {top.Count} canciones más reproducidas en el sistema.";
    }

    [RelayCommand]
    private async Task SelectNavAsync(string? tabName)
    {
        if (string.IsNullOrEmpty(tabName)) return;
        SelectedNavTab = tabName;
        if (tabName == "Search")
        {
            SearchQuery = "";
            await SearchAsync();
            CurrentViewTitle = "🔍 Búsqueda Rápida de Canciones";
        }
        else if (tabName == "Discover")
        {
            SelectedNavTab = "Discover";
            CurrentViewTitle = "🎲 Música Aleatoria (Sorpresa del Catálogo)";
            StatusMessage = "🎲 Seleccionando una canción sorpresa aleatoria del catálogo...";
            var all = await _catalogService.GetAllSongsAsync();
            if (all.Any())
            {
                var randomSong = all[new Random().Next(all.Count)];
                SelectedSong = randomSong;
                PopulateSongsList(new List<Song> { randomSong }, 1);
                StatusMessage = $"🎲 Música Aleatoria: ¡Sorpresa! Reproduciendo '{randomSong.Title} - {randomSong.Artist}'";
                if (PlaySongCommand.CanExecute(randomSong))
                {
                    PlaySongCommand.Execute(randomSong);
                }
            }
        }
        else if (tabName == "Playlists")
        {
            ShowPlaylists();
            SelectedNavTab = "Playlists";
            CurrentViewTitle = "📁 Playlists Destacadas";
        }
        else if (tabName == "TopPlayed")
        {
            await ShowTopPlayedAsync();
            SelectedNavTab = "TopPlayed";
        }
        else if (tabName == "AllGenres")
        {
            ShowPlaylists();
            SelectedNavTab = "AllGenres";
            CurrentViewTitle = "🎵 Todos los Géneros Musicales";
        }
        else if (tabName == "Favorites")
        {
            await ShowFavoritesAsync();
            SelectedNavTab = "Favorites";
        }
        else if (tabName == "History")
        {
            ShowSessionHistory();
            SelectedNavTab = "History";
            CurrentViewTitle = "🕒 Historial de Sesión (Recientes)";
        }
    }

    private void ShowSessionHistory()
    {
        IsPlaylistsView = false;
        CurrentPage = 1;
        CurrentGenre = "";
        StatusMessage = "🕒 Cargando tu historial de la sesión actual...";
        PopulateSongsList(_sessionHistory.ToList(), 1);
        StatusMessage = _sessionHistory.Any() ? $"✨ Mostrando {_sessionHistory.Count} canciones de tu sesión." : "🕒 El historial está vacío. ¡Dale Play a alguna canción!";
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Song? song)
    {
        if (song == null) return;
        song.IsFavorite = !song.IsFavorite;
        await _catalogService.UpdateSongAsync(song);
        StatusMessage = song.IsFavorite ? $"❤️ '{song.Title}' añadida a tus Favoritos." : $"🤍 '{song.Title}' eliminada de tus Favoritos.";
        if (CurrentViewTitle.Contains("FAVORITAS") && !song.IsFavorite)
        {
            _dispatcher.Invoke(() => Songs.Remove(song));
        }
    }

    [RelayCommand]
    private async Task SelectPlaylistAsync(Playlist? playlist)
    {
        if (playlist == null) return;
        await FilterGenreAsync(playlist.Genre);
    }

    [RelayCommand]
    private async Task FilterGenreAsync(string genre)
    {
        CurrentGenre = genre;
        SearchQuery = genre;
        IsPlaylistsView = false;
        CurrentViewTitle = $"📁 Playlist: {genre}";
        StatusMessage = $"🔥 Cargando éxitos de la playlist '{genre}'...";
        
        CurrentPage = 1;
        var genreSongs = await _catalogService.GetGenreSongsAsync(genre);
        PopulateSongsList(genreSongs, 1);
        StatusMessage = $"✨ Éxitos cargados en la playlist '{genre}'. ¡Haz clic para reproducir!";
    }

    [RelayCommand]
    private async Task SelectPageAsync(object? pageParamObj)
    {
        if (pageParamObj == null) return;
        string pageParam = pageParamObj.ToString() ?? "";

        int targetPage = CurrentPage;
        if (pageParam == "PREV")
        {
            targetPage = Math.Max(1, CurrentPage - 1);
        }
        else if (pageParam == "NEXT")
        {
            targetPage = Math.Min(TotalPages, CurrentPage + 1);
        }
        else if (int.TryParse(pageParam, out int pageNum))
        {
            targetPage = Math.Clamp(pageNum, 1, TotalPages);
        }

        if (targetPage == CurrentPage && pageParam != "PREV" && pageParam != "NEXT") return;

        CurrentPage = targetPage;
        PopulateSongsList(_currentDataset, (CurrentPage - 1) * 150 + 1, null, false);
        StatusMessage = $"✨ Página {CurrentPage} de {TotalPages} cargada ({Songs.Count} éxitos en pantalla).";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RefreshGenreListAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentGenre)) return;
        
        int nextPage = CurrentPage < TotalPages ? CurrentPage + 1 : 1;
        await SelectPageAsync(nextPage.ToString());
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsPlaylistsView = false;
        CurrentPage = 1;
        CurrentGenre = "";
        
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            CurrentViewTitle = "✨ Catálogo Completo";
            await LoadCatalogAsync();
            StatusMessage = "Mostrando todo el catálogo local.";
            return;
        }

        if (SearchQuery.Contains("http://") || SearchQuery.Contains("https://") || SearchQuery.Contains("www.") || SearchQuery.Contains("youtube.com") || SearchQuery.Contains("youtu.be"))
        {
            CurrentViewTitle = "🔗 Video Importado (Enlace YouTube / MP3)";
        }
        else
        {
            CurrentViewTitle = $"🔍 Resultados para: \"{SearchQuery}\"";
        }

        // --- Intercepción de Enlaces Directos de YouTube ---
        if (SearchQuery.Contains("youtube.com/watch") || SearchQuery.Contains("youtu.be/"))
        {
            StatusMessage = "🔗 Enlace de YouTube detectado. Extrayendo metadatos del video...";
            var urlSong = await _youTubeService.GetVideoInfoFromUrlAsync(SearchQuery);
            if (urlSong != null)
            {
                PopulateSongsList(new List<Song> { urlSong }, 1);
                StatusMessage = $"✅ Video importado directamente: {urlSong.DisplayName} ({urlSong.Category})";
                return;
            }
        }
        // ---------------------------------------------------

        StatusMessage = $"🔍 Buscando '{SearchQuery}' (Filtro: {(IsInstrumentalFilter ? "Instrumental + Letra" : "Música + Letra")})...";

        var localResults = await _catalogService.SearchSongsAsync(SearchQuery);
        var ytResults = await _youTubeService.SearchVideosAsync(SearchQuery, IsInstrumentalFilter);

        var combined = new List<Song>(localResults);
        combined.AddRange(ytResults);
        PopulateSongsList(combined, 1);

        StatusMessage = $"Encontrados {localResults.Count} en local y {ytResults.Count} en YouTube para '{SearchQuery}'.";
    }

    [RelayCommand]
    private async Task PasteAndSearchAsync()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SearchQuery = text.Trim();
                    await SearchAsync();
                }
            }
            else
            {
                StatusMessage = "⚠️ El portapapeles no contiene texto válido para buscar.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠️ Error al acceder al portapapeles: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        StatusMessage = "🧹 Búsqueda limpia.";
    }

    partial void OnSelectedSongChanged(Song? value)
    {
        if (value != null)
        {
            _playerService.LoadSong(value);
            TotalDurationSeconds = value.Duration.TotalSeconds;
            
            if (!string.IsNullOrEmpty(value.LyricsFilePath) && File.Exists(value.LyricsFilePath))
            {
                _currentLyrics = _lyricsService.ParseLrcFile(value.LyricsFilePath);
            }
            else
            {
                _currentLyrics = _lyricsService.GenerateDummyLyrics(value.Duration, value.Title);
                _ = LoadRealLyricsAsync(value);
            }

            if (_currentLyrics.Any())
            {
                CurrentLyricText = _currentLyrics.First().Text;
                NextLyricText = _currentLyrics.Count > 1 ? _currentLyrics[1].Text : string.Empty;
            }

            if (!string.IsNullOrEmpty(value.BackgroundImagePath) && File.Exists(value.BackgroundImagePath))
            {
                BackgroundImagePath = value.BackgroundImagePath;
            }

            if (!_suppressAutoPlay)
            {
                StatusMessage = $"⚡ Preparando para cantar: {value.DisplayName}...";
                _ = StartSongWithCountdownAsync(value);
            }
            else
            {
                StatusMessage = $"✨ Listo para cantar: {value.DisplayName} (Presiona Play ▶️ en los controles para iniciar)";
            }
        }
    }

    private async Task StartSongWithCountdownAsync(Song value)
    {
        // Detener reproducción previa si estuviera sonando
        _playerService.Stop();

        // 1. Conteo regresivo: 3, 2, 1 (en dorado reluciente, 1 segundo por número)
        for (int i = 3; i > 0; i--)
        {
            _dispatcher.Invoke(() => 
            {
                CurrentLyricText = i.ToString();
                NextLyricText = string.Empty;
                LyricProgress = 1.0; // 1.0 para que KaraokeLyricsControl lo muestre en color dorado brillante
            });
            await Task.Delay(1000);
        }

        // 2. Título en dorado exactamente por 1.3 segundos (1300 ms)
        _dispatcher.Invoke(() =>
        {
            CurrentLyricText = value.DisplayName;
            NextLyricText = string.Empty;
            LyricProgress = 1.0; // 1.0 para mantener el texto en dorado
        });
        await Task.Delay(1300);

        // 3. Desaparece el nombre
        _dispatcher.Invoke(() =>
        {
            CurrentLyricText = string.Empty;
            NextLyricText = string.Empty;
            LyricProgress = 0.0;
        });
        await Task.Delay(250); // Breve pausa tras desaparecer el título antes de empezar la canción

        // 4. Iniciar reproducción de la canción
        _dispatcher.Invoke(() =>
        {
            if (SelectedSong == value) // Solo reproducir si el usuario no cambió de canción durante el conteo
            {
                if (_currentLyrics.Any())
                {
                    CurrentLyricText = _currentLyrics.First().Text;
                    NextLyricText = _currentLyrics.Count > 1 ? _currentLyrics[1].Text : string.Empty;
                }
                else
                {
                    CurrentLyricText = string.Empty;
                    NextLyricText = string.Empty;
                }
                _playerService.Play();
            }
        });
    }

    private async Task LoadRealLyricsAsync(Song song)
    {
        try
        {
            List<LyricLine>? lyrics = null;
            
            // Si es un enlace directo de YouTube, intentamos extraer Subtítulos (CC)
            bool isYouTubeUrl = !string.IsNullOrWhiteSpace(song.AudioFilePath) && 
                                (song.AudioFilePath.Contains("youtube.com") || song.AudioFilePath.Contains("youtu.be"));
                                
            if (isYouTubeUrl)
            {
                StatusMessage = "⏳ Buscando Subtítulos (CC) originales de YouTube...";
                lyrics = await _youTubeService.GetClosedCaptionsAsync(song.AudioFilePath);
            }
            
            // Si no hay CC disponibles o aplicables, procedemos con el LyricsParserService
            if (lyrics == null || lyrics.Count == 0)
            {
                StatusMessage = $"⏳ Buscando/Generando letras para: {song.DisplayName}...";
                lyrics = await _lyricsService.FetchOrGenerateLyricsAsync(song.Title, song.Artist, song.Duration);
            }
            
            if (lyrics != null && SelectedSong == song)
            {
                if (lyrics.Count > 0)
                {
                    _lyricsService.EnsureIntroTitle(lyrics, song.DisplayName);
                }

                _dispatcher.Invoke(() =>
                {
                    _currentLyrics = lyrics;
                    if (_currentLyrics.Any() && IsPlaying)
                    {
                        var first = _currentLyrics.First();
                        if (first.StartTime <= TimeSpan.FromSeconds(2))
                        {
                            CurrentLyricText = first.Text;
                            NextLyricText = _currentLyrics.Count > 1 ? _currentLyrics[1].Text : string.Empty;
                        }
                        else
                        {
                            CurrentLyricText = string.Empty;
                            NextLyricText = first.Text;
                        }
                        StatusMessage = $"✨ Letras sincronizadas correctamente: {song.DisplayName}";
                    }
                    else
                    {
                        CurrentLyricText = string.Empty;
                        NextLyricText = string.Empty;
                        StatusMessage = $"🎵 Reproduciendo sin subtítulos (pista instrumental o sin letra oficial): {song.DisplayName}";
                    }
                });
            }
        }
        catch { }
    }

    public async Task<string> GetYouTubeStreamUrlAsync(string videoUrl)
    {
        return await _youTubeService.GetAudioStreamUrlAsync(videoUrl);
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        _dispatcher.Invoke(() =>
        {
            CurrentPositionSeconds = position.TotalSeconds;
            TimeDisplay = $"{position:mm\\:ss} / {_playerService.Duration:mm\\:ss}";

            // Compensación de alta precisión (~80ms) para latencia de pantalla a 60 FPS
            var syncPosition = position.Add(TimeSpan.FromMilliseconds(80));

            var activeLineIndex = _currentLyrics.FindIndex(l => l.IsCurrent(syncPosition));
            if (activeLineIndex >= 0)
            {
                var activeLine = _currentLyrics[activeLineIndex];
                CurrentLyricText = activeLine.Text;
                NextLyricText = activeLineIndex + 1 < _currentLyrics.Count ? _currentLyrics[activeLineIndex + 1].Text : "";

                var lineDuration = (activeLine.EndTime - activeLine.StartTime).TotalMilliseconds;
                if (lineDuration > 0)
                {
                    var elapsed = (syncPosition - activeLine.StartTime).TotalMilliseconds;
                    LyricProgress = Math.Clamp(elapsed / lineDuration, 0.0, 1.0);
                }
            }
            else
            {
                var upcoming = _currentLyrics.FirstOrDefault(l => l.StartTime > syncPosition);
                if (upcoming != null && (upcoming.StartTime - syncPosition).TotalSeconds < 3)
                {
                    NextLyricText = upcoming.Text;
                }
            }
        });
    }

    private int _lastActiveLyricIndex = -1;

    public void SyncPositionFromMediaElement(TimeSpan realPosition)
    {
        CurrentPositionSeconds = realPosition.TotalSeconds;
        TimeDisplay = $"{realPosition:mm\\:ss} / {_playerService.Duration:mm\\:ss}";

        // Compensación de alta precisión (~80ms) para sincronizar al ritmo exacto de la voz en canciones rápidas (Rap/Reggaetón)
        var effectivePos = realPosition.Add(TimeSpan.FromMilliseconds(80));

        var activeLineIndex = _currentLyrics.FindIndex(l => l.IsCurrent(effectivePos));
        if (activeLineIndex >= 0)
        {
            var activeLine = _currentLyrics[activeLineIndex];

            // Solo actualizamos texto si cambió la línea activa
            if (activeLineIndex != _lastActiveLyricIndex)
            {
                _lastActiveLyricIndex = activeLineIndex;
                CurrentLyricText = activeLine.Text;
                NextLyricText = activeLineIndex + 1 < _currentLyrics.Count ? _currentLyrics[activeLineIndex + 1].Text : "";
            }

            // Algoritmo Palabra por Palabra (Iluminación limpia por palabra completa para máxima sincronización sin cortar letras por la mitad)
            int totalWords = activeLine.Words.Count;
            if (totalWords > 0)
            {
                double exactProgress = 0.0;
                for (int i = 0; i < totalWords; i++)
                {
                    var w = activeLine.Words[i];
                    if (effectivePos >= w.StartTime)
                    {
                        exactProgress = (double)(i + 1) / totalWords;
                    }
                    else
                    {
                        break;
                    }
                }
                LyricProgress = Math.Clamp(exactProgress, 0.0, 1.0);
            }
            else
            {
                var lineDuration = (activeLine.EndTime - activeLine.StartTime).TotalMilliseconds;
                if (lineDuration > 0)
                {
                    var elapsed = (effectivePos - activeLine.StartTime).TotalMilliseconds;
                    LyricProgress = Math.Clamp(elapsed / lineDuration, 0.0, 1.0);
                }
            }
        }
        else
        {
            if (_lastActiveLyricIndex != -1)
            {
                _lastActiveLyricIndex = -1;
                CurrentLyricText = string.Empty;
                NextLyricText = string.Empty;
                LyricProgress = 0.0;
            }
            var upcoming = _currentLyrics.FirstOrDefault(l => l.StartTime > effectivePos);
            if (upcoming != null && (upcoming.StartTime - effectivePos).TotalSeconds < 5)
            {
                NextLyricText = upcoming.Text;
            }
        }

        // Sincronización del servicio interno (sin Seek para evitar interrupciones de audio)
        _playerService.UpdatePosition(realPosition);
    }

    private void OnStateChanged(object? sender, PlayerState state)
    {
        _dispatcher.Invoke(() =>
        {
            IsPlaying = state == PlayerState.Playing;
            StatusMessage = state switch
            {
                PlayerState.Playing => $"REPRODUCIENDO: {SelectedSong?.DisplayName}",
                PlayerState.Paused => "EN PAUSA",
                PlayerState.Stopped => "DETENIDO",
                _ => StatusMessage
            };
        });
    }

    [RelayCommand]
    private void Play()
    {
        if (SelectedSong != null)
        {
            SelectedSong.PlayCount++;
            _ = _catalogService.UpdateSongAsync(SelectedSong);
            
            // Agregar al historial de la sesión si no es el último agregado
            if (!_sessionHistory.Any() || _sessionHistory.First().Id != SelectedSong.Id)
            {
                _sessionHistory.Insert(0, SelectedSong);
            }
        }
        if (SelectedSong != null && !IsPlaying)
        {
            if (CurrentPositionSeconds < 1.0)
            {
                _ = StartSongWithCountdownAsync(SelectedSong);
            }
            else
            {
                _playerService.Play();
            }
        }
    }

    [RelayCommand]
    private void PlaySong(Song? song)
    {
        if (song == null) return;
        _suppressAutoPlay = false;
        if (SelectedSong == song)
        {
            if (!IsPlaying)
            {
                if (CurrentPositionSeconds < 1.0)
                {
                    _ = StartSongWithCountdownAsync(song);
                }
                else
                {
                    _playerService.Play();
                }
            }
        }
        else
        {
            SelectedSong = song;
        }
    }

    [RelayCommand]
    public void NextSong()
    {
        if (SelectedSong == null || !Songs.Any()) return;
        int idx = Songs.IndexOf(SelectedSong);
        if (idx < Songs.Count - 1)
        {
            SelectedSong = Songs[idx + 1];
            StatusMessage = $"⏭️ Siguiente canción: {SelectedSong.DisplayName}";
        }
        else if (Songs.Count > 0)
        {
            SelectedSong = Songs[0];
            StatusMessage = $"⏭️ Inicio de la lista: {SelectedSong.DisplayName}";
        }
    }

    [RelayCommand]
    public void PreviousSong()
    {
        if (SelectedSong == null || !Songs.Any()) return;
        int idx = Songs.IndexOf(SelectedSong);
        if (idx > 0)
        {
            SelectedSong = Songs[idx - 1];
            StatusMessage = $"⏮️ Canción anterior: {SelectedSong.DisplayName}";
        }
        else if (Songs.Count > 0)
        {
            SelectedSong = Songs[Songs.Count - 1];
            StatusMessage = $"⏮️ Última canción de la lista: {SelectedSong.DisplayName}";
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (IsPlaying)
        {
            _playerService.Pause();
        }
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            if (SelectedSong != null)
            {
                Play();
            }
            else if (Songs.Any())
            {
                PlaySong(Songs.First());
            }
        }
    }

    [RelayCommand]
    private void ToggleLoop()
    {
        IsLooping = !IsLooping;
        StatusMessage = IsLooping ? "🔁 Modo Replay / Bucle Infinito ACTIVADO (Estilo YouTube Loop)" : "➡️ Modo Replay DESACTIVADO (Reproducción continua de la lista)";
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        StatusMessage = IsMuted ? "🔇 Audio silenciado (Mute)" : "🔊 Audio activado";
    }

    [RelayCommand]
    private void Stop()
    {
        _playerService.Stop();
        LyricProgress = 0.0;
    }

    [RelayCommand]
    private void Seek(double seconds)
    {
        _playerService.Seek(TimeSpan.FromSeconds(seconds));
    }

    [RelayCommand]
    private void SelectBackgroundImage()
    {
        var authDialog = new PasswordDialog();
        if (authDialog.ShowDialog() != true)
        {
            StatusMessage = "🔒 Cambio de fondo cancelado o contraseña incorrecta (Clave: admin 123).";
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Title = "Seleccionar Imagen de Fondo para el Karaoke",
            Filter = "Archivos de Imagen|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Todos los archivos|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            BackgroundImagePath = openFileDialog.FileName;
            if (SelectedSong != null)
            {
                SelectedSong.BackgroundImagePath = BackgroundImagePath;
                _ = _catalogService.UpdateSongAsync(SelectedSong);
            }
            StatusMessage = "✅ Imagen de fondo autorizada y actualizada por el administrador.";
        }
    }

    [RelayCommand]
    private void SelectMp3File()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Seleccionar archivo MP3 o Audio de Karaoke",
            Filter = "Archivos de Audio|*.mp3;*.wav;*.m4a;*.ogg|Todos los archivos|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var fileName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
            var newSong = new Song
            {
                Title = fileName,
                Artist = "Personalizado / Local",
                AudioFilePath = openFileDialog.FileName,
                BackgroundImagePath = BackgroundImagePath,
                Duration = TimeSpan.FromMinutes(3.5),
                Category = "Archivos MP3 Locales",
                LyricsFilePath = "[Muestra] Letra autogenerada para " + fileName
            };

            Songs.Add(newSong);
            SelectedSong = newSong;
            _ = _catalogService.AddSongAsync(newSong);
            StatusMessage = $"Archivo local agregado: {fileName}";
        }
    }
}

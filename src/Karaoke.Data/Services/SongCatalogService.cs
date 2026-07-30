using Karaoke.Core.Interfaces;
using Karaoke.Core.Models;

namespace Karaoke.Data.Services;

public class SongCatalogService : ISongCatalogService
{
    private readonly List<Song> _songs = new();

    public SongCatalogService()
    {
    }

    public async Task<List<Song>> GetAllSongsAsync()
    {
        return await Task.FromResult(_songs.ToList());
    }

    public async Task<Song?> GetSongByIdAsync(string id)
    {
        return await Task.FromResult(_songs.FirstOrDefault(s => s.Id == id));
    }

    public async Task<List<Song>> SearchSongsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllSongsAsync();

        var q = query.ToLowerInvariant();
        var results = _songs.Where(s => 
            s.Title.ToLowerInvariant().Contains(q) || 
            s.Artist.ToLowerInvariant().Contains(q) ||
            s.Category.ToLowerInvariant().Contains(q)).ToList();

        return await Task.FromResult(results);
    }

    public async Task AddSongAsync(Song song)
    {
        _songs.Add(song);
        await Task.CompletedTask;
    }

    public async Task UpdateSongAsync(Song song)
    {
        var existing = _songs.FirstOrDefault(s => s.Id == song.Id);
        if (existing != null)
        {
            existing.Title = song.Title;
            existing.Artist = song.Artist;
            existing.AudioFilePath = song.AudioFilePath;
            existing.LyricsFilePath = song.LyricsFilePath;
            existing.BackgroundImagePath = song.BackgroundImagePath;
            existing.Category = song.Category;
            existing.Duration = song.Duration;
        }
        await Task.CompletedTask;
    }

    public async Task DeleteSongAsync(string id)
    {
        var song = _songs.FirstOrDefault(s => s.Id == id);
        if (song != null)
        {
            _songs.Remove(song);
        }
        await Task.CompletedTask;
    }

    private int _genreBatchCounter = 1;
    private IYouTubeService? _youTubeService;

    public void SetYouTubeService(IYouTubeService youTubeService)
    {
        _youTubeService = youTubeService;
    }

    public async Task<List<Song>> GetGenreSongsAsync(string genre)
    {
        if (!_songs.Any()) await InitializeDefaultCatalogAsync();
        
        var existing = _songs.Where(s => s.Category.Equals(genre, StringComparison.OrdinalIgnoreCase) || s.Category.ToLowerInvariant().Contains(genre.ToLowerInvariant())).ToList();
        
        // Consulta dinámica a YouTube para obtener música fresca en tiempo real y ampliar el catálogo
        if (_youTubeService != null)
        {
            try
            {
                string ytQuery = genre.Contains("Tendencias") ? "karaoke éxitos 2026 tendencias pop reggaeton" : $"karaoke {genre} éxitos oficial";
                var ytSongs = await _youTubeService.SearchVideosAsync(ytQuery, false, 15);
                foreach (var ytSong in ytSongs)
                {
                    ytSong.Category = genre;
                    if (!_songs.Any(s => s.Title.Equals(ytSong.Title, StringComparison.OrdinalIgnoreCase) && s.Artist.Equals(ytSong.Artist, StringComparison.OrdinalIgnoreCase)))
                    {
                        _songs.Add(ytSong);
                        existing.Add(ytSong);
                    }
                }
            }
            catch { }
        }

        if (existing.Any())
        {
            return await Task.FromResult(existing);
        }
        
        var newSongs = await GenerateSongsForGenreAsync(genre, 1);
        foreach (var s in newSongs)
        {
            if (!_songs.Any(x => x.Title.Equals(s.Title, StringComparison.OrdinalIgnoreCase) && x.Artist.Equals(s.Artist, StringComparison.OrdinalIgnoreCase)))
            {
                _songs.Add(s);
            }
        }
        return await Task.FromResult(newSongs);
    }

    public async Task<List<Song>> RefreshGenreSongsAsync(string genre)
    {
        _genreBatchCounter++;
        var existing = _songs.Where(s => s.Category.Equals(genre, StringComparison.OrdinalIgnoreCase) || s.Category.ToLowerInvariant().Contains(genre.ToLowerInvariant())).ToList();
        foreach (var s in existing)
        {
            _songs.Remove(s);
        }
        
        var newSongs = await GenerateSongsForGenreAsync(genre, _genreBatchCounter);
        foreach (var s in newSongs)
        {
            if (!_songs.Any(x => x.Title.Equals(s.Title, StringComparison.OrdinalIgnoreCase) && x.Artist.Equals(s.Artist, StringComparison.OrdinalIgnoreCase)))
            {
                _songs.Add(s);
            }
        }

        if (_youTubeService != null)
        {
            try
            {
                string ytQuery = genre.Contains("Tendencias") ? $"karaoke éxitos tendencia 2026 vol {_genreBatchCounter}" : $"karaoke {genre} mix oficial vol {_genreBatchCounter}";
                var ytSongs = await _youTubeService.SearchVideosAsync(ytQuery, false, 20);
                foreach (var ytSong in ytSongs)
                {
                    ytSong.Category = genre;
                    if (!_songs.Any(x => x.Title.Equals(ytSong.Title, StringComparison.OrdinalIgnoreCase) && x.Artist.Equals(ytSong.Artist, StringComparison.OrdinalIgnoreCase)))
                    {
                        _songs.Add(ytSong);
                        newSongs.Add(ytSong);
                    }
                }
            }
            catch { }
        }

        return await Task.FromResult(newSongs);
    }

    public async Task<List<Song>> GetGenreSongsByPageAsync(string genre, int page)
    {
        if (!_songs.Any()) await InitializeDefaultCatalogAsync();
        var filtered = _songs.Where(s => s.Category.Equals(genre, StringComparison.OrdinalIgnoreCase)).ToList();
        return await Task.FromResult(filtered);
    }

    public async Task<List<Song>> GetTopPlayedSongsAsync()
    {
        if (!_songs.Any()) await InitializeDefaultCatalogAsync();
        return await Task.FromResult(_songs.OrderByDescending(s => s.PlayCount).ToList());
    }

    public async Task<List<Song>> GetFavoriteSongsAsync()
    {
        if (!_songs.Any()) await InitializeDefaultCatalogAsync();
        return await Task.FromResult(_songs.Where(s => s.IsFavorite).ToList());
    }

    private async Task<List<Song>> GenerateSongsForGenreAsync(string genre, int batchNum = 1)
    {
        var results = new List<Song>();
        var g = genre.ToLowerInvariant();
        
        (string Artist, string Title)[] hits;
        
        if (g.Contains("tendencia") || g.Contains("moda") || g.Contains("actual"))
        {
            hits = new[] {
                ("Karol G", "TQG"), ("Feid", "Normal"), ("Bad Bunny", "Tití Me Preguntó"), ("Rosalía", "Despechá"),
                ("Quevedo", "Vista Al Mar"), ("Bizarrap & Shakira", "Music Sessions Vol. 53"), ("Peso Pluma", "Ella Baila Sola"),
                ("Myke Towers", "LALA"), ("Emilia & Tini", "La_Original.mp3"), ("Manuel Turizo", "La Bachata"),
                ("Dua Lipa", "Houdini"), ("Billie Eilish", "Birds of a Feather"), ("Sabrina Carpenter", "Espresso"),
                ("Rauw Alejandro", "Todo de Ti"), ("Sebastián Yatra", "Tacones Rojos"), ("Maluma", "Según Quién"),
                ("Becky G", "Chanel"), ("Maria Becerra", "Corazón Vacío"), ("Aitana", "Las Babys"), ("Anitta", "Envolver"),
                ("The Weeknd", "Blinding Lights"), ("Miley Cyrus", "Flowers"), ("Taylor Swift", "Cruel Summer"),
                ("Grupo Frontera", "No Se Va"), ("Carin León", "Primera Cita"), ("Young Miko", "Classy 101")
            };
        }
        else if (g.Contains("rock"))
        {
            hits = new[] {
                ("Queen", "Bohemian Rhapsody"), ("Bon Jovi", "Livin' on a Prayer"), ("Guns N' Roses", "Sweet Child O' Mine"),
                ("AC/DC", "Back in Black"), ("Soda Stereo", "De Música Ligera"), ("Hombres G", "Devuélveme a mi chica"),
                ("Enanitos Verdes", "Lamento Boliviano"), ("Maná", "Rayando el Sol"), ("Aerosmith", "I Don't Want to Miss a Thing"),
                ("The Rolling Stones", "Paint It Black"), ("Nirvana", "Smells Like Teen Spirit"), ("Metallica", "Nothing Else Matters"),
                ("The Police", "Every Breath You Take"), ("U2", "With or Without You"), ("Caifanes", "La Célula que Explota"),
                ("Héroes del Silencio", "Entre dos tierras"), ("Prisioneros", "Tren al sur"), ("El Tri", "Triste canción"),
                ("Mago de Oz", "Fiesta Pagana"), ("Rata Blanca", "Mujer amante"), ("Europe", "The Final Countdown"),
                ("Kiss", "I Was Made For Lovin' You"), ("Eagles", "Hotel California"), ("Pink Floyd", "Another Brick in the Wall")
            };
        }
        else if (g.Contains("pop"))
        {
            hits = new[] {
                ("Dua Lipa", "Levitating"), ("Shakira", "Waka Waka"), ("Karol G", "Bichota"), ("Katy Perry", "Roar"),
                ("Taylor Swift", "Shake It Off"), ("Rosalía", "Malamente"), ("Ariana Grande", "7 rings"), ("Lady Gaga", "Bad Romance"),
                ("Beyoncé", "Crazy in Love"), ("Madonna", "Hung Up"), ("Bruno Mars", "Uptown Funk"), ("Rihanna", "Umbrella"),
                ("Miley Cyrus", "Wrecking Ball"), ("Ed Sheeran", "Shape of You"), ("Adele", "Rolling in the Deep"),
                ("Thalía", "Amor a la mexicana"), ("Paulina Rubio", "Ni una sola palabra"), ("Julieta Venegas", "Me voy"),
                ("Mon Laferte", "Tu falta de querer"), ("Belinda", "Luz sin gravedad"), ("Britney Spears", "Toxic"),
                ("Christina Aguilera", "Fighter"), ("Kylie Minogue", "Can't Get You Out of My Head")
            };
        }
        else if (g.Contains("balada") || g.Contains("románt"))
        {
            hits = new[] {
                ("Luis Miguel", "La Incondicional"), ("Chayanne", "Dejaría Todo"), ("Alejandro Fernández", "Me Dediqué a Perderte"),
                ("Sin Bandera", "Mientes Tan Bien"), ("Camila", "Mientes"), ("Franco De Vita", "Te Amo"),
                ("Juan Gabriel", "Hasta que te conocí"), ("Ricky Martin", "Tal Vez"), ("Laura Pausini", "Se fue"),
                ("Ricardo Arjona", "Fuiste Tú"), ("Cristian Castro", "Azul"), ("David Bisbal", "Dígale"),
                ("Marco Antonio Solís", "Si no te hubieras ido"), ("Pablo Alborán", "Solamente Tú"), ("Alejandro Sanz", "Corazón Partío"),
                ("Reik", "Noviembre sin ti"), ("Jesse & Joy", "¡Corre!"), ("Yuridia", "Ya te olvidé"),
                ("Gian Marco", "Se me olvidó"), ("Ricardo Montaner", "Tan enamorados"), ("Eros Ramazzotti", "Otra como tú")
            };
        }
        else if (g.Contains("80"))
        {
            hits = new[] {
                ("Michael Jackson", "Billie Jean"), ("A-ha", "Take On Me"), ("Cyndi Lauper", "Girls Just Want to Have Fun"),
                ("Whitney Houston", "I Wanna Dance with Somebody"), ("Timbiriche", "Tú y yo somos uno mismo"), ("Mecano", "Hijo de la Luna"),
                ("Flans", "No controles"), ("Luis Miguel", "Ahora te puedes marchar"), ("Hombres G", "Venezia"), ("Toto", "Africa"),
                ("Europe", "Carrie"), ("Guns N' Roses", "Welcome to the Jungle"), ("Soda Stereo", "Persiana Americana"),
                ("Bonnie Tyler", "Total Eclipse of the Heart"), ("George Michael", "Careless Whisper"), ("Miguel Mateos", "Cuando seas grande"),
                ("Enanitos Verdes", "La muralla verde"), ("Duncan Dhu", "En algún lugar"), ("Alaska y Dinarama", "A quién le importa")
            };
        }
        else if (g.Contains("reggae") || g.Contains("urban") || g.Contains("fiesta"))
        {
            hits = new[] {
                ("Daddy Yankee", "Gasolina"), ("Bad Bunny", "Me Porto Bonito"), ("Don Omar", "Danza Kuduro"),
                ("J Balvin", "Mi Gente"), ("Maluma", "Hawái"), ("Wisin & Yandel", "Rakata"), ("Nicky Jam", "El Perdón"),
                ("Ozuna", "Taki Taki"), ("Anuel AA", "China"), ("Feid", "Feliz Cumpleaños Ferxxo"), ("Rauw Alejandro", "Diluvio"),
                ("Karol G", "Provenza"), ("Plan B", "Fanática Sensual"), ("Zion & Lennox", "Zun Da Da"), ("Farruko", "Pepas"),
                ("Arcángel", "La Jumpa"), ("Sech", "Otro Trago"), ("Lunay", "Soltera"), ("Ivy Queen", "Quiero Bailar")
            };
        }
        else if (g.Contains("salsa") || g.Contains("cumbia") || g.Contains("tropic"))
        {
            hits = new[] {
                ("Marc Anthony", "Vivir Mi Vida"), ("Grupo Niche", "Una Aventura"), ("Gilberto Santa Rosa", "Conteo Regresivo"),
                ("Joe Arroyo", "La Rebelión"), ("Los Ángeles Azules", "17 Años"), ("Selena", "Como la Flor"),
                ("Celia Cruz", "La Vida es un Carnaval"), ("Tito Nieves", "Fabricando Fantasías"), ("Willie Colón", "Idilio"),
                ("Oscar D'León", "Llorando se fue"), ("El Gran Combo", "Me Liberé"), ("Jerry Rivera", "Amores como el nuestro"),
                ("Rubén Blades", "Pedro Navaja"), ("Frankie Ruiz", "Tú con él"), ("Víctor Manuelle", "Tengo Ganas"),
                ("Grupo 5", "Motor y Motivo"), ("Agua Marina", "Tu amor fue una mentira"), ("Corazón Serrano", "Cuatro mentiras")
            };
        }
        else
        {
            hits = new[] {
                ("Karaoke VIP", "Éxito Inolvidable"), ("Karafun Studio", "Canta Conmigo"), ("Super Éxito", "Noche de Fiesta"),
                ("Estrella Pop", "Amor Eterno"), ("Banda Latina", "Ritmo Total"), ("Rockstar Pro", "Himno del Escenario"),
                ("Diva Global", "Pasión y Fuego"), ("Rey del Ritmo", "Bajo las Estrellas"), ("Leyenda Musical", "Corazón de Oro")
            };
        }

        string[] editions = new[] { "", " [En Vivo]", " [Remastered]", " [Acústico]", " [Tour Edition]", " [KTV Pro]" };

        for (int i = 0; i < hits.Length; i++)
        {
            var pair = hits[i];
            string editionTag = batchNum > 1 ? editions[(i + batchNum) % editions.Length] : "";
            string title = $"{pair.Title}{editionTag}".Trim();
            string artist = pair.Artist;

            results.Add(new Song
            {
                Id = $"genre_{g}_{i}_{Guid.NewGuid():N}".Substring(0, 20),
                Title = title,
                Artist = artist,
                Category = genre,
                Duration = TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(30 + (i * 13) % 180)),
                PlayCount = Math.Max(10, (hits.Length + 5 - i) * 12),
                IsFavorite = false,
                AudioFilePath = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString($"{artist} {title} karaoke oficial")}",
                LyricsFilePath = $"[Karafun Cloud] Transcripción sincronizada para: {title} - {artist}"
            });
        }

        return await Task.FromResult(results);
    }

    public async Task InitializeDefaultCatalogAsync()
    {
        if (_songs.Any()) return;
        string[] defaultGenres = new[] { "🔥 Tendencias Actuales", "Rock Clásico", "Pop Divas", "Baladas Románticas", "Éxitos 80s", "Reggaetón Fiesta", "Salsa y Cumbia" };
        foreach (var g in defaultGenres)
        {
            var songs = await GenerateSongsForGenreAsync(g);
            foreach (var s in songs)
            {
                if (!_songs.Any(x => x.Title.Equals(s.Title, StringComparison.OrdinalIgnoreCase) && x.Artist.Equals(s.Artist, StringComparison.OrdinalIgnoreCase)))
                {
                    _songs.Add(s);
                }
            }
        }
    }
}

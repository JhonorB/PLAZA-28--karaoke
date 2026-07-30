using System.Net;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using Karaoke.Core.Interfaces;
using Karaoke.Core.Models;

namespace Karaoke.Services;

public class YouTubeService : IYouTubeService
{
    // API Key oficial del usuario insertada:
    private string _apiKey = "AIzaSyDCEmFUhnHKrZuQD46X2HKlOvZ6qlT2UIw";

    public void SetApiKey(string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _apiKey = apiKey.Trim();
        }
    }

    public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);

    public async Task<List<Song>> SearchVideosAsync(string query, bool isInstrumental = false, int maxResults = 10)
    {
        var results = new List<Song>();
        var cleanQuery = string.IsNullOrWhiteSpace(query) ? "Karaoke Popular" : query.Trim();

        // Eliminar hardcoding de 'español' para permitir música en Inglés y otros idiomas
        string searchQuery = isInstrumental
            ? $"{cleanQuery} karaoke instrumental"
            : $"{cleanQuery} official lyrics";
        
        string categoryTag = isInstrumental ? "🎹 Instrumental + Letra" : "🎤 Música + Letra";

        try
        {
            if (HasApiKey)
            {
                var googleYtService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
                {
                    ApiKey = _apiKey,
                    ApplicationName = "KaraokeComercialPro"
                });

                var searchListRequest = googleYtService.Search.List("snippet");
                searchListRequest.Q = searchQuery; // Algoritmo inteligente de consulta
                searchListRequest.MaxResults = maxResults + 5; // Extra para filtrar por relevancia
                searchListRequest.Type = "video";
                searchListRequest.VideoCategoryId = "10"; // Categoría 10 = Música en YouTube

                var searchListResponse = await searchListRequest.ExecuteAsync();

                var tempItems = new List<(Google.Apis.YouTube.v3.Data.SearchResult Item, int Score)>();

                foreach (var item in searchListResponse.Items)
                {
                    if (item.Id.VideoId != null)
                    {
                        var title = WebUtility.HtmlDecode(item.Snippet.Title);
                        var artist = WebUtility.HtmlDecode(item.Snippet.ChannelTitle);
                        int score = ScoreVideoRelevance(title, artist, cleanQuery, isInstrumental);
                        tempItems.Add((item, score));
                    }
                }

                // Ordenar por puntuación del algoritmo para que la mejor instrumental en español quede en el Top:
                var sorted = tempItems.OrderByDescending(x => x.Score).Take(maxResults);

                foreach (var (item, _) in sorted)
                {
                    var title = WebUtility.HtmlDecode(item.Snippet.Title);
                    var artist = WebUtility.HtmlDecode(item.Snippet.ChannelTitle);

                    results.Add(new Song
                    {
                        Title = title,
                        Artist = artist,
                        Category = categoryTag,
                        Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(45)),
                        AudioFilePath = $"https://www.youtube.com/watch?v={item.Id.VideoId}",
                        LyricsFilePath = $"[YouTube En Vivo] Transcripción para: {title}",
                        CoverImagePath = item.Snippet.Thumbnails?.Medium?.Url ?? item.Snippet.Thumbnails?.Default__?.Url ?? string.Empty
                    });
                }

                if (results.Any())
                {
                    return results;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error consultando API de YouTube: {ex.Message}");
        }

        // Fallback robusto en caso de que la búsqueda falle o no devuelva items
        results.Add(new Song
        {
            Title = $"{cleanQuery} {(isInstrumental ? "(Instrumental sin voz)" : "(Versión Oficial con Voz)")}",
            Artist = "YouTube Nube / Karaoke HD",
            Category = categoryTag,
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(45)),
            AudioFilePath = $"https://youtube.com/watch?v=demo_{Guid.NewGuid():N}",
            LyricsFilePath = $"[Muestra] Letra sincronizada para: {cleanQuery}..."
        });

        return results;
    }

    private int ScoreVideoRelevance(string videoTitle, string channelTitle, string targetQuery, bool isInstrumental)
    {
        int score = 100;
        string tLower = (videoTitle + " " + channelTitle).ToLowerInvariant();
        string qLower = targetQuery.ToLowerInvariant();

        // Penalizar fuertemente si sale en inglés SOLO si el usuario buscó explícitamente en "español" o "latino"
        bool seeksSpanish = qLower.Contains("español") || qLower.Contains("spanish") || qLower.Contains("latino");
        if (seeksSpanish)
        {
            string[] englishMismatchWords = { "english version", "english cover", "in english", "translated to english" };
            foreach (var word in englishMismatchWords)
            {
                if (tLower.Contains(word) && !qLower.Contains(word))
                {
                    score -= 500; // Descartar versiones en inglés si se busca en español
                }
            }
        }

        // Premiar canales de karaoke reconocidos mundialmente (Sing King, KaraFun, etc.)
        if (tLower.Contains("sing king") || tLower.Contains("karafun") || tLower.Contains("karaoke version") || tLower.Contains("canta con nosotros") || tLower.Contains("karaoke acústico"))
        {
            score += 200;
        }

        // Premiar palabras clave en instrumental:
        if (isInstrumental)
        {
            if (tLower.Contains("karaoke") || tLower.Contains("instrumental") || tLower.Contains("sin voz") || tLower.Contains("pista") || tLower.Contains("backing track")) score += 80;
            if (tLower.Contains("vocal") || tLower.Contains("con voz") || tLower.Contains("official video") || tLower.Contains("oficial")) score -= 100;
        }

        // Premiar fuertemente la coincidencia exacta de palabras del título y artista:
        var queryWords = qLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2 && w != "karaoke" && w != "oficial" && w != "español" && w != "official" && w != "lyrics" && w != "video");
        foreach (var w in queryWords)
        {
            if (tLower.Contains(w)) score += 100;
        }

        return score;
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoIdOrUrl)
    {
        try
        {
            var youtube = new YoutubeClient();
            string targetUrl = videoIdOrUrl;

            // Si es una URL de búsqueda del catálogo o consulta, usamos el algoritmo para encontrar el video real:
            if (videoIdOrUrl.Contains("search_query=") || !videoIdOrUrl.Contains("watch?v="))
            {
                string query = videoIdOrUrl;
                if (videoIdOrUrl.Contains("search_query="))
                {
                    query = Uri.UnescapeDataString(videoIdOrUrl.Substring(videoIdOrUrl.IndexOf("search_query=") + 13));
                }

                var searchResults = new List<YoutubeExplode.Search.VideoSearchResult>();
                await foreach (var video in youtube.Search.GetVideosAsync(query))
                {
                    searchResults.Add(video);
                    if (searchResults.Count >= 10) break;
                }

                var bestVideo = searchResults
                    .OrderByDescending(v => ScoreVideoRelevance(v.Title, v.Author.ChannelTitle, query, true))
                    .FirstOrDefault();

                if (bestVideo != null)
                {
                    targetUrl = bestVideo.Url;
                }
            }

            var manifest = await youtube.Videos.Streams.GetManifestAsync(targetUrl);
            
            // Priorizamos un stream Muxed (Video + Audio combinados) seleccionando la máxima resolución posible (hasta 1080p o superior)
            var muxedStreams = manifest.GetMuxedStreams()
                .OrderByDescending(s => s.VideoResolution.Height)
                .ThenByDescending(s => s.Bitrate)
                .ToList();

            var bestMuxed = muxedStreams.FirstOrDefault(s => s.VideoResolution.Height >= 1080)
                         ?? muxedStreams.FirstOrDefault(s => s.VideoResolution.Height >= 720)
                         ?? muxedStreams.FirstOrDefault();

            if (bestMuxed != null)
            {
                System.Diagnostics.Debug.WriteLine($"[YouTubeService] Stream Muxed de Alta Definición seleccionado: {bestMuxed.VideoResolution} ({bestMuxed.VideoQuality.Label}) - {bestMuxed.Url}");
                return bestMuxed.Url;
            }
            
            // Fallback a solo audio si no hay video disponible
            var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (streamInfo != null)
            {
                return streamInfo.Url;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extrayendo stream con YoutubeExplode: {ex.Message}");
        }
        return videoIdOrUrl;
    }

    public async Task<Song?> GetVideoInfoFromUrlAsync(string url)
    {
        try
        {
            var youtube = new YoutubeClient();
            var videoId = YoutubeExplode.Videos.VideoId.Parse(url);
            var video = await youtube.Videos.GetAsync(videoId);
            
            // Auto-detect si es instrumental o vocal por el título o canal
            string lowerTitle = video.Title.ToLower();
            bool isInstrumental = lowerTitle.Contains("karaoke") || 
                                  lowerTitle.Contains("instrumental") ||
                                  lowerTitle.Contains("pista") ||
                                  lowerTitle.Contains("sin voz");
                                  
            return new Song
            {
                Id = $"yt_{video.Id}",
                Title = video.Title,
                Artist = video.Author.ChannelTitle,
                AudioFilePath = url,
                Duration = video.Duration ?? TimeSpan.Zero,
                Category = isInstrumental ? "Karaoke/Instrumental" : "Música Vocal",
                CoverImagePath = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? ""
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error obteniendo info de video por URL: {ex.Message}");
            return null;
        }
    }

    public async Task<List<LyricLine>?> GetClosedCaptionsAsync(string videoUrl)
    {
        try
        {
            var youtube = new YoutubeClient();
            var videoId = YoutubeExplode.Videos.VideoId.Parse(videoUrl);
            var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoId);
            
            // Buscar subtítulos en español (manual o autogenerado como es-419, es-MX, es), luego inglés, o el primero disponible
            var trackInfo = manifest.Tracks.FirstOrDefault(t => t.Language.Code.StartsWith("es", StringComparison.OrdinalIgnoreCase) && !t.IsAutoGenerated)
                         ?? manifest.Tracks.FirstOrDefault(t => t.Language.Code.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                         ?? manifest.Tracks.FirstOrDefault(t => t.Language.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                         ?? manifest.Tracks.FirstOrDefault();

            if (trackInfo != null)
            {
                var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);
                var lyricLines = new List<LyricLine>();
                
                foreach (var caption in track.Captions)
                {
                    if (!string.IsNullOrWhiteSpace(caption.Text))
                    {
                        string cleanedText = System.Text.RegularExpressions.Regex.Replace(caption.Text, @"\[.*?\]|\(.*?\)|♪|♫", "").Trim();
                        if (!string.IsNullOrWhiteSpace(cleanedText))
                        {
                            var line = new LyricLine
                            {
                                StartTime = caption.Offset,
                                EndTime = caption.Offset + caption.Duration,
                                Text = cleanedText
                            };

                            var words = cleanedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (words.Length > 0 && caption.Duration.TotalMilliseconds > 0)
                            {
                                var wDur = caption.Duration / words.Length;
                                for (int w = 0; w < words.Length; w++)
                                {
                                    line.Words.Add(new LyricWord
                                    {
                                        Text = words[w],
                                        StartTime = caption.Offset + (wDur * w),
                                        EndTime = caption.Offset + (wDur * (w + 1))
                                    });
                                }
                            }

                            lyricLines.Add(line);
                        }
                    }
                }
                
                return lyricLines.Count > 0 ? lyricLines : null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error obteniendo CC de YouTube: {ex.Message}");
        }
        return null;
    }
}

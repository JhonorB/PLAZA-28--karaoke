using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Karaoke.Core.Interfaces;
using Karaoke.Core.Models;

namespace Karaoke.Services;

public class LyricsParserService : ILyricsParserService
{
    private static readonly Regex LrcRegex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)", RegexOptions.Compiled);

    public List<LyricLine> ParseLrc(string lrcContent)
    {
        var lines = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(lrcContent))
            return lines;

        var rawLines = lrcContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var parsedTemp = new List<(TimeSpan Start, string Text)>();

        foreach (var rawLine in rawLines)
        {
            var match = LrcRegex.Match(rawLine.Trim());
            if (match.Success)
            {
                int minutes = int.Parse(match.Groups[1].Value);
                int seconds = int.Parse(match.Groups[2].Value);
                string msStr = match.Groups[3].Value;
                int milliseconds = msStr.Length == 2 ? int.Parse(msStr) * 10 : int.Parse(msStr);

                var time = TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(seconds)).Add(TimeSpan.FromMilliseconds(milliseconds));
                var text = match.Groups[4].Value.Trim();
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\[.*?\]", "").Trim();
                
                if (!string.IsNullOrEmpty(text))
                {
                    parsedTemp.Add((time, text));
                }
            }
            else if (!rawLine.StartsWith("[") && !string.IsNullOrWhiteSpace(rawLine))
            {
                // Plain line fallback
                string text = System.Text.RegularExpressions.Regex.Replace(rawLine.Trim(), @"\[.*?\]", "").Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    parsedTemp.Add((TimeSpan.Zero, text));
                }
            }
        }

        for (int i = 0; i < parsedTemp.Count; i++)
        {
            var current = parsedTemp[i];
            var endTime = (i + 1 < parsedTemp.Count && parsedTemp[i + 1].Start > current.Start) 
                ? parsedTemp[i + 1].Start 
                : current.Start.Add(TimeSpan.FromSeconds(5));

            var lyricLine = new LyricLine
            {
                StartTime = current.Start,
                EndTime = endTime,
                Text = current.Text
            };

            // Generate simple word breakdown for sweep effect
            var words = current.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                var wordDuration = (endTime - current.Start) / words.Length;
                for (int w = 0; w < words.Length; w++)
                {
                    lyricLine.Words.Add(new LyricWord
                    {
                        Text = words[w],
                        StartTime = current.Start.Add(wordDuration * w),
                        EndTime = current.Start.Add(wordDuration * (w + 1))
                    });
                }
            }

            lines.Add(lyricLine);
        }

        return lines;
    }

    public List<LyricLine> ParseLrcFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            return ParseLrc(content);
        }
        return new List<LyricLine>();
    }

    public List<LyricLine> GenerateDummyLyrics(TimeSpan duration, string songTitle)
    {
        // El usuario pidió no inventar letras falsas ni coros principales.
        // Si no hay subtítulos de YouTube (CC) ni archivos externos en LRCLIB, retornamos una lista vacía para no mostrar puntos molestos en la pantalla
        return new List<LyricLine>();
    }

    private static readonly HttpClient _httpClient = new HttpClient();

    private string CleanTitleForSearch(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        int pipeIdx = title.IndexOf('|');
        if (pipeIdx > 0) title = title.Substring(0, pipeIdx);
        title = Regex.Replace(title, @"\(.*?\)|\[.*?\]", " ");
        string[] attachWords = { "vevo", "topic", "official", "oficial", "channel", "canal", "karaoke", "instrumental", "video", "lyric", "letras", "letra", "acustico", "acústico", "version", "versión", "cover", "pista", "audio", "4k" };
        foreach (var w in attachWords)
        {
            title = Regex.Replace(title, w, " ", RegexOptions.IgnoreCase);
        }
        string[] stopWords = { "hd", "producer", "feat", "ft.", "ft", "en vivo", "live", "zettha", "zetta", "the" };
        foreach (var word in stopWords)
        {
            title = Regex.Replace(title, $@"\b{word}\b", " ", RegexOptions.IgnoreCase);
        }
        title = Regex.Replace(title, @"\s+", " ").Trim(' ', '-', '_');
        return title;
    }

    private async Task<List<LyricLine>?> TrySearchLrcLibAsync(string query, string? expectedTitle = null, string? expectedArtist = null)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return null;
        try
        {
            var url = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("KaraokeProCommercial/1.0 (https://github.com/karaoke)");
            }
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("syncedLyrics", out var syncedProp) && syncedProp.ValueKind == JsonValueKind.String)
                    {
                        string syncedText = syncedProp.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(syncedText))
                        {
                            if (IsBadLyricMatch(syncedText, expectedTitle) || !IsTrackMatch(item, expectedTitle, expectedArtist))
                            {
                                continue;
                            }
                            var parsed = ParseLrc(syncedText);
                            if (parsed.Count > 0) return parsed;
                        }
                    }
                }
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("plainLyrics", out var plainProp) && plainProp.ValueKind == JsonValueKind.String)
                    {
                        string plainText = plainProp.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(plainText))
                        {
                            if (IsBadLyricMatch(plainText, expectedTitle) || !IsTrackMatch(item, expectedTitle, expectedArtist))
                            {
                                continue;
                            }
                            return AutoSynchronizePlainLyrics(plainText, TimeSpan.FromMinutes(3.5));
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private bool IsBadLyricMatch(string lyricsText, string? expectedTitle)
    {
        if (string.IsNullOrWhiteSpace(lyricsText)) return true;
        
        string lower = lyricsText.ToLowerInvariant();
        string titleLower = expectedTitle?.ToLowerInvariant() ?? "";
        
        // Evitar colisiones o remezclas con mezclas en inglés/spanglish o mensajes extraños que no coincidan con el título original
        string[] unwantedWords = { "on repeat", "got what i need", "papi", "mami", "quítame el estrés", "let me see" };
        foreach (var w in unwantedWords)
        {
            if (lower.Contains(w) && !titleLower.Contains(w))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsTrackMatch(JsonElement item, string? expectedTitle, string? expectedArtist)
    {
        string lrcTrackName = item.TryGetProperty("trackName", out var tProp) ? (tProp.GetString() ?? "") : "";
        string lrcArtistName = item.TryGetProperty("artistName", out var aProp) ? (aProp.GetString() ?? "") : "";

        // 1. Validar Título (Debe coincidir al menos una palabra clave del título esperado)
        if (!string.IsNullOrWhiteSpace(expectedTitle))
        {
            var titleKeywords = CleanTitleForSearch(expectedTitle)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !IsStopWord(w))
                .ToList();

            if (titleKeywords.Count > 0)
            {
                string targetTitle = $"{lrcTrackName} {lrcArtistName}".ToLowerInvariant();
                if (!titleKeywords.Any(k => targetTitle.Contains(k.ToLowerInvariant())))
                {
                    return false;
                }
            }
        }

        // 2. Validar Artista (Si tenemos un artista real, NO aceptar letras de otro artista diferente con el mismo título de canción)
        if (!string.IsNullOrWhiteSpace(expectedArtist))
        {
            string cleanArtist = CleanArtistForSearch(expectedArtist);
            var artistKeywords = cleanArtist
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !IsStopWord(w))
                .ToList();

            if (artistKeywords.Count > 0)
            {
                string targetArtist = $"{lrcArtistName} {lrcTrackName}".ToLowerInvariant();
                string titleLower = expectedTitle?.ToLowerInvariant() ?? "";
                bool artistInTitle = !string.IsNullOrWhiteSpace(lrcArtistName) && 
                                     lrcArtistName.Split(new[] { ' ', ',', '&', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                                  .Any(w => w.Length > 2 && !IsStopWord(w) && titleLower.Contains(w.ToLowerInvariant()));

                if (!artistKeywords.Any(k => targetArtist.Contains(k.ToLowerInvariant())) && !artistInTitle)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private string CleanArtistForSearch(string artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return "";
        string cleaned = Regex.Replace(artist, @"\(.*?\)|\[.*?\]", " ");
        string[] attachWords = { "vevo", "topic", "official", "oficial", "channel", "canal", "personalizado", "desconocido" };
        foreach (var w in attachWords)
        {
            cleaned = Regex.Replace(cleaned, w, " ", RegexOptions.IgnoreCase);
        }
        string[] ignoreWords = { "artist", "artista", "various", "feat", "ft.", "ft" };
        foreach (var w in ignoreWords)
        {
            cleaned = Regex.Replace(cleaned, $@"\b{w}\b", " ", RegexOptions.IgnoreCase);
        }
        return Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', '_');
    }

    private bool IsStopWord(string word)
    {
        string[] stops = { "para", "como", "con", "del", "las", "los", "por", "una", "uno", "que", "más", "mas", "esta", "esto", "pero", "sobre", "entre" };
        return stops.Contains(word.ToLowerInvariant());
    }

    public async Task<List<LyricLine>> FetchOrGenerateLyricsAsync(string songTitle, string artist, TimeSpan duration)
    {
        string cleanTitle = CleanTitleForSearch(songTitle);
        string cleanArtist = CleanArtistForSearch(artist);
        List<LyricLine>? result = null;
        
        // Intento 1 (Prioritario): Si tenemos artista, buscar con Artista + Título
        if (!string.IsNullOrWhiteSpace(cleanArtist) && !cleanArtist.Contains("YouTube", StringComparison.OrdinalIgnoreCase) && !cleanArtist.Contains("Personalizado", StringComparison.OrdinalIgnoreCase))
        {
            result = await TrySearchLrcLibAsync($"{cleanArtist} {cleanTitle}", cleanTitle, cleanArtist);
            if (result != null && result.Count > 0) 
            {
                EnsureIntroTitle(result, songTitle);
                return result;
            }
        }

        // Intento 2: Búsqueda directa del título limpio
        result = await TrySearchLrcLibAsync(cleanTitle, cleanTitle, cleanArtist);
        if (result != null && result.Count > 0)
        {
            EnsureIntroTitle(result, songTitle);
            return result;
        }

        // Intento 3: Si tiene guiones (típico YouTube "Artista - Canción"), tomar partes principales
        var parts = cleanTitle.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 2).ToList();
        if (parts.Count >= 2)
        {
            string inferredArtist = CleanArtistForSearch(parts[parts.Count - 2]);
            string inferredTitle = CleanTitleForSearch(parts[parts.Count - 1]);
            string query2 = $"{inferredArtist} {inferredTitle}";
            result = await TrySearchLrcLibAsync(query2, inferredTitle, inferredArtist);
            if (result != null && result.Count > 0)
            {
                EnsureIntroTitle(result, songTitle);
                return result;
            }
            
            string query3 = $"{inferredTitle} {inferredArtist}";
            result = await TrySearchLrcLibAsync(query3, inferredTitle, inferredArtist);
            if (result != null && result.Count > 0)
            {
                EnsureIntroTitle(result, songTitle);
                return result;
            }
        }

        // Si la búsqueda en nube no encuentra una coincidencia exacta y verificada de artista y título, generar letras sincronizadas
        var aiLyrics = GenerateRealAILyrics(songTitle, artist, duration);
        EnsureIntroTitle(aiLyrics, songTitle);
        return aiLyrics;
    }

    public void EnsureIntroTitle(List<LyricLine> lyrics, string songTitle)
    {
        if (lyrics == null) return;

        // Limpiar cualquier introducción hablada extraña o mensajes no deseados al principio de la canción (menos de 2 segundos)
        while (lyrics.Count > 0 && lyrics[0].StartTime < TimeSpan.FromSeconds(2.0) && !lyrics[0].Text.Contains("🎵"))
        {
            lyrics.RemoveAt(0);
        }
    }

    private List<LyricLine> GenerateRealAILyrics(string songTitle, string artist, TimeSpan duration)
    {
        return GenerateDummyLyrics(duration, songTitle);
    }

    private List<LyricLine> AutoSynchronizePlainLyrics(string plainText, TimeSpan duration)
    {
        var rawLines = plainText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("["))
                                .ToList();

        if (rawLines.Count == 0) return GenerateDummyLyrics(duration, "Canción");

        var lines = new List<LyricLine>();
        double totalSecs = duration.TotalSeconds > 20 ? duration.TotalSeconds - 10 : 180;
        double secPerLine = totalSecs / rawLines.Count;

        for (int i = 0; i < rawLines.Count; i++)
        {
            var start = TimeSpan.FromSeconds(5 + i * secPerLine);
            var end = TimeSpan.FromSeconds(5 + (i + 0.9) * secPerLine);

            var line = new LyricLine
            {
                StartTime = start,
                EndTime = end,
                Text = rawLines[i]
            };

            var words = rawLines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                var wDur = (end - start) / words.Length;
                for (int w = 0; w < words.Length; w++)
                {
                    line.Words.Add(new LyricWord
                    {
                        Text = words[w],
                        StartTime = start.Add(wDur * w),
                        EndTime = start.Add(wDur * (w + 1))
                    });
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}

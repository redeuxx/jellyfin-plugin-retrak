using System;

namespace ReTrak.Api;

/// <summary>
/// ReTrak API URI helpers.
/// </summary>
public static class ReTrakUris
{
    /// <summary>
    /// Gets the ReTrak plugin client id sent with API requests.
    /// </summary>
    public const string Id = "c44548028dcd8f31e9bee55318562e6e5deb8524f5ca3e77e167fd3b1c9ce380";

    /// <summary>
    /// Gets the ReTrak plugin client secret.
    /// </summary>
    public const string Secret = "d453bc07bcf42f72e3915715a5275d99de8381ff007c84d20e89ed1070310c89";

    private static string BaseUrl
    {
        get
        {
            var configUrl = Plugin.Instance?.PluginConfiguration?.ReTrakUrl;
            if (string.IsNullOrWhiteSpace(configUrl))
            {
                return "https://retrak.tv/api";
            }

            configUrl = configUrl.Trim().TrimEnd('/');
            if (!configUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                configUrl += "/api";
            }

            return configUrl;
        }
    }

    /// <summary>
    /// Gets the OAuth token endpoint.
    /// </summary>
    public static string Token => $"{BaseUrl}/oauth/token";

    /// <summary>
    /// Gets the collection sync add endpoint.
    /// </summary>
    public static string SyncCollectionAdd => $"{BaseUrl}/sync/collection";

    /// <summary>
    /// Gets the collection sync remove endpoint.
    /// </summary>
    public static string SyncCollectionRemove => $"{BaseUrl}/sync/collection/remove";

    /// <summary>
    /// Gets the ratings sync endpoint.
    /// </summary>
    public static string SyncRatingsAdd => $"{BaseUrl}/sync/ratings";

    /// <summary>
    /// Gets the scrobble start endpoint.
    /// </summary>
    public static string ScrobbleStart => $"{BaseUrl}/scrobble/start";

    /// <summary>
    /// Gets the scrobble pause endpoint.
    /// </summary>
    public static string ScrobblePause => $"{BaseUrl}/scrobble/pause";

    /// <summary>
    /// Gets the scrobble stop endpoint.
    /// </summary>
    public static string ScrobbleStop => $"{BaseUrl}/scrobble/stop";

    /// <summary>
    /// Gets the watched movies endpoint.
    /// </summary>
    public static string WatchedMovies => $"{BaseUrl}/sync/watched/movies";

    /// <summary>
    /// Gets the watched shows endpoint.
    /// </summary>
    public static string WatchedShows => $"{BaseUrl}/sync/watched/shows";

    /// <summary>
    /// Gets the collected movies endpoint.
    /// </summary>
    public static string CollectedMovies => $"{BaseUrl}/sync/collection/movies?extended=metadata";

    /// <summary>
    /// Gets the collected shows endpoint.
    /// </summary>
    public static string CollectedShows => $"{BaseUrl}/sync/collection/shows?extended=metadata";

    /// <summary>
    /// Gets the paused movies endpoint.
    /// </summary>
    public static string PausedMovies => $"{BaseUrl}/sync/playback/movies";

    /// <summary>
    /// Gets the paused episodes endpoint.
    /// </summary>
    public static string PausedEpisodes => $"{BaseUrl}/sync/playback/episodes";

    /// <summary>
    /// Gets the watched movies history endpoint.
    /// </summary>
    public static string SyncWatchedMoviesHistory => $"{BaseUrl}/sync/history/movies?page={{page}}&limit=1000";

    /// <summary>
    /// Gets the watched episodes history endpoint.
    /// </summary>
    public static string SyncWatchedEpisodesHistory => $"{BaseUrl}/sync/history/episodes?page={{page}}&limit=1000";

    /// <summary>
    /// Gets the movie recommendations endpoint.
    /// </summary>
    public static string RecommendationsMovies => $"{BaseUrl}/recommendations/movies";

    /// <summary>
    /// Gets the show recommendations endpoint.
    /// </summary>
    public static string RecommendationsShows => $"{BaseUrl}/recommendations/shows";
}

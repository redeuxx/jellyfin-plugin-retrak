using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using ReTrak.Api.DataContracts;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.DataContracts.Scrobble;
using ReTrak.Api.DataContracts.Sync;
using ReTrak.Api.DataContracts.Sync.Collection;
using ReTrak.Api.DataContracts.Sync.Ratings;
using ReTrak.Api.DataContracts.Sync.Watched;
using ReTrak.Model;
using ReTrak.Model.Enums;
using ReTrakEpisodeCollected = ReTrak.Api.DataContracts.Sync.Collection.ReTrakEpisodeCollected;
using ReTrakMovieCollected = ReTrak.Api.DataContracts.Sync.Collection.ReTrakMovieCollected;
using ReTrakShowCollected = ReTrak.Api.DataContracts.Sync.Collection.ReTrakShowCollected;

namespace ReTrak.Api;

/// <summary>
/// ReTrak API client class.
/// </summary>
public class ReTrakApi
{
    private static readonly SemaphoreSlim _retrakResourcePool = new SemaphoreSlim(1, 1);
    private static readonly TimeSpan _tooManyRequestDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _gatewayDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<ReTrakApi> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerApplicationHost _appHost;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReTrakApi"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
    /// <param name="appHost">The <see cref="IServerApplicationHost"/>.</param>
    /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
    /// <param name="userManager">The <see cref="IUserManager"/>.</param>
    public ReTrakApi(
        ILogger<ReTrakApi> logger,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        IUserDataManager userDataManager,
        IUserManager userManager)
    {
        _httpClientFactory = httpClientFactory;
        _appHost = appHost;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether it's possible/allowed to sync a <see cref="BaseItem"/> for a <see cref="ReTrakUser"/>.
    /// </summary>
    /// <param name="item">Item to check.</param>
    /// <param name="retrakUser">The ReTrak user to check for.</param>
    /// <returns><see cref="bool"/> indicating if it's possible/allowed to sync this item.</returns>
    public bool CanSync(BaseItem item, ReTrakUser retrakUser)
    {
        if (item.Path == null || item.LocationType == LocationType.Virtual)
        {
            return false;
        }

        if (retrakUser.LocationsExcluded != null
            && retrakUser.LocationsExcluded.Any(directory => item.Path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (item is Movie movie)
        {
            return movie.HasProviderId(MetadataProvider.Imdb)
                || movie.HasProviderId(MetadataProvider.Tmdb);
        }

        if (item is Episode episode
            && episode.Series != null
            && !episode.IsMissingEpisode
            && (episode.IndexNumber.HasValue
                || HasAnyProviderTvIds(episode)
            ))
        {
            var series = episode.Series;

            return HasAnyProviderTvIds(series);
        }

        return false;
    }

    /// <summary>
    /// Report to ReTrak that a movie is being watched or has been watched.
    /// </summary>
    /// <param name="movie">The movie being watched/scrobbled.</param>
    /// <param name="mediaStatus">The <see cref="MediaStatus"/> indicating whether a movie is being watched or scrobbled.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's watch progress is being updated.</param>
    /// <param name="progressPercent">The progress percentage.</param>
    /// <returns>A standard ReTrak response data contract.</returns>
    public async Task<ReTrakScrobbleResponse> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, ReTrakUser retrakUser, float progressPercent)
    {
        var movieData = new ReTrakScrobbleMovie
        {
            AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AppVersion = _appHost.ApplicationVersionString,
            Progress = progressPercent,
            Movie = new ReTrakMovie
            {
                Title = movie.Name,
                Year = movie.ProductionYear,
                Ids = GetReTrakIMDBTMDBIds<Movie, ReTrakMovieId>(movie)
            }
        };

        string url;
        switch (mediaStatus)
        {
            case MediaStatus.Watching:
                url = ReTrakUris.ScrobbleStart;
                break;
            case MediaStatus.Paused:
                url = ReTrakUris.ScrobblePause;
                break;
            default:
                url = ReTrakUris.ScrobbleStop;
                break;
        }

        return await PostToReTrak<ReTrakScrobbleResponse>(url, movieData, retrakUser, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports to ReTrak that an episode is being watched or has been watched.
    /// </summary>
    /// <param name="episode">The <see cref="Episode"/> being watched.</param>
    /// <param name="mediaStatus">The <see cref="MediaStatus"/> indicating whether an episode is being watched or scrobbled.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's watch progress is being updated.</param>
    /// <param name="progressPercent">The progress percentage.</param>
    /// <param name="useProviderIds"><see cref="bool"/> specifying if provider ids should be used for lookup or not.</param>
    /// <returns>Task{List{ReTrakScrobbleResponse}}.</returns>
    public async Task<List<ReTrakScrobbleResponse>> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus mediaStatus, ReTrakUser retrakUser, float progressPercent, bool useProviderIds = true)
    {
        var episodeDatas = new List<ReTrakScrobbleEpisode>();

        if (useProviderIds
            && HasAnyProviderTvIds(episode)
            && (!episode.IndexNumber.HasValue
                || !episode.IndexNumberEnd.HasValue
                || episode.IndexNumberEnd <= episode.IndexNumber))
        {
            episodeDatas.Add(new ReTrakScrobbleEpisode
            {
                AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                AppVersion = _appHost.ApplicationVersionString,
                Progress = progressPercent,
                Episode = new ReTrakEpisode
                {
                    Ids = GetReTrakTvIds<Episode, ReTrakEpisodeId>(episode)
                }
            });
        }
        else if (episode.IndexNumber.HasValue)
        {
            var indexNumber = episode.IndexNumber.Value;
            var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

            for (var number = indexNumber; number <= finalNumber; number++)
            {
                episodeDatas.Add(new ReTrakScrobbleEpisode
                {
                    AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    AppVersion = _appHost.ApplicationVersionString,
                    Progress = progressPercent,
                    Episode = new ReTrakEpisode
                    {
                        Season = episode.GetSeasonNumber(),
                        Number = number
                    },
                    Show = new ReTrakShow
                    {
                        Title = episode.Series.Name,
                        Year = episode.Series.ProductionYear,
                        Ids = GetReTrakTvIds<Series, ReTrakShowId>(episode.Series)
                    }
                });
            }
        }

        string url;
        switch (mediaStatus)
        {
            case MediaStatus.Watching:
                url = ReTrakUris.ScrobbleStart;
                break;
            case MediaStatus.Paused:
                url = ReTrakUris.ScrobblePause;
                break;
            default:
                url = ReTrakUris.ScrobbleStop;
                break;
        }

        var responses = new List<ReTrakScrobbleResponse>();
        foreach (var retrakScrobbleEpisode in episodeDatas)
        {
            var response = await PostToReTrak<ReTrakScrobbleResponse>(url, retrakScrobbleEpisode, retrakUser, CancellationToken.None).ConfigureAwait(false);
            // Response can be empty if episode not found
            if (response is not null)
            {
                responses.Add(response);
            }
            else if (useProviderIds && HasAnyProviderTvIds(episode))
            {
                // Try scrobbling without ids
                _logger.LogDebug("Resend episode status update, without episode ids");
                responses = await SendEpisodeStatusUpdateAsync(episode, mediaStatus, retrakUser, progressPercent, false).ConfigureAwait(false);
            }
        }

        return responses;
    }

    /// <summary>
    /// Add or remove a list of movies to/from the user's ReTrak library.
    /// </summary>
    /// <param name="movies">The movies to add or remove.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's library is being updated.</param>
    /// <param name="eventType">The <see cref="EventType"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task with ReTrak sync response data contracts.</returns>
    public async Task<IReadOnlyList<ReTrakSyncResponse>> SendLibraryUpdateAsync(
        ICollection<Movie> movies,
        ReTrakUser retrakUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movies);
        ArgumentOutOfRangeException.ThrowIfZero(movies.Count);
        ArgumentNullException.ThrowIfNull(retrakUser);

        var moviesPayload = movies.Select(m =>
        {
            var audioStream = m.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
            var retrakMovieCollected = new ReTrakMovieCollected
            {
                CollectedAt = m.DateCreated.ToISO8601(),
                Title = m.Name,
                Year = m.ProductionYear,
                Ids = GetReTrakIMDBTMDBIds<Movie, ReTrakMovieId>(m)
            };

            if (retrakUser.ExportMediaInfo)
            {
                var defaultVideoStream = m.GetDefaultVideoStream();
                retrakMovieCollected.AudioChannels = audioStream?.GetAudioChannels();
                retrakMovieCollected.Audio = audioStream?.GetCodecRepresetation();
                retrakMovieCollected.Resolution = defaultVideoStream?.GetResolution();
                retrakMovieCollected.Is3D = m.Is3D;
                retrakMovieCollected.Hdr = defaultVideoStream?.GetHdr();
                retrakMovieCollected.MediaType = Enums.ReTrakMediaType.digital;
            }

            return retrakMovieCollected;
        });

        var url = (eventType == EventType.Add || eventType == EventType.Update) ? ReTrakUris.SyncCollectionAdd : ReTrakUris.SyncCollectionRemove;
        var responses = new List<ReTrakSyncResponse>();
        var chunks = moviesPayload.Chunk(100);
        foreach (var chunk in chunks)
        {
            var data = new ReTrakSyncCollected
            {
                Movies = chunk.ToList()
            };

            var response = await PostToReTrak<ReTrakSyncResponse>(url, data, retrakUser, cancellationToken).ConfigureAwait(false);
            responses.Add(response);
        }

        return responses;
    }

    /// <summary>
    /// Add or remove a list of episodes to/from the user's ReTrak library.
    /// </summary>
    /// <param name="episodes">The episodes to add or remove.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's library is being updated.</param>
    /// <param name="eventType">The <see cref="EventType"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task{IEnumerable{ReTrakSyncResponse}}.</returns>
    public async Task<IEnumerable<ReTrakSyncResponse>> SendLibraryUpdateAsync(
        ICollection<Episode> episodes,
        ReTrakUser retrakUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentOutOfRangeException.ThrowIfZero(episodes.Count);
        ArgumentNullException.ThrowIfNull(retrakUser);

        var responses = new List<ReTrakSyncResponse>();
        var chunks = episodes.Chunk(100);
        foreach (var chunk in chunks)
        {
            responses.Add(await SendLibraryUpdateInternalAsync(chunk, retrakUser, eventType, cancellationToken).ConfigureAwait(false));
        }

        return responses;
    }

    private async Task<ReTrakSyncResponse> SendLibraryUpdateInternalAsync(
        IReadOnlyList<Episode> episodes,
        ReTrakUser retrakUser,
        EventType eventType,
        CancellationToken cancellationToken,
        bool useProviderIds = true)
    {
        var episodesPayload = new List<ReTrakEpisodeCollected>();
        var showPayload = new List<ReTrakShowCollected>();
        foreach (Episode episode in episodes)
        {
            var audioStream = episode.GetMediaStreams().FirstOrDefault(stream => stream.Type == MediaStreamType.Audio);
            var defaultVideoStream = episode.GetDefaultVideoStream();
            if (useProviderIds
                && HasAnyProviderTvIds(episode)
                && (!episode.IndexNumber.HasValue
                    || !episode.IndexNumberEnd.HasValue
                    || episode.IndexNumberEnd <= episode.IndexNumber))
            {
                var retrakEpisodeCollected = new ReTrakEpisodeCollected
                {
                    CollectedAt = episode.DateCreated.ToISO8601(),
                    Ids = GetReTrakTvIds<Episode, ReTrakEpisodeId>(episode)
                };

                if (retrakUser.ExportMediaInfo)
                {
                    retrakEpisodeCollected.AudioChannels = audioStream?.GetAudioChannels();
                    retrakEpisodeCollected.Audio = audioStream?.GetCodecRepresetation();
                    retrakEpisodeCollected.Resolution = defaultVideoStream?.GetResolution();
                    retrakEpisodeCollected.Is3D = episode.Is3D;
                    retrakEpisodeCollected.Hdr = defaultVideoStream?.GetHdr();
                    retrakEpisodeCollected.MediaType = Enums.ReTrakMediaType.digital;
                }

                episodesPayload.Add(retrakEpisodeCollected);
            }
            else if (episode.IndexNumber.HasValue)
            {
                var indexNumber = episode.IndexNumber.Value;
                var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
                var syncShow = FindShow(showPayload, episode.Series);
                if (syncShow == null)
                {
                    syncShow = new ReTrakShowCollected
                    {
                        Ids = GetReTrakTvIds<Series, ReTrakShowId>(episode.Series),
                        Seasons = new List<ReTrakSeasonCollected>()
                    };

                    showPayload.Add(syncShow);
                }

                var syncSeason = syncShow.Seasons.FirstOrDefault(season => season.Number == episode.GetSeasonNumber());
                if (syncSeason == null)
                {
                    syncSeason = new ReTrakSeasonCollected
                    {
                        Number = episode.GetSeasonNumber(),
                        Episodes = new List<ReTrakEpisodeCollected>()
                    };

                    syncShow.Seasons.Add(syncSeason);
                }

                for (var number = indexNumber; number <= finalNumber; number++)
                {
                    var ids = new ReTrakEpisodeId();

                    if (number == indexNumber)
                    {
                        // Omit this from the rest because then we end up attaching the provider ids of the first episode to the subsequent ones
                        ids = GetReTrakTvIds<Episode, ReTrakEpisodeId>(episode);
                    }

                    var retrakEpisodeCollected = new ReTrakEpisodeCollected
                    {
                        Number = number,
                        CollectedAt = episode.DateCreated.ToISO8601(),
                        Ids = ids
                    };

                    if (retrakUser.ExportMediaInfo)
                    {
                        retrakEpisodeCollected.AudioChannels = audioStream?.GetAudioChannels();
                        retrakEpisodeCollected.Audio = audioStream?.GetCodecRepresetation();
                        retrakEpisodeCollected.Resolution = defaultVideoStream?.GetResolution();
                        retrakEpisodeCollected.Is3D = episode.Is3D;
                        retrakEpisodeCollected.Hdr = defaultVideoStream?.GetHdr();
                        retrakEpisodeCollected.MediaType = Enums.ReTrakMediaType.digital;
                    }

                    syncSeason.Episodes.Add(retrakEpisodeCollected);
                }
            }
        }

        var data = new ReTrakSyncCollected
        {
            Episodes = episodesPayload,
            Shows = showPayload
        };

        var url = (eventType == EventType.Add || eventType == EventType.Update) ? ReTrakUris.SyncCollectionAdd : ReTrakUris.SyncCollectionRemove;
        var response = await PostToReTrak<ReTrakSyncResponse>(url, data, retrakUser, cancellationToken).ConfigureAwait(false);
        if (useProviderIds && response.NotFound.Episodes.Count > 0)
        {
            // Send subset of episodes back to ReTrak to try without ids
            _logger.LogDebug("Resend episodes Library update, without episode ids");
            await SendLibraryUpdateInternalAsync(FindNotFoundEpisodes(episodes, response), retrakUser, eventType, cancellationToken, false).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Add or remove a show/series to/from the user's ReTrak library.
    /// </summary>
    /// <param name="show">The show/series to add or remove.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's library is being updated.</param>
    /// <param name="eventType">The <see cref="EventType"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task{ReTrakSyncResponse}.</returns>
    public async Task<ReTrakSyncResponse> SendLibraryUpdateAsync(
        Series show,
        ReTrakUser retrakUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(retrakUser);

        var showPayload = new List<ReTrakShowCollected>
        {
            new ReTrakShowCollected
            {
                Title = show.Name,
                Year = show.ProductionYear,
                Ids = GetReTrakTvIds<Series, ReTrakShowId>(show)
            }
        };

        var data = new ReTrakSyncCollected
        {
            Shows = showPayload
        };

        var url = eventType == EventType.Add ? ReTrakUris.SyncCollectionAdd : ReTrakUris.SyncCollectionRemove;
        return await PostToReTrak<ReTrakSyncResponse>(url, data, retrakUser, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rate an item.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="rating">The rating.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's library is being updated.</param>
    /// <param name="useEpisodeProviderIds">If provider ids should be used for episode syncing.</param>
    /// <returns>Task{ReTrakSyncResponse}.</returns>
    public async Task<ReTrakSyncResponse> SendItemRating(BaseItem item, int rating, ReTrakUser retrakUser, bool useEpisodeProviderIds = true)
    {
        if (retrakUser == null)
        {
            return null;
        }

        object data = new { };
        if (item is Movie)
        {
            data = new
            {
                movies = new[]
                {
                    new ReTrakMovieRated
                    {
                        Title = item.Name,
                        Year = item.ProductionYear,
                        Ids = GetReTrakIMDBTMDBIds<Movie, ReTrakMovieId>((Movie)item),
                        Rating = rating
                    }
                }
            };
        }
        else if (item is Episode episode)
        {
            if (useEpisodeProviderIds && HasAnyProviderTvIds(episode))
            {
                data = new
                {
                    episodes = new[]
                    {
                        new ReTrakEpisodeRated
                        {
                            Rating = rating,
                            Ids = GetReTrakTvIds<Episode, ReTrakEpisodeId>(episode)
                        }
                    }
                };
            }
            else
            {
                if (episode.IndexNumber.HasValue)
                {
                    var show = new ReTrakShowRated
                    {
                        Ids = GetReTrakTvIds<Series, ReTrakShowId>(episode.Series),
                        Seasons = new List<ReTrakSeasonRated>
                        {
                            new ReTrakSeasonRated
                            {
                                Number = episode.GetSeasonNumber(),
                                Episodes = new List<ReTrakEpisodeRated>
                                {
                                    new ReTrakEpisodeRated
                                    {
                                        Number = episode.IndexNumber,
                                        Rating = rating
                                    }
                                }
                            }
                        }
                    };
                    data = new
                    {
                        shows = new[]
                        {
                            show
                        }
                    };
                }
            }
        }
        else // It's a series
        {
            data = new
            {
                shows = new[]
                {
                    new ReTrakShowRated
                    {
                        Rating = rating,
                        Title = item.Name,
                        Year = item.ProductionYear,
                        Ids = GetReTrakTvIds<Series, ReTrakShowId>((Series)item)
                    }
                }
            };
        }

        var response = await PostToReTrak<ReTrakSyncResponse>(ReTrakUris.SyncRatingsAdd, data, retrakUser).ConfigureAwait(false);

        if (item is Episode && useEpisodeProviderIds && response.NotFound.Episodes.Count > 0)
        {
            // Try sync without ids
            _logger.LogDebug("Resend episode rating, without episode ids");
            return await SendItemRating(item, rating, retrakUser, false).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Get movie recommendations.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{ReTrakMovie}}.</returns>
    public async Task<List<ReTrakMovie>> SendMovieRecommendationsRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<ReTrakMovie>>(ReTrakUris.RecommendationsMovies, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get show recommendations.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{ReTrakShow}}.</returns>
    public async Task<List<ReTrakShow>> SendShowRecommendationsRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<ReTrakShow>>(ReTrakUris.RecommendationsShows, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get all watched movies.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Users.Watched.ReTrakMovieWatched}}.</returns>
    public async Task<List<DataContracts.Users.Watched.ReTrakMovieWatched>> SendGetAllWatchedMoviesRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<DataContracts.Users.Watched.ReTrakMovieWatched>>(ReTrakUris.WatchedMovies, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get watched shows.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Users.Watched.ReTrakShowWatched}}.</returns>
    public async Task<List<DataContracts.Users.Watched.ReTrakShowWatched>> SendGetWatchedShowsRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<DataContracts.Users.Watched.ReTrakShowWatched>>(ReTrakUris.WatchedShows, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get watched movies history.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Sync.History.ReTrakMovieWatchedHistory}}.</returns>
    public async Task<List<DataContracts.Sync.History.ReTrakMovieWatchedHistory>> SendGetWatchedMoviesHistoryRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrakWithPaging<DataContracts.Sync.History.ReTrakMovieWatchedHistory>(ReTrakUris.SyncWatchedMoviesHistory, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get watched episodes history.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Sync.History.ReTrakEpisodeWatchedHistory}}.</returns>
    public async Task<List<DataContracts.Sync.History.ReTrakEpisodeWatchedHistory>> SendGetWatchedEpisodesHistoryRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrakWithPaging<DataContracts.Sync.History.ReTrakEpisodeWatchedHistory>(ReTrakUris.SyncWatchedEpisodesHistory, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get all paused movies.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Users.Playback.ReTrakMoviePaused}}.</returns>
    public async Task<List<DataContracts.Users.Playback.ReTrakMoviePaused>> SendGetAllPausedMoviesRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<DataContracts.Users.Playback.ReTrakMoviePaused>>(ReTrakUris.PausedMovies, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get paused episodes.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Users.Playback.ReTrakEpisodePaused}}.</returns>
    public async Task<List<DataContracts.Users.Playback.ReTrakEpisodePaused>> SendGetPausedEpisodesRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<DataContracts.Users.Playback.ReTrakEpisodePaused>>(ReTrakUris.PausedEpisodes, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get collected movies.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Users.Collection.ReTrakMovieCollected}}.</returns>
    public async Task<List<DataContracts.Users.Collection.ReTrakMovieCollected>> SendGetAllCollectedMoviesRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<DataContracts.Users.Collection.ReTrakMovieCollected>>(ReTrakUris.CollectedMovies, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Get collected shows.
    /// </summary>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <returns>Task{List{DataContracts.Users.Collection.ReTrakShowCollected}}.</returns>
    public async Task<List<DataContracts.Users.Collection.ReTrakShowCollected>> SendGetCollectedShowsRequest(ReTrakUser retrakUser)
    {
        return await GetFromReTrak<List<DataContracts.Users.Collection.ReTrakShowCollected>>(ReTrakUris.CollectedShows, retrakUser).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a list of movies to ReTrak that have been marked as watched or unwatched.
    /// </summary>
    /// <param name="movies">The list of movies to send.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's library is being updated.</param>
    /// <param name="seen">True if movies are being marked seen, false otherwise.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task{List{ReTrakSyncResponse}}.</returns>
    // TODO: netstandard2.1: use IAsyncEnumerable
    public async Task<List<ReTrakSyncResponse>> SendMoviePlaystateUpdates(
        ICollection<Movie> movies,
        ReTrakUser retrakUser,
        bool seen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movies);
        ArgumentOutOfRangeException.ThrowIfZero(movies.Count);
        ArgumentNullException.ThrowIfNull(retrakUser);

        var user = _userManager.GetUserById(retrakUser.LinkedMbUserId);
        if (user is null)
        {
            _logger.LogWarning("User id ({UserId}) linked to ReTrak does not exist", retrakUser.LinkedMbUserId);
            return null;
        }

        var moviesPayload = movies.Select(m =>
        {
            var lastPlayedDate = seen
                ? _userDataManager.GetUserData(user, m).LastPlayedDate
                : null;

            return new ReTrakMovieWatched
            {
                Title = m.Name,
                Ids = GetReTrakIMDBTMDBIds<Movie, ReTrakMovieId>(m),
                Year = m.ProductionYear,
                WatchedAt = lastPlayedDate?.ToISO8601()
            };
        });

        var chunks = moviesPayload.Chunk(100).ToList();
        var retrakResponses = new List<ReTrakSyncResponse>();

        foreach (var chunk in chunks)
        {
            var data = new ReTrakSyncWatched
            {
                Movies = chunk.ToList()
            };

            var url = seen ? ReTrakUris.SyncWatchedHistoryAdd : ReTrakUris.SyncWatchedHistoryRemove;
            var response = await PostToReTrak<ReTrakSyncResponse>(url, data, retrakUser, cancellationToken).ConfigureAwait(false);
            if (response != null)
            {
                retrakResponses.Add(response);
            }
        }

        return retrakResponses;
    }

    /// <summary>
    /// Send a list of episodes to ReTrak that have been marked watched or unwatched.
    /// </summary>
    /// <param name="episodes">The list of episodes to send.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/> who's library is being updated.</param>
    /// <param name="seen">True if episodes are being marked seen, false otherwise.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task{List{ReTrakSyncResponse}}.</returns>
    public async Task<List<ReTrakSyncResponse>> SendEpisodePlaystateUpdates(
        ICollection<Episode> episodes,
        ReTrakUser retrakUser,
        bool seen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentOutOfRangeException.ThrowIfZero(episodes.Count);
        ArgumentNullException.ThrowIfNull(retrakUser);

        var chunks = episodes.Chunk(100);
        var retrakResponses = new List<ReTrakSyncResponse>();

        foreach (var chunk in chunks)
        {
            var response = await SendEpisodePlaystateUpdatesInternalAsync(chunk, retrakUser, seen, cancellationToken).ConfigureAwait(false);

            if (response != null)
            {
                retrakResponses.Add(response);
            }
        }

        return retrakResponses;
    }

    private async Task<ReTrakSyncResponse> SendEpisodePlaystateUpdatesInternalAsync(
        IReadOnlyList<Episode> episodeChunk,
        ReTrakUser retrakUser,
        bool seen,
        CancellationToken cancellationToken,
        bool useProviderIds = true)
    {
        var user = _userManager.GetUserById(retrakUser.LinkedMbUserId);
        if (user is null)
        {
            _logger.LogWarning("User id ({UserId}) linked to ReTrak does not exist", retrakUser.LinkedMbUserId);
            return null;
        }

        var data = new ReTrakSyncWatched
        {
            Episodes = new List<ReTrakEpisodeWatched>(),
            Shows = new List<ReTrakShowWatched>()
        };

        foreach (var episode in episodeChunk)
        {
            var lastPlayedDate = seen
                ? _userDataManager.GetUserData(user, episode)
                    .LastPlayedDate
                : null;

            if (useProviderIds
                && HasAnyProviderTvIds(episode)
                && (!episode.IndexNumber.HasValue
                    || !episode.IndexNumberEnd.HasValue
                    || episode.IndexNumberEnd <= episode.IndexNumber))
            {
                data.Episodes.Add(new ReTrakEpisodeWatched
                {
                    Ids = GetReTrakTvIds<Episode, ReTrakEpisodeId>(episode),
                    WatchedAt = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                });
            }
            else if (episode.IndexNumber != null)
            {
                var indexNumber = episode.IndexNumber.Value;
                var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

                var syncShow = FindShow(data.Shows, episode.Series);
                if (syncShow == null)
                {
                    syncShow = new ReTrakShowWatched
                    {
                        Ids = GetReTrakTvIds<Series, ReTrakShowId>(episode.Series),
                        Seasons = new List<ReTrakSeasonWatched>()
                    };

                    data.Shows.Add(syncShow);
                }

                var syncSeason = syncShow.Seasons.FirstOrDefault(ss => ss.Number == episode.GetSeasonNumber());
                if (syncSeason == null)
                {
                    syncSeason = new ReTrakSeasonWatched
                    {
                        Number = episode.GetSeasonNumber(),
                        Episodes = new List<ReTrakEpisodeWatched>()
                    };

                    syncShow.Seasons.Add(syncSeason);
                }

                for (var number = indexNumber; number <= finalNumber; number++)
                {
                    syncSeason.Episodes.Add(new ReTrakEpisodeWatched
                    {
                        Number = number,
                        WatchedAt = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                    });
                }
            }
        }

        var url = seen ? ReTrakUris.SyncWatchedHistoryAdd : ReTrakUris.SyncWatchedHistoryRemove;

        var response = await PostToReTrak<ReTrakSyncResponse>(url, data, retrakUser, cancellationToken).ConfigureAwait(false);

        if (useProviderIds && response.NotFound.Episodes.Count > 0)
        {
            // Send subset of episodes back to ReTrak to try without ids
            _logger.LogDebug("Resend episodes playstate update, without episode ids");
            await SendEpisodePlaystateUpdatesInternalAsync(FindNotFoundEpisodes(episodeChunk, response), retrakUser, seen, cancellationToken, false).ConfigureAwait(false);
        }

        return response;
    }

    private List<Episode> FindNotFoundEpisodes(IReadOnlyList<Episode> episodeChunk, ReTrakSyncResponse retrakSyncResponse)
    {
        // Episodes not found. If using ids, try again without them
        List<Episode> episodes = new List<Episode>();
        // Build a list of unfound episodes with ids
        foreach (ReTrakEpisode retrakEpisode in retrakSyncResponse.NotFound.Episodes.Where(episode => HasAnyProviderTvIds(episode.Ids)))
        {
            // Find matching episode in Jellyfin based on provider ids
            var notFoundEpisode = episodeChunk.FirstOrDefault(episode =>
                (episode.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
                 && imdbId == retrakEpisode.Ids.Imdb)
                || (episode.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
                    && tmdbId == retrakEpisode.Ids.Tmdb?.ToString(CultureInfo.InvariantCulture))
                || (episode.TryGetProviderId(MetadataProvider.Tvdb, out var tvdbId)
                    && tvdbId == retrakEpisode.Ids.Tvdb)
                || (episode.TryGetProviderId(MetadataProvider.TvRage, out var tvRageId)
                    && tvRageId == retrakEpisode.Ids.Tvrage));

            if (notFoundEpisode != null)
            {
                episodes.Add(notFoundEpisode);
            }
        }

        return episodes;
    }

    private Task<T> GetFromReTrak<T>(string url, ReTrakUser retrakUser)
    {
        return GetFromReTrak<T>(url, retrakUser, CancellationToken.None);
    }

    private async Task<T> GetFromReTrak<T>(string url, ReTrakUser retrakUser, CancellationToken cancellationToken)
    {
        var httpClient = GetHttpClient();

        if (retrakUser != null)
        {
            await SetRequestHeaders(httpClient, retrakUser).ConfigureAwait(false);
        }

        await _retrakResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await RetryHttpRequest(async () => await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _retrakResourcePool.Release();
        }
    }

    private Task<List<T>> GetFromReTrakWithPaging<T>(string url, ReTrakUser retrakUser)
    {
        return GetFromReTrakWithPaging<T>(url, retrakUser, CancellationToken.None);
    }

    private async Task<List<T>> GetFromReTrakWithPaging<T>(string url, ReTrakUser retrakUser, CancellationToken cancellationToken)
    {
        var httpClient = GetHttpClient();
        var page = 1;
        var result = new List<T>();

        if (retrakUser != null)
        {
            await SetRequestHeaders(httpClient, retrakUser).ConfigureAwait(false);
        }

        await _retrakResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            while (true)
            {
                var urlWithPage = url.Replace("{page}", page.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
                var response = await RetryHttpRequest(async () => await httpClient.GetAsync(urlWithPage, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return result;
                }

                response.EnsureSuccessStatusCode();
                var tmpResult = await response.Content.ReadFromJsonAsync<List<T>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
                if (tmpResult != null)
                {
                    result.AddRange(tmpResult);
                }

                if (page < int.Parse(response.Headers.GetValues("X-Pagination-Page-Count").FirstOrDefault(page.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture))
                {
                    page++;
                }
                else
                {
                    break; // break loop when no more new pages are available
                }
            }

            return result;
        }
        finally
        {
            _retrakResourcePool.Release();
        }
    }

    private async Task<HttpResponseMessage> PostToReTrak(string url, object data)
    {
        var httpClient = GetHttpClient();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        using var content = new ByteArrayContent(bytes);
        content.Headers.Add(HeaderNames.ContentType, MediaTypeNames.Application.Json);

        await _retrakResourcePool.WaitAsync().ConfigureAwait(false);

        try
        {
            return await httpClient.PostAsync(url, content).ConfigureAwait(false);
        }
        finally
        {
            _retrakResourcePool.Release();
        }
    }

    private Task<T> PostToReTrak<T>(string url, object data, ReTrakUser retrakUser)
    {
        return PostToReTrak<T>(url, data, retrakUser, CancellationToken.None);
    }

    /// <summary>
    /// Posts data to url, authenticating with <see cref="ReTrakUser"/>.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="data">The data object.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    private async Task<T> PostToReTrak<T>(
        string url,
        object data,
        ReTrakUser retrakUser,
        CancellationToken cancellationToken)
    {
        if (retrakUser != null && retrakUser.ExtraLogging)
        {
            _logger.LogDebug("{@JsonData}", data);
        }

        var httpClient = GetHttpClient();

        if (retrakUser != null)
        {
            await SetRequestHeaders(httpClient, retrakUser).ConfigureAwait(false);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        using var content = new ByteArrayContent(bytes);
        content.Headers.Add(HeaderNames.ContentType, MediaTypeNames.Application.Json);

        await _retrakResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await RetryHttpRequest(async () => await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled in PostToReTrak");
            throw;
        }
        finally
        {
            _retrakResourcePool.Release();
        }
    }

    private async Task<HttpResponseMessage> RetryHttpRequest(Func<Task<HttpResponseMessage>> function)
    {
        HttpResponseMessage response = null;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                response = await function().ConfigureAwait(false);
                var statusCode = response.StatusCode;

                if (statusCode.HasFlag(HttpStatusCode.TooManyRequests))
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? _tooManyRequestDelay;
                    _logger.LogDebug("Too many requests while communicating with ReTrak - waiting {Time}s", delay.TotalSeconds);
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                else if (statusCode.HasFlag(HttpStatusCode.BadGateway)
                    || statusCode.HasFlag(HttpStatusCode.GatewayTimeout)
                    || statusCode.HasFlag(HttpStatusCode.ServiceUnavailable))
                {
                    _logger.LogDebug("Connectivity error while communicating with ReTrak - waiting {Time}s", _gatewayDelay.TotalSeconds);
                    await Task.Delay(_gatewayDelay).ConfigureAwait(false);
                }
                else
                {
                    break;
                }
            }
            catch (Exception)
            {
            }
        }

        return response;
    }

    private HttpClient GetHttpClient()
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        client.DefaultRequestHeaders.Add("retrak-api-version", "2");
        client.DefaultRequestHeaders.Add("retrak-api-key", ReTrakUris.Id);
        return client;
    }

    private Task SetRequestHeaders(HttpClient httpClient, ReTrakUser retrakUser)
    {
        if (!string.IsNullOrEmpty(retrakUser.AccessToken))
        {
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + retrakUser.AccessToken);
        }

        return Task.CompletedTask;
    }

    private static TReturn GetReTrakIMDBTMDBIds<TInput, TReturn>(TInput mediaObject)
        where TInput : IHasProviderIds
        where TReturn : ReTrakIMDBandTMDBId, new()
    {
        return new TReturn
        {
            Imdb = mediaObject.GetProviderId(MetadataProvider.Imdb),
            Tmdb = mediaObject.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
        };
    }

    private static TReturn GetReTrakTvIds<TInput, TReturn>(TInput mediaObject)
        where TInput : IHasProviderIds
        where TReturn : ReTrakTVId, new()
    {
        TReturn retval = GetReTrakIMDBTMDBIds<TInput, TReturn>(mediaObject);
        retval.Tvdb = mediaObject.GetProviderId(MetadataProvider.Tvdb);
        retval.Tvrage = mediaObject.GetProviderId(MetadataProvider.TvRage);
        return retval;
    }

    private static TReTrakShow FindShow<TReTrakShow>(ICollection<TReTrakShow> shows, Series series)
        where TReTrakShow : ReTrakShow
    {
        return shows.FirstOrDefault(
            sre => sre.Ids != null
                   && sre.Ids.Imdb == series.GetProviderId(MetadataProvider.Imdb)
                   && sre.Ids.Tmdb == series.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
                   && sre.Ids.Tvdb == series.GetProviderId(MetadataProvider.Tvdb)
                   && sre.Ids.Tvrage == series.GetProviderId(MetadataProvider.TvRage));
    }

    private bool HasAnyProviderTvIds(BaseItem item)
    {
        return item.HasProviderId(MetadataProvider.Imdb)
               || item.HasProviderId(MetadataProvider.Tmdb)
               || item.HasProviderId(MetadataProvider.Tvdb)
               || item.HasProviderId(MetadataProvider.TvRage);
    }

    private bool HasAnyProviderTvIds(ReTrakTVId item)
    {
        return !string.IsNullOrEmpty(item.Imdb)
               || !(item.Tmdb == null)
               || !string.IsNullOrEmpty(item.Tvdb)
               || !string.IsNullOrEmpty(item.Tvrage);
    }
}

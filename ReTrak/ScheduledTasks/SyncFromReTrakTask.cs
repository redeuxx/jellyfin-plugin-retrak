using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using ReTrak.Api;
using ReTrak.Api.DataContracts.Sync.History;
using ReTrak.Api.DataContracts.Users.Playback;
using ReTrak.Api.DataContracts.Users.Watched;
using ReTrak.Helpers;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace ReTrak.ScheduledTasks;

/// <summary>
/// Task that will Sync each users ReTrak profile with their local library. This task will only include
/// watched states.
/// </summary>
public class SyncFromReTrakTask : IScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<SyncFromReTrakTask> _logger;
    private readonly ReTrakApi _retrakApi;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncFromReTrakTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public SyncFromReTrakTask(
        ILoggerFactory loggerFactory,
        IUserManager userManager,
        IUserDataManager userDataManager,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        ILibraryManager libraryManager)
    {
        _userManager = userManager;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _logger = loggerFactory.CreateLogger<SyncFromReTrakTask>();
        _retrakApi = new ReTrakApi(loggerFactory.CreateLogger<ReTrakApi>(), httpClientFactory, appHost, userDataManager, userManager);
    }

    /// <inheritdoc />
    public string Key => "ReTrakSyncFromReTrakTask";

    /// <inheritdoc />
    public string Name => "Import watched states and playback progress from ReTrak";

    /// <inheritdoc />
    public string Description => "Imports each user's watched/unwatched status and playback progress from ReTrak to all items in the user's ReTrak monitored locations";

    /// <inheritdoc />
    public string Category => "ReTrak";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var users = _userManager.GetUsers().Where(user => UserHelper.GetReTrakUser(user, true) != null).ToList();

        // No point going further if we don't have users.
        if (users.Count == 0)
        {
            _logger.LogDebug("No Users returned");
            return;
        }

        // Purely for progress reporting
        var percentPerUser = 100d / users.Count;
        double currentProgress = 0;
        var numComplete = 0;

        foreach (var user in users)
        {
            try
            {
                await SyncReTrakDataForUser(user, currentProgress, progress, percentPerUser, cancellationToken).ConfigureAwait(false);

                numComplete++;
                currentProgress = percentPerUser * numComplete;
                progress.Report(currentProgress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing ReTrak data for user {UserName}", user.Username);
            }
        }
    }

    private async Task SyncReTrakDataForUser(User user, double currentProgress, IProgress<double> progress, double percentPerUser, CancellationToken cancellationToken)
    {
        var retrakUser = UserHelper.GetReTrakUser(user, true);

        if (retrakUser.SkipUnwatchedImportFromReTrak
            && retrakUser.SkipWatchedImportFromReTrak
            && retrakUser.SkipPlaybackProgressImportFromReTrak)
        {
            _logger.LogDebug("User {Name} disabled (un)watched and playback syncing.", user.Username);
            return;
        }

        List<ReTrakMovieWatched> retrakWatchedMovies = new List<ReTrakMovieWatched>();
        List<ReTrakShowWatched> retrakWatchedShows = new List<ReTrakShowWatched>();
        List<ReTrakMovieWatchedHistory> retrakWatchedMoviesHistory = new List<ReTrakMovieWatchedHistory>(); // not used for now, just for reference to get watched movies history count
        List<ReTrakEpisodeWatchedHistory> retrakWatchedEpisodesHistory = new List<ReTrakEpisodeWatchedHistory>(); // used for fall episode matching by ids
        List<ReTrakMoviePaused> retrakPausedMovies = new List<ReTrakMoviePaused>();
        List<ReTrakEpisodePaused> retrakPausedEpisodes = new List<ReTrakEpisodePaused>();

        try
        {
            /*
             * In order to be as accurate as possible. We need to download the user's show collection and the user's watched shows.
             * It's unfortunate that ReTrak doesn't explicitly supply a bulk method to determine shows that have not been watched
             * like they do for movies.
             */
            if (!(retrakUser.SkipUnwatchedImportFromReTrak && retrakUser.SkipWatchedImportFromReTrak))
            {
                retrakWatchedMovies.AddRange(await _retrakApi.SendGetAllWatchedMoviesRequest(retrakUser).ConfigureAwait(false));
                retrakWatchedShows.AddRange(await _retrakApi.SendGetWatchedShowsRequest(retrakUser).ConfigureAwait(false));
                retrakWatchedMoviesHistory.AddRange(await _retrakApi.SendGetWatchedMoviesHistoryRequest(retrakUser).ConfigureAwait(false));
                retrakWatchedEpisodesHistory.AddRange(await _retrakApi.SendGetWatchedEpisodesHistoryRequest(retrakUser).ConfigureAwait(false));
            }

            if (!retrakUser.SkipPlaybackProgressImportFromReTrak)
            {
                retrakPausedMovies.AddRange(await _retrakApi.SendGetAllPausedMoviesRequest(retrakUser).ConfigureAwait(false));
                retrakPausedEpisodes.AddRange(await _retrakApi.SendGetPausedEpisodesRequest(retrakUser).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled");
            throw;
        }

        _logger.LogInformation("ReTrak watched movies for user {User}: {Count}", user.Username, retrakWatchedMovies.Count);
        _logger.LogInformation("ReTrak watched movies history for user {User}: {Count}", user.Username, retrakWatchedMoviesHistory.Count);
        _logger.LogInformation("ReTrak paused movies for user {User}: {Count}", user.Username, retrakPausedMovies.Count);
        _logger.LogInformation("ReTrak watched shows for user {User}: {Count}", user.Username, retrakWatchedShows.Count);
        _logger.LogInformation("ReTrak watched episodes history for user {User}: {Count}", user.Username, retrakWatchedEpisodesHistory.Count);
        _logger.LogInformation("ReTrak paused episodes for user {User}: {Count}", user.Username, retrakPausedEpisodes.Count);

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[]
            {
                BaseItemKind.Movie,
                BaseItemKind.Episode
            },
            IsVirtualItem = false,
            OrderBy = new[]
            {
                (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                (ItemSortBy.SortName, SortOrder.Ascending)
            }
        };

        var totalCount = _libraryManager.GetCount(baseQuery);

        const int Limit = 100;
        int offset = 0, previousCount;

        // Purely for progress reporting
        var percentPerIteration = percentPerUser / (totalCount / (double)Limit);

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var mediaItems = _libraryManager.GetItemList(baseQuery);

            previousCount = mediaItems.Count;
            offset += Limit;

            mediaItems = mediaItems.Where(i => _retrakApi.CanSync(i, retrakUser)).ToList();

            // Purely for progress reporting
            var percentPerItem = percentPerIteration / mediaItems.Count;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedWatchedMovie = Extensions.FindMatch(movie, retrakWatchedMovies);
                var matchedPausedMovie = Extensions.FindMatch(movie, retrakPausedMovies);
                var userData = _userDataManager.GetUserData(user, movie);
                bool changed = false;

                if (matchedWatchedMovie != null)
                {
                    _logger.LogDebug("Movie is in watched list of user {User}: {Name}", user.Username, movie.Name);

                    if (!retrakUser.SkipWatchedImportFromReTrak)
                    {
                        DateTime? tLastPlayed = null;
                        if (DateTime.TryParse(matchedWatchedMovie.LastWatchedAt, out var value))
                        {
                            tLastPlayed = value;
                        }

                        // Set movie as watched
                        if (!userData.Played)
                        {
                            // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
                            _logger.LogDebug("Marking movie as watched for user {User} locally: {Name}", user.Username, movie.Name);
                            if (tLastPlayed == null && userData.LastPlayedDate == null)
                            {
                                _logger.LogDebug("Movie's local and remote last played date are missing, falling back to the current time for user {User} locally: {Name}", user.Username, movie.Name);
                                userData.LastPlayedDate = DateTime.Now;
                            }

                            if (tLastPlayed != null
                                && userData.LastPlayedDate != null
                                && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                                && userData.LastPlayedDate < tLastPlayed)
                            {
                                _logger.LogDebug("Setting movie's last played date to remote which is more than 10 minutes more recent than local (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
                                userData.LastPlayedDate = tLastPlayed;
                            }

                            userData.Played = true;
                            changed = true;
                        }

                        // Keep the highest play count
                        if (userData.PlayCount < matchedWatchedMovie.Plays)
                        {
                            _logger.LogDebug("Adjusting movie's play count to match a higher remote value (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", matchedWatchedMovie.Plays, userData.PlayCount, user.Username, movie.Name);
                            userData.PlayCount = matchedWatchedMovie.Plays;
                            changed = true;
                        }

                        // Update last played if remote time is more recent
                        if (tLastPlayed != null && (userData.LastPlayedDate == null || userData.LastPlayedDate < tLastPlayed))
                        {
                            _logger.LogDebug("Adjusting movie's last played date to match a more recent remote last played date (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
                            userData.LastPlayedDate = tLastPlayed;
                            changed = true;
                        }
                    }
                }
                else if (!retrakUser.SkipUnwatchedImportFromReTrak)
                {
                    _logger.LogDebug("Movie is not in watched list: {Name}", movie.Name);

                    // Set movie as unwatched
                    if (userData.Played)
                    {
                        _logger.LogDebug("Marking movie as unwatched for user {User} locally: {Name}", user.Username, movie.Name);
                        userData.Played = false;
                        changed = true;
                    }
                }

                if (!retrakUser.SkipPlaybackProgressImportFromReTrak && matchedPausedMovie != null)
                {
                    _logger.LogDebug("Movie is in paused list of user {User}: {Name}", user.Username, movie.Name);

                    var lastPlayed = userData.LastPlayedDate;
                    DateTime? paused = null;
                    if (DateTime.TryParse(matchedPausedMovie.PausedAt, out var value))
                    {
                        paused = value;
                    }

                    if (lastPlayed == null || (paused != null && lastPlayed < paused))
                    {
                        _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote: {Remote} | local: {Local}). Setting playback progress of movie for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedMovie.Progress, movie.Name);

                        var runtimeTicks = movie.GetRunTimeTicksForPlayState();
                        var retrakPlaybackTicks = runtimeTicks != 0
                            ? (long)matchedPausedMovie.Progress * runtimeTicks / 100L
                            : 0;

                        userData.PlaybackPositionTicks = retrakPlaybackTicks;
                        changed = true;
                    }
                }

                // Only process if there's a change
                if (changed)
                {
                    _userDataManager.SaveUserData(
                        user,
                        movie,
                        userData,
                        UserDataSaveReason.Import,
                        cancellationToken);
                }

                // Purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            foreach (var episode in mediaItems.OfType<Episode>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedWatchedShow = Extensions.FindMatch(episode.Series, retrakWatchedShows);
                var matchedPausedEpisode = Extensions.FindMatch(episode, retrakPausedEpisodes);
                var userData = _userDataManager.GetUserData(user, episode);
                bool changed = false;
                bool episodeWatched = false;

                if (!retrakUser.SkipWatchedImportFromReTrak && matchedWatchedShow != null)
                {
                    // Keep track of the shows rewatch cycles
                    DateTime? tLastReset = null;
                    if (DateTime.TryParse(matchedWatchedShow.ResetAt, out var resetValue))
                    {
                        tLastReset = resetValue;
                    }

                    var matchedWatchedEpisodeHistory = Extensions.FindAllMatches(episode, retrakWatchedEpisodesHistory);

                    // Check if match is found in history
                    if (matchedWatchedEpisodeHistory != null && matchedWatchedEpisodeHistory.Any())
                    {
                        // History is ordered with last watched first, so take the first one
                        var lastWatchedEpisodeHistory = matchedWatchedEpisodeHistory[0];

                        // Prepend a check if the matched episode is on a rewatch cycle and
                        // discard it if the last play date was before the reset date
                        if (lastWatchedEpisodeHistory != null
                            && tLastReset != null
                            && DateTime.TryParse(lastWatchedEpisodeHistory.WatchedAt, out var lastPlayedValue)
                            && lastPlayedValue < tLastReset)
                        {
                            lastWatchedEpisodeHistory = null;
                        }

                        if (lastWatchedEpisodeHistory != null)
                        {
                            _logger.LogDebug("Episode is in watched history list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

                            episodeWatched = true;
                            DateTime? tLastPlayed = null;
                            if (DateTime.TryParse(lastWatchedEpisodeHistory.WatchedAt, out var lastWatchedValue))
                            {
                                tLastPlayed = lastWatchedValue;
                            }

                            // Set episode as watched
                            if (!userData.Played)
                            {
                                // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
                                _logger.LogDebug("Marking episode as watched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                                if (tLastPlayed == null && userData.LastPlayedDate == null)
                                {
                                    _logger.LogDebug("Episode's local and remote last played date are missing, falling back to the current time for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                                    userData.LastPlayedDate = DateTime.Now;
                                }

                                if (tLastPlayed != null
                                    && userData.LastPlayedDate != null
                                    && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                                    && userData.LastPlayedDate < tLastPlayed)
                                {
                                    _logger.LogDebug("Setting episode's last played date to remote which is more than 10 minutes more recent than local (remote: {Remote} | local: {Local}) for user {User} locally: {Data}", tLastPlayed, userData.LastPlayedDate, user.Username, GetVerboseEpisodeData(episode));
                                    userData.LastPlayedDate = tLastPlayed;
                                }

                                userData.Played = true;
                                changed = true;
                            }

                            // Update last played if remote time is more recent
                            if (tLastPlayed != null && (userData.LastPlayedDate == null || userData.LastPlayedDate < tLastPlayed))
                            {
                                _logger.LogDebug("Adjusting episode's last played date to match a more recent remote last played date (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, episode.Name);
                                userData.LastPlayedDate = tLastPlayed;
                                changed = true;
                            }

                            // Keep the highest play count
                            var playCount = matchedWatchedEpisodeHistory.Count;
                            if (userData.PlayCount < playCount)
                            {
                                _logger.LogDebug("Adjusting episode's play count to match a higher remote value (remote: {Remote} | local: {Local}) for user {User} locally: {Data}", playCount, userData.PlayCount, user.Username, GetVerboseEpisodeData(episode));
                                userData.PlayCount = playCount;
                                changed = true;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No episode history data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
                    }
                }
                else
                {
                    _logger.LogDebug("No show data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
                }

                if (!retrakUser.SkipUnwatchedImportFromReTrak && !episodeWatched)
                {
                    _logger.LogDebug("Episode not in watched list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));
                    if (userData.Played)
                    {
                        _logger.LogDebug("Marking episode as unwatched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                        userData.Played = false;
                        changed = true;
                    }
                }

                if (!retrakUser.SkipPlaybackProgressImportFromReTrak && matchedPausedEpisode != null)
                {
                    _logger.LogDebug("Episode is in paused list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

                    var lastPlayed = userData.LastPlayedDate;
                    DateTime? paused = null;
                    if (DateTime.TryParse(matchedPausedEpisode.PausedAt, out var value))
                    {
                        paused = value;
                    }

                    if (lastPlayed == null || (paused != null && lastPlayed < paused))
                    {
                        _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote: {Remote} | local: {Local}). Setting playback progress of episode for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedEpisode.Progress, GetVerboseEpisodeData(episode));

                        var runtimeTicks = episode.GetRunTimeTicksForPlayState();
                        var retrakPlaybackTicks = runtimeTicks != 0
                            ? (long)matchedPausedEpisode.Progress * runtimeTicks / 100L
                            : 0;

                        userData.PlaybackPositionTicks = retrakPlaybackTicks;
                        changed = true;
                    }
                }

                // Only process if changed
                if (changed)
                {
                    _userDataManager.SaveUserData(
                        user,
                        episode,
                        userData,
                        UserDataSaveReason.Import,
                        cancellationToken);
                }

                // Purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }
        }
        while (previousCount != 0);
    }

    private static string GetVerboseEpisodeData(Episode episode)
    {
        var episodeString = new StringBuilder()
            .Append("Episode: ")
            .Append(episode.GetSeasonNumber().ToString(CultureInfo.InvariantCulture))
            .Append('x')
            .Append(episode.IndexNumber != null ? episode.IndexNumber : "null")
            .Append(" '").Append(episode.Name).Append("' ")
            .Append("Series: '")
            .Append(episode.Series != null
                ? !string.IsNullOrWhiteSpace(episode.Series.Name)
                    ? episode.Series.Name
                    : "null property"
                : "null class")
            .Append("' ")
            .Append("Tvdb id: ")
            .Append(episode.GetProviderId(MetadataProvider.Tvdb) ?? "null").Append(' ')
            .Append("Tmdb id: ")
            .Append(episode.GetProviderId(MetadataProvider.Tmdb) ?? "null").Append(' ')
            .Append("Imdb id: ")
            .Append(episode.GetProviderId(MetadataProvider.Imdb) ?? "null").Append(' ')
            .Append("TvRage id: ")
            .Append(episode.GetProviderId(MetadataProvider.TvRage) ?? "null");

        return episodeString.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using ReTrak.Api;
using ReTrak.Api.DataContracts.Sync;
using ReTrak.Api.Enums;
using ReTrak.Helpers;
using ReTrak.Model;
using ReTrak.Model.Enums;

namespace ReTrak.ScheduledTasks;

/// <summary>
/// Task that will Sync each users local library with their respective ReTrak profiles. This task will only include
/// titles, watched states will be synced in other tasks.
/// </summary>
public class SyncLibraryTask : IScheduledTask
{
    private readonly IUserManager _userManager;

    private readonly ILogger<SyncLibraryTask> _logger;

    private readonly ReTrakApi _retrakApi;

    private readonly IUserDataManager _userDataManager;

    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncLibraryTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public SyncLibraryTask(
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
        _logger = loggerFactory.CreateLogger<SyncLibraryTask>();
        _retrakApi = new ReTrakApi(loggerFactory.CreateLogger<ReTrakApi>(), httpClientFactory, appHost, userDataManager, userManager);
    }

    /// <inheritdoc />
    public string Key => "ReTrakSyncLibraryTask";

    /// <inheritdoc />
    public string Name => "Export library to ReTrak";

    /// <inheritdoc />
    public string Category => "ReTrak";

    /// <inheritdoc />
    public string Description => "Exports any media that is in each user's ReTrak monitored locations to their ReTrak collection";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var users = _userManager.GetUsers().Where(u => UserHelper.GetReTrakUser(u, true) != null).ToList();

        // No point going further if we don't have users.
        if (users.Count == 0)
        {
            _logger.LogInformation("No Users returned");
            return;
        }

        // Purely for progress reporting
        double currentProgress = 0d;
        var percentPerUser = 100d / users.Count;

        foreach (var user in users)
        {
            var retrakUser = UserHelper.GetReTrakUser(user, true);

            if (!(retrakUser.SynchronizeCollections || retrakUser.PostUnwatchedHistory || retrakUser.PostWatchedHistory))
            {
                _logger.LogDebug("User {Name} disabled collection and history syncing.", user.Username);
                continue;
            }

            await SyncUserLibrary(user, retrakUser, progress, currentProgress, percentPerUser, cancellationToken).ConfigureAwait(false);

            currentProgress += percentPerUser;
            progress.Report(currentProgress);
        }
    }

    /// <summary>
    /// Calls <see cref="SyncMovies"/> and <see cref="SyncShows"/>.
    /// </summary>
    /// <param name="user">The <see cref="User"/>.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="currentProgress">The current progress.</param>
    /// <param name="percentPerUser">Percent per user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task.</returns>
    private async Task SyncUserLibrary(
        User user,
        ReTrakUser retrakUser,
        IProgress<double> progress,
        double currentProgress,
        double percentPerUser,
        CancellationToken cancellationToken)
    {
        var partialPercentage = percentPerUser * 0.5;
        await SyncMovies(user, retrakUser, progress, currentProgress, partialPercentage, cancellationToken).ConfigureAwait(false);
        await SyncShows(user, retrakUser, progress, currentProgress + partialPercentage, partialPercentage, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncMovies(
        User user,
        ReTrakUser retrakUser,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        List<Api.DataContracts.Users.Watched.ReTrakMovieWatched> retrakWatchedMovies = new List<Api.DataContracts.Users.Watched.ReTrakMovieWatched>();
        List<Api.DataContracts.Users.Collection.ReTrakMovieCollected> retrakCollectedMovies = new List<ReTrak.Api.DataContracts.Users.Collection.ReTrakMovieCollected>();

        try
        {
            /*
            * In order to sync watched status to ReTrak we need to know what's been watched on ReTrak already. This
            * will stop us from endlessly incrementing the watched values on the site.
            */
            if (retrakUser.PostWatchedHistory || retrakUser.PostUnwatchedHistory)
            {
                retrakWatchedMovies.AddRange(await _retrakApi.SendGetAllWatchedMoviesRequest(retrakUser).ConfigureAwait(false));
            }

            if (retrakUser.SynchronizeCollections)
            {
                retrakCollectedMovies.AddRange(await _retrakApi.SendGetAllCollectedMoviesRequest(retrakUser).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled in SyncMovies");
            throw;
        }

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            IsVirtualItem = false,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
        };

        // Purely for progress reporting
        availablePercent /= 4;

        var collectedMovies = new List<Movie>();
        var playedMovies = new List<Movie>();
        var unplayedMovies = new List<Movie>();

        const int Limit = 100;
        int offset = 0, previousCount;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var items = _libraryManager.GetItemList(baseQuery);
            previousCount = items.Count;
            offset += Limit;
            var movieItems = items.OfType<Movie>().Where(x => _retrakApi.CanSync(x, retrakUser));

            if (movieItems != null)
            {
                foreach (var libraryMovie in movieItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var userData = _userDataManager.GetUserData(user, libraryMovie);

                    if (retrakUser.SynchronizeCollections)
                    {
                        // If movie is not collected, or (export media info setting is enabled and every collected matching movie has different metadata), collect it
                        var collectedMatchingMovies = Extensions.FindMatch(libraryMovie, retrakCollectedMovies);
                        if (collectedMatchingMovies == null || (retrakUser.ExportMediaInfo && collectedMatchingMovies.MetadataIsDifferent(libraryMovie)))
                        {
                            collectedMovies.Add(libraryMovie);
                        }
                    }

                    var movieWatched = Extensions.FindMatch(libraryMovie, retrakWatchedMovies);

                    // If the movie has been played locally and is unplayed on ReTrak then add it to the list
                    if (userData.Played)
                    {
                        if (movieWatched == null && retrakUser.PostWatchedHistory)
                        {
                            playedMovies.Add(libraryMovie);
                        }
                    }
                    else
                    {
                        // If the movie has not been played locally but is played on ReTrak then add it to the unplayed list
                        if (movieWatched != null && retrakUser.PostUnwatchedHistory)
                        {
                            unplayedMovies.Add(libraryMovie);
                        }
                    }
                }
            }
        }
        while (previousCount != 0);

        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send movies to mark collected
        await SendMovieCollectionUpdates(true, retrakUser, collectedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send movies to mark watched
        await SendMoviePlaystateUpdates(true, retrakUser, playedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send movies to mark unwatched
        await SendMoviePlaystateUpdates(false, retrakUser, unplayedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);
    }

    private async Task SendMovieCollectionUpdates(
        bool collected,
        ReTrakUser retrakUser,
        List<Movie> movies,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (movies.Count > 0)
        {
            var collectString = collected ? "add to" : "remove from";
            _logger.LogInformation("Movies to {State} collection: {Count}", collectString, movies.Count);
            try
            {
                List<ReTrakSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (movies.Count / 100.0);

                // Force update ReTrak if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < movies.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var moviesToSend = movies.GetRange(offset, 100);
                    dataContracts = (await _retrakApi.SendLibraryUpdateAsync(
                                moviesToSend,
                                retrakUser,
                                collected ? EventType.Add : EventType.Remove,
                                cancellationToken)
                            .ConfigureAwait(false))
                        .ToList();

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogReTrakResponseDataContract(dataContracts, ReTrakItemType.movie);
                }

                dataContracts = (await _retrakApi.SendLibraryUpdateAsync(
                            movies.GetRange(offset, movies.Count - offset),
                            retrakUser,
                            collected ? EventType.Add : EventType.Remove,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogReTrakResponseDataContract(dataContracts, ReTrakItemType.movie);
            }
            catch (ArgumentNullException argNullEx)
            {
                _logger.LogError(argNullEx, "ArgumentNullException handled sending movies to ReTrak");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception handled sending movies to ReTrak");
            }
        }
    }

    private async Task SendMoviePlaystateUpdates(
        bool seen,
        ReTrakUser retrakUser,
        List<Movie> movies,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (movies.Count > 0)
        {
            var watchedString = seen ? "watched" : "unwatched";
            _logger.LogInformation("Movies to set {State}: {Count}", watchedString, movies.Count);
            try
            {
                List<ReTrakSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (movies.Count / 100.0);

                // Force update ReTrak if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < movies.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var moviesToSend = movies.GetRange(offset, 100);
                    dataContracts = await _retrakApi.SendMoviePlaystateUpdates(
                            moviesToSend,
                            retrakUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false);

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogReTrakResponseDataContract(dataContracts, ReTrakItemType.movie);
                }

                dataContracts = await _retrakApi.SendMoviePlaystateUpdates(
                        movies.GetRange(offset, movies.Count - offset),
                        retrakUser,
                        seen,
                        cancellationToken)
                    .ConfigureAwait(false);

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogReTrakResponseDataContract(dataContracts, ReTrakItemType.movie);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating movie play states");
            }
        }
    }

    private async Task SyncShows(
        User user,
        ReTrakUser retrakUser,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        List<Api.DataContracts.Users.Watched.ReTrakShowWatched> retrakWatchedShows = new List<Api.DataContracts.Users.Watched.ReTrakShowWatched>();
        List<Api.DataContracts.Users.Collection.ReTrakShowCollected> retrakCollectedShows = new List<Api.DataContracts.Users.Collection.ReTrakShowCollected>();

        try
        {
            /*
            * In order to sync watched status to ReTrak we need to know what's been watched on ReTrak already. This
            * will stop us from endlessly incrementing the watched values on the site.
            */
            if (retrakUser.PostWatchedHistory || retrakUser.PostUnwatchedHistory)
            {
                retrakWatchedShows.AddRange(await _retrakApi.SendGetWatchedShowsRequest(retrakUser).ConfigureAwait(false));
            }

            if (retrakUser.SynchronizeCollections)
            {
                retrakCollectedShows.AddRange(await _retrakApi.SendGetCollectedShowsRequest(retrakUser).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled in SyncShows");
            throw;
        }

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            IsVirtualItem = false,
            OrderBy = new[] { (ItemSortBy.SeriesSortName, SortOrder.Ascending) }
        };

        // Purely for progress reporting
        availablePercent /= 4;

        var collectedEpisodes = new List<Episode>();
        var playedEpisodes = new List<Episode>();
        var unplayedEpisodes = new List<Episode>();

        const int Limit = 100;
        int offset = 0, previousCount;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var items = _libraryManager.GetItemList(baseQuery);
            previousCount = items.Count;
            offset += Limit;
            var episodeItems = items.OfType<Episode>().Where(x => _retrakApi.CanSync(x, retrakUser));

            if (episodeItems != null)
            {
                foreach (var episode in episodeItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var userData = _userDataManager.GetUserData(user, episode);
                    var isPlayedReTrak = false;
                    var retrakWatchedShow = Extensions.FindMatch(episode.Series, retrakWatchedShows);

                    if (retrakUser.PostSetUnwatched || retrakUser.PostSetWatched)
                    {
                        if (retrakWatchedShow?.Seasons != null && retrakWatchedShow.Seasons.Count > 0)
                        {
                            isPlayedReTrak = retrakWatchedShow.Seasons.Any(
                                season => season.Number == episode.GetSeasonNumber()
                                    && season.Episodes != null
                                    && season.Episodes.Any(e => episode.ContainsEpisodeNumber(e.Number)
                                        && e.Plays > 0));
                        }

                        // If the show has been played locally and is unplayed on ReTrak then add it to the list
                        if (retrakUser.PostWatchedHistory && userData != null && userData.Played && !isPlayedReTrak)
                        {
                            playedEpisodes.Add(episode);
                        }
                        else if (retrakUser.PostUnwatchedHistory && userData != null && !userData.Played && isPlayedReTrak)
                        {
                            // If the show has not been played locally but is played on ReTrak then add it to the unplayed list
                            unplayedEpisodes.Add(episode);
                        }
                    }

                    if (retrakUser.SynchronizeCollections)
                    {
                        var retrakCollectedShow = Extensions.FindMatch(episode.Series, retrakCollectedShows);

                        if (retrakCollectedShow?.Seasons == null || retrakCollectedShow.Seasons.All(season => season.Number != episode.GetSeasonNumber()))
                        {
                            collectedEpisodes.Add(episode);
                        }
                        else
                        {
                            var retrakCollectedSeason = retrakCollectedShow?.Seasons.FirstOrDefault(season => season.Number == episode.GetSeasonNumber());
                            var retrakCollectedEpisode = retrakCollectedSeason?.Episodes.FirstOrDefault(e => e.Number == episode.IndexNumber);
                            if (retrakCollectedEpisode == null || (retrakUser.ExportMediaInfo && retrakCollectedEpisode.MetadataIsDifferent(episode)))
                            {
                                collectedEpisodes.Add(episode);
                            }
                        }
                    }
                }
            }
        }
        while (previousCount != 0);

        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send episodes to mark collected
        await SendEpisodeCollectionUpdates(true, retrakUser, collectedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send episodes to mark watched
        await SendEpisodePlaystateUpdates(true, retrakUser, playedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send episodes to mark unwatched
        await SendEpisodePlaystateUpdates(false, retrakUser, unplayedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);
    }

    private async Task SendEpisodeCollectionUpdates(
        bool collected,
        ReTrakUser retrakUser,
        List<Episode> episodes,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (episodes.Count > 0)
        {
            var collectString = collected ? "add to" : "remove from";
            _logger.LogInformation("Episodes to {State} collection {Count}", collectString, episodes.Count);
            try
            {
                List<ReTrakSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (episodes.Count / 100.0);

                // Force update ReTrak if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < episodes.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var episodesToSend = episodes.GetRange(offset, 100);
                    dataContracts = (await _retrakApi.SendLibraryUpdateAsync(
                                episodesToSend,
                                retrakUser,
                                collected ? EventType.Add : EventType.Remove,
                                cancellationToken)
                            .ConfigureAwait(false))
                        .ToList();

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogReTrakResponseDataContract(dataContracts, ReTrakItemType.episode);
                }

                dataContracts = (await _retrakApi.SendLibraryUpdateAsync(
                            episodes.GetRange(offset, episodes.Count - offset),
                            retrakUser,
                            collected ? EventType.Add : EventType.Remove,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogReTrakResponseDataContract(dataContracts, ReTrakItemType.episode);
            }
            catch (ArgumentNullException argNullEx)
            {
                _logger.LogError(argNullEx, "ArgumentNullException handled sending episodes to ReTrak");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception handled sending episodes to ReTrak");
            }
        }
    }

    private async Task SendEpisodePlaystateUpdates(
        bool seen,
        ReTrakUser retrakUser,
        List<Episode> episodes,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (episodes.Count > 0)
        {
            var watchedString = seen ? "watched" : "unwatched";
            _logger.LogInformation("Episodes to set {State}: {Count}", watchedString, episodes.Count);
            try
            {
                List<ReTrakSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (episodes.Count / 100.0);

                // Force update ReTrak if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < episodes.Count)
                {
                    var episodesToSend = episodes.GetRange(offset, 100);
                    cancellationToken.ThrowIfCancellationRequested();
                    dataContracts = await _retrakApi.SendEpisodePlaystateUpdates(
                            episodesToSend,
                            retrakUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false);

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogReTrakResponseDataContract(dataContracts, ReTrakItemType.episode);
                }

                dataContracts = (await _retrakApi.SendEpisodePlaystateUpdates(
                            episodes.GetRange(offset, episodes.Count - offset),
                            retrakUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogReTrakResponseDataContract(dataContracts, ReTrakItemType.episode);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating episode play states");
            }
        }
    }

    private void LogReTrakResponseDataContract(IReadOnlyCollection<ReTrakSyncResponse> dataContracts, ReTrakItemType type)
    {
        if (dataContracts.Count != 0)
        {
            foreach (var dataContract in dataContracts)
            {
                if (type is ReTrakItemType.movie)
                {
                    if (dataContract.Added?.Movies > 0)
                    {
                        _logger.LogDebug("Added movies: {Count}", dataContract.Added.Movies);
                    }

                    if (dataContract.Updated?.Movies > 0)
                    {
                        _logger.LogDebug("Updated movies: {Count}", dataContract.Updated.Movies);
                    }

                    if (dataContract.Deleted?.Movies > 0)
                    {
                        _logger.LogDebug("Removed movies: {Count}", dataContract.Deleted.Movies);
                    }

                    if (dataContract.NotFound is not null)
                    {
                        foreach (var retrakMovie in dataContract.NotFound.Movies)
                        {
                            _logger.LogError("Movie not found on ReTrak: {@ReTrakMovie}", retrakMovie);
                        }
                    }
                }

                if (type is ReTrakItemType.episode)
                {
                    if (dataContract.Added?.Episodes > 0)
                    {
                        _logger.LogDebug("Added episodes: {Count}", dataContract.Added.Episodes);
                    }

                    if (dataContract.Updated?.Episodes > 0)
                    {
                        _logger.LogDebug("Updated episodes: {Count}", dataContract.Updated.Episodes);
                    }

                    if (dataContract.Deleted?.Episodes > 0)
                    {
                        _logger.LogDebug("Removed episodes: {Count}", dataContract.Deleted.Episodes);
                    }

                    if (dataContract.NotFound is not null)
                    {
                        foreach (var retrakEpisode in dataContract.NotFound.Episodes)
                        {
                            _logger.LogError("Episode not found on ReTrak: {@ReTrakEpisode}", retrakEpisode);
                        }
                    }
                }
            }
        }
    }
}

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
/// Exports each user's Jellyfin library to their ReTrak collection.
/// </summary>
public class SyncLibraryTask : IScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly ILogger<SyncLibraryTask> _logger;
    private readonly ReTrakApi _retrakApi;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncLibraryTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public SyncLibraryTask(
        ILoggerFactory loggerFactory,
        IUserManager userManager,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        ILibraryManager libraryManager)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _logger = loggerFactory.CreateLogger<SyncLibraryTask>();
        _retrakApi = new ReTrakApi(loggerFactory.CreateLogger<ReTrakApi>(), httpClientFactory, appHost, userManager);
    }

    /// <inheritdoc />
    public string Key => "ReTrakSyncLibraryTask";

    /// <inheritdoc />
    public string Name => "Export library to ReTrak";

    /// <inheritdoc />
    public string Category => "ReTrak";

    /// <inheritdoc />
    public string Description => "Exports media in each user's ReTrak monitored locations to their ReTrak collection";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var users = _userManager.GetUsers().Where(u => UserHelper.GetReTrakUser(u, true) != null).ToList();

        if (users.Count == 0)
        {
            _logger.LogInformation("No Users returned");
            return;
        }

        double currentProgress = 0d;
        var percentPerUser = 100d / users.Count;

        foreach (var user in users)
        {
            var retrakUser = UserHelper.GetReTrakUser(user, true);

            if (!retrakUser.SynchronizeCollections)
            {
                _logger.LogDebug("User {Name} disabled collection syncing.", user.Username);
                continue;
            }

            await SyncUserLibrary(user, retrakUser, progress, currentProgress, percentPerUser, cancellationToken).ConfigureAwait(false);

            currentProgress += percentPerUser;
            progress.Report(currentProgress);
        }
    }

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
        var retrakCollectedMovies = new List<Api.DataContracts.Users.Collection.ReTrakMovieCollected>();
        try
        {
            retrakCollectedMovies.AddRange(await _retrakApi.SendGetAllCollectedMoviesRequest(retrakUser).ConfigureAwait(false));
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

        var collectedMovies = new List<Movie>();
        const int Limit = 100;
        int offset = 0, previousCount;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var items = _libraryManager.GetItemList(baseQuery);
            previousCount = items.Count;
            offset += Limit;

            foreach (var libraryMovie in items.OfType<Movie>().Where(x => _retrakApi.CanSync(x, retrakUser)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var collectedMatchingMovies = Extensions.FindMatch(libraryMovie, retrakCollectedMovies);
                if (collectedMatchingMovies == null
                    || (retrakUser.ExportMediaInfo && collectedMatchingMovies.MetadataIsDifferent(libraryMovie)))
                {
                    collectedMovies.Add(libraryMovie);
                }
            }
        }
        while (previousCount != 0);

        await SendMovieCollectionUpdates(true, retrakUser, collectedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncShows(
        User user,
        ReTrakUser retrakUser,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        var retrakCollectedShows = new List<Api.DataContracts.Users.Collection.ReTrakShowCollected>();
        try
        {
            retrakCollectedShows.AddRange(await _retrakApi.SendGetCollectedShowsRequest(retrakUser).ConfigureAwait(false));
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

        var collectedEpisodes = new List<Episode>();
        const int Limit = 100;
        int offset = 0, previousCount;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var items = _libraryManager.GetItemList(baseQuery);
            previousCount = items.Count;
            offset += Limit;

            foreach (var episode in items.OfType<Episode>().Where(x => _retrakApi.CanSync(x, retrakUser)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var retrakCollectedShow = Extensions.FindMatch(episode.Series, retrakCollectedShows);

                if (retrakCollectedShow?.Seasons == null
                    || retrakCollectedShow.Seasons.All(season => season.Number != episode.GetSeasonNumber()))
                {
                    collectedEpisodes.Add(episode);
                    continue;
                }

                var retrakCollectedSeason = retrakCollectedShow.Seasons.FirstOrDefault(
                    season => season.Number == episode.GetSeasonNumber());
                var retrakCollectedEpisode = retrakCollectedSeason?.Episodes.FirstOrDefault(
                    e => e.Number == episode.IndexNumber);
                if (retrakCollectedEpisode == null
                    || (retrakUser.ExportMediaInfo && retrakCollectedEpisode.MetadataIsDifferent(episode)))
                {
                    collectedEpisodes.Add(episode);
                }
            }
        }
        while (previousCount != 0);

        await SendEpisodeCollectionUpdates(true, retrakUser, collectedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
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
        if (movies.Count == 0)
        {
            return;
        }

        var collectString = collected ? "add to" : "remove from";
        _logger.LogInformation("Movies to {State} collection: {Count}", collectString, movies.Count);
        try
        {
            var percentPerRequest = availablePercent / Math.Max(movies.Count / 100.0, 1);
            var offset = 0;
            while (offset < movies.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(100, movies.Count - offset);
                var moviesToSend = movies.GetRange(offset, count);
                var dataContracts = (await _retrakApi.SendLibraryUpdateAsync(
                            moviesToSend,
                            retrakUser,
                            collected ? EventType.Add : EventType.Remove,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                offset += count;
                currentProgress += percentPerRequest;
                progress.Report(currentProgress);
                LogReTrakResponseDataContract(dataContracts, ReTrakItemType.movie);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception handled sending movies to ReTrak");
        }
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
        if (episodes.Count == 0)
        {
            return;
        }

        var collectString = collected ? "add to" : "remove from";
        _logger.LogInformation("Episodes to {State} collection {Count}", collectString, episodes.Count);
        try
        {
            var percentPerRequest = availablePercent / Math.Max(episodes.Count / 100.0, 1);
            var offset = 0;
            while (offset < episodes.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(100, episodes.Count - offset);
                var episodesToSend = episodes.GetRange(offset, count);
                var dataContracts = (await _retrakApi.SendLibraryUpdateAsync(
                            episodesToSend,
                            retrakUser,
                            collected ? EventType.Add : EventType.Remove,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                offset += count;
                currentProgress += percentPerRequest;
                progress.Report(currentProgress);
                LogReTrakResponseDataContract(dataContracts, ReTrakItemType.episode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception handled sending episodes to ReTrak");
        }
    }

    private void LogReTrakResponseDataContract(IReadOnlyCollection<ReTrakSyncResponse> dataContracts, ReTrakItemType type)
    {
        if (dataContracts.Count == 0)
        {
            return;
        }

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

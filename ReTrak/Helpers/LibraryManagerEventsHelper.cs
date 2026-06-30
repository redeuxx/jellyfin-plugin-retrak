using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using ReTrak.Api;
using ReTrak.Model;
using ReTrak.Model.Enums;

namespace ReTrak.Helpers;

internal sealed class LibraryManagerEventsHelper : IDisposable
{
    private readonly List<LibraryEvent> _queuedEvents;
    private readonly ILogger<LibraryManagerEventsHelper> _logger;
    private readonly ReTrakApi _retrakApi;
    private Timer _queueTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryManagerEventsHelper"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="retrakApi">The <see cref="ReTrakApi"/>.</param>
    public LibraryManagerEventsHelper(ILogger<LibraryManagerEventsHelper> logger, ReTrakApi retrakApi)
    {
        _queuedEvents = new List<LibraryEvent>();
        _logger = logger;
        _retrakApi = retrakApi;
    }

    /// <summary>
    /// Queues an item to be added to ReTrak.
    /// </summary>
    /// <param name="item"> The <see cref="BaseItem"/>.</param>
    /// <param name="eventType">The <see cref="EventType"/>.</param>
    public void QueueItem(BaseItem item, EventType eventType)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_queuedEvents)
        {
            if (_queueTimer == null)
            {
                _queueTimer = new Timer(
                    OnQueueTimerCallback,
                    null,
                    TimeSpan.FromMilliseconds(10000),
                    Timeout.InfiniteTimeSpan);
            }
            else
            {
                _queueTimer.Change(TimeSpan.FromMilliseconds(10000), Timeout.InfiniteTimeSpan);
            }

            var users = Plugin.Instance.PluginConfiguration.GetAllReTrakUsers();

            if (users == null || users.Count == 0)
            {
                return;
            }

            // Check if item can be synced for all users.

            foreach (var user in users.Where(user => _retrakApi.CanSync(item, user)))
            {
                // Add to queue.
                // Sync will be processed when the next timer elapsed event fires.
                if (user.SynchronizeCollections && (!user.DontRemoveItemFromReTrak || eventType != EventType.Remove))
                {
                    _queuedEvents.Add(new LibraryEvent { Item = item, ReTrakUser = user, EventType = eventType });
                }
            }
        }
    }

    /// <summary>
    /// Wait for timer callback to be completed.
    /// </summary>
    private async void OnQueueTimerCallback(object state)
    {
        try
        {
            await OnQueueTimerCallbackInternal().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnQueueTimerCallbackInternal");
        }
    }

    /// <summary>
    /// Wait for timer to be completed.
    /// </summary>
    private async Task OnQueueTimerCallbackInternal()
    {
        _logger.LogInformation("Timer elapsed - processing queued items");
        List<LibraryEvent> queue;

        lock (_queuedEvents)
        {
            if (_queuedEvents.Count == 0)
            {
                _logger.LogInformation("No events... stopping queue timer");
                return;
            }

            queue = _queuedEvents.ToList();
            _queuedEvents.Clear();
        }

        var queuedMovieDeletes = new List<LibraryEvent>();
        var queuedMovieAdds = new List<LibraryEvent>();
        var queuedMovieUpdates = new List<LibraryEvent>();
        var queuedEpisodeDeletes = new List<LibraryEvent>();
        var queuedEpisodeAdds = new List<LibraryEvent>();
        var queuedEpisodeUpdates = new List<LibraryEvent>();
        var queuedShowDeletes = new List<LibraryEvent>();
        var queuedShowAdds = new List<LibraryEvent>();
        var queuedShowUpdates = new List<LibraryEvent>();
        var retrakUsers = Plugin.Instance.PluginConfiguration.GetAllReTrakUsers();

        foreach (var retrakUser in retrakUsers.Where(retrakUser => !string.IsNullOrWhiteSpace(retrakUser.AccessToken)))
        {
            queuedMovieDeletes.Clear();
            queuedMovieAdds.Clear();
            queuedMovieUpdates.Clear();
            queuedEpisodeDeletes.Clear();
            queuedEpisodeAdds.Clear();
            queuedEpisodeUpdates.Clear();
            queuedShowDeletes.Clear();
            queuedShowAdds.Clear();
            queuedShowUpdates.Clear();

            foreach (var ev in queue)
            {
                var eventReTrakUserGuid = ev.ReTrakUser.LinkedMbUserId;
                if (eventReTrakUserGuid.Equals(retrakUser.LinkedMbUserId))
                {
                    switch (ev.Item)
                    {
                        case Movie when ev.EventType is EventType.Remove:
                            queuedMovieDeletes.Add(ev);
                            break;
                        case Movie when ev.EventType is EventType.Add:
                            queuedMovieAdds.Add(ev);
                            break;
                        case Movie when ev.EventType is EventType.Update:
                            queuedMovieUpdates.Add(ev);
                            break;
                        case Episode when ev.EventType is EventType.Remove:
                            queuedEpisodeDeletes.Add(ev);
                            break;
                        case Episode when ev.EventType is EventType.Add:
                            queuedEpisodeAdds.Add(ev);
                            break;
                        case Episode when ev.EventType is EventType.Update:
                            queuedEpisodeUpdates.Add(ev);
                            break;
                        case Series when ev.EventType is EventType.Remove:
                            queuedShowDeletes.Add(ev);
                            break;
                        case Series when ev.EventType is EventType.Add:
                            queuedShowAdds.Add(ev);
                            break;
                        case Series when ev.EventType is EventType.Update:
                            queuedShowUpdates.Add(ev);
                            break;
                    }
                }
            }

            await ProcessQueuedMovieEvents(queuedMovieDeletes, retrakUser, EventType.Remove).ConfigureAwait(false);
            await ProcessQueuedMovieEvents(queuedMovieAdds, retrakUser, EventType.Add).ConfigureAwait(false);
            await ProcessQueuedMovieEvents(queuedMovieUpdates, retrakUser, EventType.Update).ConfigureAwait(false);

            await ProcessQueuedEpisodeEvents(queuedEpisodeDeletes, retrakUser, EventType.Remove).ConfigureAwait(false);
            await ProcessQueuedEpisodeEvents(queuedEpisodeAdds, retrakUser, EventType.Add).ConfigureAwait(false);
            await ProcessQueuedEpisodeEvents(queuedEpisodeUpdates, retrakUser, EventType.Update).ConfigureAwait(false);

            await ProcessQueuedShowEvents(queuedShowDeletes, retrakUser, EventType.Remove).ConfigureAwait(false);
            await ProcessQueuedShowEvents(queuedShowAdds, retrakUser, EventType.Add).ConfigureAwait(false);
            await ProcessQueuedShowEvents(queuedShowUpdates, retrakUser, EventType.Update).ConfigureAwait(false);
        }
    }

    private async Task ProcessQueuedShowEvents(IReadOnlyCollection<LibraryEvent> events, ReTrakUser retrakUser, EventType eventType)
    {
        if (events.Count == 0)
        {
            _logger.LogInformation("No shows with event type {EventType} to process", eventType);
            return;
        }

        _logger.LogInformation("Processing {Count} shows with event type {EventType}", events.Count, eventType);

        var shows = events.Select(lev => (Series)lev.Item)
            .Where(lev => !string.IsNullOrEmpty(lev.Name)
                          && (!string.IsNullOrEmpty(lev.GetProviderId(MetadataProvider.Tmdb))
                              || !string.IsNullOrEmpty(lev.GetProviderId(MetadataProvider.Tvdb))
                              || !string.IsNullOrEmpty(lev.GetProviderId(MetadataProvider.Imdb))
                              || !string.IsNullOrEmpty(lev.GetProviderId(MetadataProvider.TvRage)))
                          && !retrakUser.LocationsExcluded.Any(directory => lev.Path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
            .ToHashSet();

        try
        {
            // Should probably not be awaiting this, but it's unlikely a user will be deleting more than one or two shows at a time
            foreach (var show in shows)
            {
                await _retrakApi.SendLibraryUpdateAsync(show, retrakUser, eventType, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled processing queued series events");
        }
    }

    /// <summary>
    /// Processes queued movie events.
    /// </summary>
    /// <param name="events">The <see cref="LibraryEvent"/> enumerable.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <param name="eventType">The <see cref="EventType"/>.</param>
    /// <returns>Task.</returns>
    private async Task ProcessQueuedMovieEvents(IReadOnlyCollection<LibraryEvent> events, ReTrakUser retrakUser, EventType eventType)
    {
        if (events.Count == 0)
        {
            _logger.LogInformation("No movies with event type {EventType} to process", eventType);
            return;
        }

        _logger.LogInformation("Processing {Count} movies with event type {EventType}", events.Count, eventType);

        var movies = events.Select(e => (Movie)e.Item)
            .Where(movie => !string.IsNullOrEmpty(movie.Name)
                          && (!string.IsNullOrEmpty(movie.GetProviderId(MetadataProvider.Tmdb))
                              || !string.IsNullOrEmpty(movie.GetProviderId(MetadataProvider.Imdb)))
                          && !retrakUser.LocationsExcluded.Any(directory => movie.Path is not null && movie.Path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
            .ToHashSet();

        try
        {
            await _retrakApi.SendLibraryUpdateAsync(movies, retrakUser, eventType, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled processing queued movie events");
        }
    }

    /// <summary>
    /// Processes queued episode events.
    /// </summary>
    /// <param name="events">The <see cref="LibraryEvent"/> enumerable.</param>
    /// <param name="retrakUser">The <see cref="ReTrakUser"/>.</param>
    /// <param name="eventType">The <see cref="EventType"/>.</param>
    /// <returns>Task.</returns>
    private async Task ProcessQueuedEpisodeEvents(IReadOnlyCollection<LibraryEvent> events, ReTrakUser retrakUser, EventType eventType)
    {
        try
        {
            if (events.Count == 0)
            {
                _logger.LogInformation("No episodes with event type {EventType} to process", eventType);
                return;
            }

            _logger.LogInformation("Processing {Count} episodes with event type {EventType}", events.Count, eventType);

            var episodes = events.Select(lev => (Episode)lev.Item)
                .Where(lev => lev.Series != null
                            && !string.IsNullOrEmpty(lev.Series.Name)
                            && (!string.IsNullOrEmpty(lev.Series.GetProviderId(MetadataProvider.Tmdb))
                                || !string.IsNullOrEmpty(lev.GetProviderId(MetadataProvider.Tvdb)))
                            && !retrakUser.LocationsExcluded.Any(directory => lev.Path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(i => i.Series.Id)
                .ToList();

            // Can't progress further without episodes
            if (episodes.Count == 0)
            {
                _logger.LogDebug("Episodes count is 0");

                return;
            }

            var payload = new List<Episode>();
            var currentSeriesId = episodes[0].Series.Id;

            foreach (var ep in episodes)
            {
                if (!currentSeriesId.Equals(ep.Series.Id))
                {
                    // We're starting a new series. Time to send the current one to ReTrak
                    await _retrakApi.SendLibraryUpdateAsync(payload, retrakUser, eventType, CancellationToken.None).ConfigureAwait(false);

                    currentSeriesId = ep.Series.Id;
                    payload.Clear();
                }

                payload.Add(ep);
            }

            if (payload.Count != 0)
            {
                await _retrakApi.SendLibraryUpdateAsync(payload, retrakUser, eventType, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled processing queued episode events");
        }
    }

    public void Dispose()
    {
        _queueTimer?.Dispose();
    }
}

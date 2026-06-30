using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.DataContracts.Sync;
using ReTrak.Helpers;

namespace ReTrak.Api;

/// <summary>
/// ReTrak API controller for Jellyfin clients.
/// </summary>
[ApiController]
[Authorize]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class ReTrakController : ControllerBase
{
    private readonly ReTrakApi _retrakApi;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ReTrakController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReTrakController"/> class.
    /// </summary>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    public ReTrakController(
        IUserDataManager userDataManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        ILibraryManager libraryManager,
        IUserManager userManager)
    {
        _logger = loggerFactory.CreateLogger<ReTrakController>();
        _retrakApi = new ReTrakApi(loggerFactory.CreateLogger<ReTrakApi>(), httpClientFactory, appHost, userDataManager, userManager);
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Rate an item on ReTrak.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <param name="itemId">The item id.</param>
    /// <param name="rating">Rating between 1 - 10 (0 = unrate).</param>
    /// <response code="200">Item rated successfully.</response>
    /// <returns>A <see cref="ReTrakSyncResponse"/>.</returns>
    [HttpPost("Users/{userGuid}/Items/{itemId}/Rate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReTrakSyncResponse>> RateItem([FromRoute] Guid userGuid, [FromRoute] Guid itemId, [FromQuery] int rating)
    {
        _logger.LogInformation("RateItem request received");

        var currentItem = _libraryManager.GetItemById(itemId);

        if (currentItem == null)
        {
            _logger.LogInformation("currentItem is null");
            return null;
        }

        return await _retrakApi.SendItemRating(currentItem, rating, UserHelper.GetReTrakUser(userGuid, true)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended ReTrak movies.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Recommended movies returned.</response>
    /// <returns>A list of recommended movies.</returns>
    [HttpPost("Users/{userGuid}/RecommendedMovies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReTrakMovie>>> RecommendedMovies([FromRoute] Guid userGuid)
    {
        return await _retrakApi.SendMovieRecommendationsRequest(UserHelper.GetReTrakUser(userGuid, true)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended ReTrak shows.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Recommended shows returned.</response>
    /// <returns>A list of recommended shows.</returns>
    [HttpPost("Users/{userGuid}/RecommendedShows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReTrakShow>>> RecommendedShows([FromRoute] Guid userGuid)
    {
        return await _retrakApi.SendShowRecommendationsRequest(UserHelper.GetReTrakUser(userGuid, true)).ConfigureAwait(false);
    }
}

#pragma warning disable CA1819

using System;

namespace ReTrak.Model;

/// <summary>
/// ReTrak user class.
/// </summary>
public class ReTrakUser
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReTrakUser"/> class.
    /// </summary>
    public ReTrakUser()
    {
        AccessToken = null;
        LinkedMbUserId = Guid.Empty;
        SkipUnwatchedImportFromReTrak = true;
        SkipWatchedImportFromReTrak = false;
        SkipPlaybackProgressImportFromReTrak = false;
        ExtraLogging = false;
        ExportMediaInfo = false;
        SynchronizeCollections = true;
        Scrobble = true;
        LocationsExcluded = null;
        AccessTokenExpiration = DateTime.MinValue;
        DontRemoveItemFromReTrak = true;
    }

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the linked Mb user id.
    /// </summary>
    public Guid LinkedMbUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip unwatched import option is enabled or not.
    /// </summary>
    public bool SkipUnwatchedImportFromReTrak { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip playback progress import option is enabled or not.
    /// </summary>
    public bool SkipPlaybackProgressImportFromReTrak { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the skip watched import option is enabled or not.
    /// </summary>
    public bool SkipWatchedImportFromReTrak { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether extra logging is enabled or not.
    /// </summary>
    public bool ExtraLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the media info should be exported or not.
    /// </summary>
    public bool ExportMediaInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collections should be synchronized or not.
    /// </summary>
    public bool SynchronizeCollections { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether scrobbling should take place or not.
    /// </summary>
    public bool Scrobble { get; set; }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public string[] LocationsExcluded { get; set; }

    /// <summary>
    /// Gets or sets the access token expiration.
    /// </summary>
    public DateTime AccessTokenExpiration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether item should be removed from ReTrak.
    /// </summary>
    public bool DontRemoveItemFromReTrak { get; set; }
}

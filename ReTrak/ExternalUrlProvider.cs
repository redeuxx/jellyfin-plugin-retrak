using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace ReTrak;

/// <summary>
/// External url provider for ReTrak.
/// </summary>
public class ExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => "ReTrak";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrEmpty(imdbId))
        {
            yield break;
        }

        switch (item)
        {
            case Movie or Trailer or LiveTvProgram { IsMovie: true }:
                yield return $"https://ReTrak/movies/{imdbId}";
                break;
            case Episode:
                yield return $"https://ReTrak/episodes/{imdbId}";
                break;
            case Series:
                yield return $"https://ReTrak/shows/{imdbId}";
                break;
        }
    }
}

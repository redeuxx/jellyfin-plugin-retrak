using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.DataContracts.Sync.History;
using ReTrak.Api.DataContracts.Users.Collection;
using ReTrak.Api.DataContracts.Users.Playback;
using ReTrak.Api.DataContracts.Users.Watched;
using ReTrak.Api.Enums;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace ReTrak;

/// <summary>
/// Class for ReTrak plugin extension functions.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Minimum height for 576p videos.
    /// </summary>
    /// <remarks>
    /// 500px is chosen to catch all videos larger than 480px with 20px deviation.
    /// </remarks>
    private const int MinHeight576P = 500;

    /// <summary>
    /// Minimum width for 576p videos.
    /// </summary>
    /// <remarks>
    /// 630px is chosen to accomodate weird old videos, officially the lowest width for 4:3 576p video is 704px.
    /// </remarks>
    private const int MinWidth576P = 630;

    /// <summary>
    /// Minimum width for 720p videos.
    /// </summary>
    /// <remarks>
    /// 950px is chosen to accomodate 4:3 videos and 10px deviation.
    /// </remarks>
    private const int MinWidth720P = 950;

    /// <summary>
    /// Minimum width for 1080p videos.
    /// </summary>
    /// <remarks>
    /// 1400px is chosen to accomodate 4:3 videos and 40px deviation.
    /// </remarks>
    private const int MinWidth1080P = 1400;

    /// <summary>
    /// Minimum width for 2160p videos.
    /// </summary>
    /// <remarks>
    /// Includes 40px deviation.
    /// </remarks>
    private const int MinWidth2160P = 3800;

    /// <summary>
    /// Convert string to int.
    /// </summary>
    /// <param name="input">String to convert to int.</param>
    /// <returns>int?.</returns>
    public static int? ConvertToInt(this string input)
    {
        if (int.TryParse(input, out int result))
        {
            return result;
        }

        return null;
    }

    /// <summary>
    /// Checks if <see cref="ReTrakMetadata"/> is empty.
    /// </summary>
    /// <param name="metadata">String to convert to int.</param>
    /// <returns><see cref="bool"/> indicating if the provided <see cref="ReTrakMetadata"/> is empty.</returns>
    public static bool IsEmpty(this ReTrakMetadata metadata)
        => metadata.MediaType == null
           && metadata.Resolution == null
           && metadata.Audio == null
           && metadata.Hdr == null
           && string.IsNullOrEmpty(metadata.AudioChannels);

    /// <summary>
    /// Gets the ReTrak codec representation of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="audioStream">The <see cref="MediaStream"/>.</param>
    /// <returns>ReTrakAudio.</returns>
    public static ReTrakAudio? GetCodecRepresetation(this MediaStream audioStream)
    {
        var audio = audioStream != null && !string.IsNullOrEmpty(audioStream.Codec)
            ? audioStream.Codec.ToLowerInvariant().Replace(' ', '_')
            : null;
        switch (audio)
        {
            case "aac":
                return ReTrakAudio.aac;
            case "ac3":
                return ReTrakAudio.dolby_digital;
            case "dca":
            case "dts":
                return ReTrakAudio.dts;
            case "eac3":
                return ReTrakAudio.dolby_digital_plus;
            case "flac":
                return ReTrakAudio.flac;
            case "mp2":
                return ReTrakAudio.mp2;
            case "mp3":
                return ReTrakAudio.mp3;
            case "ogg":
            case "vorbis":
                return ReTrakAudio.ogg;
            case "opus":
                return ReTrakAudio.ogg_opus;
            case not null when audio.StartsWith("pcm_", StringComparison.Ordinal):
                return ReTrakAudio.lpcm;
            case "truehd":
                return ReTrakAudio.dolby_truehd;
            case "wma":
            case "wmav2":
            case "wmapro":
            case "wmavoice":
                return ReTrakAudio.wma;
            default:
                return null;
        }
    }

    /// <summary>
    /// Checks if metadata of new collected movie is different from the already collected.
    /// </summary>
    /// <param name="collectedMovie">The <see cref="ReTrakMovieCollected"/>.</param>
    /// <param name="movie">The <see cref="Movie"/>.</param>
    /// <returns><see cref="bool"/> indicating if the new movie has different metadata to the already collected.</returns>
    public static bool MetadataIsDifferent(this ReTrakMovieCollected collectedMovie, Movie movie)
    {
        var match = false;
        var mediaStreams = movie.GetMediaStreams();
        var defaultVideoStream = mediaStreams.FirstOrDefault(x => x.Index == movie.DefaultVideoStreamIndex);
        var audioStream = mediaStreams.FirstOrDefault(x => x.Type == MediaStreamType.Audio);

        if (defaultVideoStream != null)
        {
            var is3D = movie.Is3D;
            var resolution = defaultVideoStream.GetResolution();
            var hdr = defaultVideoStream.GetHdr();
            match = match || collectedMovie.Metadata.Resolution != resolution || collectedMovie.Metadata.Is3D != is3D || collectedMovie.Metadata.Hdr != hdr;
        }

        if (audioStream != null)
        {
            var audio = GetCodecRepresetation(audioStream);
            var audioChannels = audioStream.GetAudioChannels();
            match = match || collectedMovie.Metadata.Audio != audio || collectedMovie.Metadata.AudioChannels != audioChannels;
        }

        return match || collectedMovie.Metadata.MediaType != ReTrakMediaType.digital;
    }

    /// <summary>
    /// Checks if metadata of new collected episode is different from the already collected.
    /// </summary>
    /// <param name="collectedEpisode">The <see cref="ReTrakEpisodeCollected"/>.</param>
    /// <param name="episode">The <see cref="Episode"/>.</param>
    /// <returns><see cref="bool"/> indicating if the new episode has different metadata to the already collected.</returns>
    public static bool MetadataIsDifferent(this ReTrakEpisodeCollected collectedEpisode, Episode episode)
    {
        var match = false;
        var mediaStreams = episode.GetMediaStreams();
        var defaultVideoStream = mediaStreams.FirstOrDefault(x => x.Index == episode.DefaultVideoStreamIndex);
        var audioStream = mediaStreams.FirstOrDefault(x => x.Type == MediaStreamType.Audio);

        if (defaultVideoStream != null)
        {
            var is3D = episode.Is3D;
            var resolution = defaultVideoStream.GetResolution();
            var hdr = defaultVideoStream.GetHdr();
            match = match || collectedEpisode.Metadata.Resolution != resolution || collectedEpisode.Metadata.Is3D != is3D || collectedEpisode.Metadata.Hdr != hdr;
        }

        if (audioStream != null)
        {
            var audio = GetCodecRepresetation(audioStream);
            var audioChannels = audioStream.GetAudioChannels();
            match = match || collectedEpisode.Metadata.Audio != audio || collectedEpisode.Metadata.AudioChannels != audioChannels;
        }

        return match || collectedEpisode.Metadata.MediaType != ReTrakMediaType.digital;
    }

    /// <summary>
    /// Gets the resolution of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="videoStream">The <see cref="MediaStream"/>.</param>
    /// <returns>string.</returns>
    public static ReTrakResolution? GetResolution(this MediaStream videoStream)
    {
        if (videoStream == null)
        {
            return null;
        }

        if (!videoStream.Width.HasValue)
        {
            return null;
        }

        if (videoStream.Width.Value >= MinWidth2160P)
        {
            return ReTrakResolution.uhd_4k;
        }

        if (videoStream.Width.Value >= MinWidth1080P)
        {
            return videoStream.IsInterlaced ? ReTrakResolution.hd_1080i : ReTrakResolution.hd_1080p;
        }

        if (videoStream.Width.Value >= MinWidth720P)
        {
            return ReTrakResolution.hd_720p;
        }

        if (videoStream.Width.Value >= MinWidth576P && videoStream.Height.HasValue && videoStream.Height.Value >= MinHeight576P)
        {
            return videoStream.IsInterlaced ? ReTrakResolution.sd_576i : ReTrakResolution.sd_576p;
        }

        // Set 480p as fallback since ReTrak does not allow lower resolutions
        return videoStream.IsInterlaced ? ReTrakResolution.sd_480i : ReTrakResolution.sd_480p;
    }

    /// <summary>
    /// Gets the HDR type of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="videoStream">The <see cref="MediaStream"/>.</param>
    /// <returns>string.</returns>
    public static ReTrakHdr? GetHdr(this MediaStream videoStream)
    {
        if (videoStream.DvProfile != null)
        {
            return ReTrakHdr.dolby_vision;
        }

        var rageType = videoStream.VideoRangeType;
        return rageType switch
        {
            VideoRangeType.DOVI => ReTrakHdr.dolby_vision,
            VideoRangeType.HDR10 => ReTrakHdr.hdr10,
            VideoRangeType.HLG => ReTrakHdr.hlg,
            _ => null
        };
    }

    /// <summary>
    /// Gets the ISO-8601 representation of a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/>.</param>
    /// <returns>string.</returns>
    public static string ToISO8601(this DateTime dateTime)
        => dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the season number of an <see cref="Episode"/>.
    /// </summary>
    /// <param name="episode">The <see cref="Episode"/>.</param>
    /// <returns>int.</returns>
    public static int GetSeasonNumber(this Episode episode)
        => (episode.ParentIndexNumber != 0 ? episode.ParentIndexNumber ?? 1 : episode.ParentIndexNumber).Value;

    /// <summary>
    /// Gets the number of audio channels of a <see cref="MediaStream"/>.
    /// </summary>
    /// <param name="audioStream">The <see cref="MediaStream"/>.</param>
    /// <returns>string.</returns>
    public static string GetAudioChannels(this MediaStream audioStream)
    {
        if (audioStream == null || string.IsNullOrEmpty(audioStream.ChannelLayout))
        {
            return null;
        }

        var channels = audioStream.ChannelLayout;
        switch (channels)
        {
            case "22.2":
            case "hexadecagonal":
                return "10.1";
            case "9.1.4":
                return "9.1";
            case "7.2.3":
            case "7.1.4":
                return "7.1.4";
            case "7.1.2":
                return "7.1.2";
            case "octagonal":
            case "7.1(wide-side)":
            case "7.1(wide)":
            case "7.1":
                return "7.1";
            case "7.0(front)":
            case "7.0":
            case "6.1(front)":
            case "6.1(back)":
            case "6.1":
                return "6.1";
            case "cube":
            case "5.1.4":
                return "5.1.4";
            case "5.1.2":
                return "5.1.2";
            case "hexagonal":
            case "6.0(front)":
            case "6.0":
            case "5.1(side)":
            case "5.1":
                return "5.1";
            case "5.0(side)":
            case "5.0":
                return "5.0";
            case "4.1":
                return "4.1";
            case "quad(side)":
            case "quad":
            case "4.0":
                return "4.0";
            case "3.1.2":
            case "3.1":
                return "3.1";
            case "3.0(back)":
            case "3.0":
                return "3.0";
            case "2.1":
                return "2.1";
            case "downmix":
            case "stereo":
                return "2.0";
            case "mono":
                return "1.0";
            default:
                return null;
        }
    }

    /// <summary>
    /// Gets a watched match for a series.
    /// </summary>
    /// <param name="item">The <see cref="Series"/>.</param>
    /// <param name="results">The <see cref="IEnumerable{ReTrakShowWatched}"/>.</param>
    /// <returns>ReTrakShowWatched.</returns>
    public static ReTrakShowWatched FindMatch(Series item, IEnumerable<ReTrakShowWatched> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Show));
    }

    /// <summary>
    /// Gets a collected match for a series.
    /// </summary>
    /// <param name="item">The <see cref="Series"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakShowCollected}"/>.</param>
    /// <returns>ReTrakShowCollected.</returns>
    public static ReTrakShowCollected FindMatch(Series item, IEnumerable<ReTrakShowCollected> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Show));
    }

    /// <summary>
    /// Gets a paused match for a series.
    /// </summary>
    /// <param name="item">The <see cref="Episode"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakShowCollected}"/>.</param>
    /// <returns>ReTrakShowCollected.</returns>
    public static ReTrakEpisodePaused FindMatch(Episode item, IEnumerable<ReTrakEpisodePaused> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Episode));
    }

    /// <summary>
    /// Gets a watched match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakMovieWatched}"/>.</param>
    /// <returns>ReTrakMovieWatched.</returns>
    public static ReTrakMovieWatched FindMatch(BaseItem item, IEnumerable<ReTrakMovieWatched> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a collected match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakMovieCollected}"/>.</param>
    /// <returns>ReTrakMovieCollected.</returns>
    public static ReTrakMovieCollected FindMatch(BaseItem item, IEnumerable<ReTrakMovieCollected> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a paused match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakMoviePaused}"/>.</param>
    /// <returns>ReTrakMoviePaused.</returns>
    public static ReTrakMoviePaused FindMatch(BaseItem item, IEnumerable<ReTrakMoviePaused> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a watched history match for a movie.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakMovieWatchedHistory}"/>.</param>
    /// <returns>ReTrakMovieWatchedHistory.</returns>
    public static ReTrakMovieWatchedHistory FindMatch(Movie item, IEnumerable<ReTrakMovieWatchedHistory> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Movie));
    }

    /// <summary>
    /// Gets a watched history match for an episode.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakEpisodeWatchedHistory}"/>.</param>
    /// <returns>ReTrakEpisodeWatchedHistory.</returns>
    public static ReTrakEpisodeWatchedHistory FindMatch(Episode item, IEnumerable<ReTrakEpisodeWatchedHistory> results)
    {
        return results.FirstOrDefault(i => IsMatch(item, i.Episode));
    }

    /// <summary>
    /// Gets all watched history matches for an episode.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="results">>The <see cref="IEnumerable{ReTrakEpisodeWatchedHistory}"/>.</param>
    /// <returns>IEnumerable{ReTrakEpisodeWatchedHistory}.</returns>
    public static IReadOnlyList<ReTrakEpisodeWatchedHistory> FindAllMatches(Episode item, IEnumerable<ReTrakEpisodeWatchedHistory> results)
    {
        return results.Where(i => IsMatch(item, i)).ToList();
    }

    /// <summary>
    /// Checks if a <see cref="BaseItem"/> matches a <see cref="ReTrakMovie"/>.
    /// </summary>
    /// <param name="item">The <see cref="BaseItem"/>.</param>
    /// <param name="movie">The IEnumerable of <see cref="ReTrakMovie"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="BaseItem"/> matches a <see cref="ReTrakMovie"/>.</returns>
    private static bool IsMatch(BaseItem item, ReTrakMovie movie)
    {
        if (item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
            && string.Equals(imdbId, movie.Ids.Imdb, StringComparison.Ordinal))
        {
            return true;
        }

        if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
            && string.Equals(tmdbId, movie.Ids.Tmdb?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a <see cref="Series"/> matches a <see cref="ReTrakShow"/>.
    /// </summary>
    /// <param name="item">The <see cref="Series"/>.</param>
    /// <param name="show">The <see cref="ReTrakShow"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="Series"/> matches a <see cref="ReTrakShow"/>.</returns>
    private static bool IsMatch(Series item, ReTrakShow show)
    {
        if (item.TryGetProviderId(MetadataProvider.Tvdb, out var tvdbId)
            && show.Ids.Tvdb.HasValue
            && string.Equals(tvdbId, show.Ids.Tvdb.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return true;
        }

        if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
            && string.Equals(tmdbId, show.Ids.Tmdb?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return true;
        }

        if (item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
            && string.Equals(imdbId, show.Ids.Imdb, StringComparison.Ordinal))
        {
            return true;
        }

        if (item.TryGetProviderId(MetadataProvider.TvRage, out var tvRageId)
            && show.Ids.Tvrage.HasValue
            && string.Equals(tvRageId, show.Ids.Tvrage.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a <see cref="Episode"/> matches a <see cref="ReTrakEpisode"/>.
    /// </summary>
    /// <param name="item">The <see cref="Episode"/>.</param>
    /// <param name="episode">The <see cref="ReTrakEpisode"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="Episode"/> matches a <see cref="ReTrakEpisode"/>.</returns>
    public static bool IsMatch(Episode item, ReTrakEpisode episode)
    {
        var tvdb = item.GetProviderId(MetadataProvider.Tvdb);
        if (!string.IsNullOrEmpty(tvdb) && episode.Ids.Tvdb.HasValue
            && string.Equals(tvdb, episode.Ids.Tvdb.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tmdb = item.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrEmpty(tmdb) && string.Equals(tmdb, episode.Ids.Tmdb?.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var imdb = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdb) && string.Equals(imdb, episode.Ids.Imdb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tvrage = item.GetProviderId(MetadataProvider.TvRage);
        if (!string.IsNullOrEmpty(tvrage) && episode.Ids.Tvrage.HasValue
            && string.Equals(tvrage, episode.Ids.Tvrage.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a <see cref="Episode"/> matches a <see cref="ReTrakEpisodeWatchedHistory"/>.
    /// </summary>
    /// <param name="item">The <see cref="Episode"/>.</param>
    /// <param name="episodeHistory">The <see cref="ReTrakEpisodeWatchedHistory"/>.</param>
    /// <returns><see cref="bool"/> indicating if the <see cref="Episode"/> matches a <see cref="ReTrakEpisodeWatchedHistory"/>.</returns>
    public static bool IsMatch(Episode item, ReTrakEpisodeWatchedHistory episodeHistory)
    {
        // Match by provider id's
        if (IsMatch(item, episodeHistory.Episode))
        {
            return true;
        }

        // Match by show, season and episode number if there isn't any provider id in common
        // If there was a common provider id between the item and the ReTrak episode (f.e. both have tvdb id), you shouldn't check anymore by season/number
        if (!HasAnyProviderTvIdInCommon(item, episodeHistory.Episode)
            && IsMatch(item.Series, episodeHistory.Show)
            && item.GetSeasonNumber() == episodeHistory.Episode.Season
            && item.ContainsEpisodeNumber(episodeHistory.Episode.Number))
        {
            return true;
        }

        return false;
    }

    private static bool HasAnyProviderTvIdInCommon(Episode item, ReTrakEpisode retrakEpisode)
    {
        return (item.HasProviderId(MetadataProvider.Tvdb) && retrakEpisode.Ids.Tvdb != null)
            || (item.HasProviderId(MetadataProvider.Imdb) && retrakEpisode.Ids.Imdb != null)
            || (item.HasProviderId(MetadataProvider.Tmdb) && retrakEpisode.Ids.Tmdb != null)
            || (item.HasProviderId(MetadataProvider.TvRage) && retrakEpisode.Ids.Tvrage != null);
    }
}

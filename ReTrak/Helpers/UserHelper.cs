using System;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using ReTrak.Model;

namespace ReTrak.Helpers;

internal static class UserHelper
{
    public static ReTrakUser GetReTrakUser(string userId, bool authorized = false)
    {
        return GetReTrakUser(Guid.Parse(userId), authorized);
    }

    public static ReTrakUser GetReTrakUser(User user, bool authorized = false)
    {
        return GetReTrakUser(user.Id, authorized);
    }

    public static ReTrakUser GetReTrakUser(Guid userGuid, bool authorized = false)
    {
        var retrakUsers = Plugin.Instance.PluginConfiguration.GetAllReTrakUsers();
        if (retrakUsers.Count == 0)
        {
            return null;
        }

        return retrakUsers.FirstOrDefault(user =>
        {
            if (user.LinkedMbUserId == Guid.Empty
                || (authorized && string.IsNullOrWhiteSpace(user.AccessToken)))
            {
                return false;
            }

            if (user.LinkedMbUserId.Equals(userGuid))
            {
                return true;
            }

            return false;
        });
    }
}

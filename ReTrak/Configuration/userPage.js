const pluginUniqueId = '4e2945d8-c6df-4613-bc75-c54d193d58ef';

function loadUserConfiguration(userId, page) {
    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
        config = config || {};
        config.ReTrakUsers = config.ReTrakUsers || [];
        let currentUserConfig = config.ReTrakUsers.filter(function (u) {
            return u.LinkedMbUserId == userId;
        })[0];
        if (!currentUserConfig) {
            currentUserConfig = {
                AccessToken: '',
                SkipUnwatchedImportFromReTrak: true,
                PostWatchedHistory: true,
                SynchronizeCollections: true,
                ExtraLogging: false,
                ExportMediaInfo: true
            };
        }
        page.querySelector('#txtUserAccessToken').value = currentUserConfig.AccessToken || '';
        page.querySelector('#chkUserSkipUnwatchedImportFromReTrak').checked = currentUserConfig.SkipUnwatchedImportFromReTrak;
        page.querySelector('#chkUserPostWatchedHistory').checked = currentUserConfig.PostWatchedHistory;
        page.querySelector('#chkUserSyncCollection').checked = currentUserConfig.SynchronizeCollections;
        page.querySelector('#chkUserExtraLogging').checked = currentUserConfig.ExtraLogging;
        page.querySelector('#chkUserExportMediaInfo').checked = currentUserConfig.ExportMediaInfo;
        Dashboard.hideLoadingMsg();
    });
}

export default function (view) {
    view.querySelector('#retrakUserConfigurationForm').addEventListener('submit', function (e) {
        e.preventDefault();
        const userId = ApiClient.getCurrentUserId();
        ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
            config = config || {};
            config.ReTrakUsers = config.ReTrakUsers || [];
            let currentUserConfig = config.ReTrakUsers.filter(function (u) {
                return u.LinkedMbUserId == userId;
            })[0];
            if (!currentUserConfig) {
                currentUserConfig = { LinkedMbUserId: userId };
                config.ReTrakUsers.push(currentUserConfig);
            }
            currentUserConfig.AccessToken = view.querySelector('#txtUserAccessToken').value;
            currentUserConfig.SkipUnwatchedImportFromReTrak = view.querySelector('#chkUserSkipUnwatchedImportFromReTrak').checked;
            currentUserConfig.PostWatchedHistory = view.querySelector('#chkUserPostWatchedHistory').checked;
            currentUserConfig.SynchronizeCollections = view.querySelector('#chkUserSyncCollection').checked;
            currentUserConfig.ExtraLogging = view.querySelector('#chkUserExtraLogging').checked;
            currentUserConfig.ExportMediaInfo = view.querySelector('#chkUserExportMediaInfo').checked;
            currentUserConfig.LinkedMbUserId = userId;
            ApiClient.updatePluginConfiguration(pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
        return false;
    });

    view.addEventListener('viewshow', function () {
        loadUserConfiguration(ApiClient.getCurrentUserId(), view);
    });
}

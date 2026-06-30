const ReTrakConfigurationPage = {
    pluginUniqueId: '4e2945d8-c6df-4613-bc75-c54d193d58ef',
    loadConfiguration: function (userId, page) {
        ApiClient.getPluginConfiguration(ReTrakConfigurationPage.pluginUniqueId).then(function (config) {
            config = config || {};
            config.ReTrakUsers = config.ReTrakUsers || [];
            let currentUserConfig = config.ReTrakUsers.filter(function (curr) {
                return curr.LinkedMbUserId == userId;
            })[0];
            if (!currentUserConfig) {
                currentUserConfig = {
                    AccessToken: '',
                    SkipUnwatchedImportFromReTrak: true,
                    SkipWatchedImportFromReTrak: false,
                    SkipPlaybackProgressImportFromReTrak: false,
                    PostWatchedHistory: true,
                    PostUnwatchedHistory: false,
                    PostSetWatched: true,
                    PostSetUnwatched: false,
                    ExtraLogging: false,
                    ExportMediaInfo: true,
                    SynchronizeCollections: true,
                    Scrobble: true,
                    DontRemoveItemFromReTrak: true
                };
            }
            currentUserConfig.LocationsExcluded = currentUserConfig.LocationsExcluded || [];
            page.querySelector('#txtReTrakUrl').value = config.ReTrakUrl || 'https://retrak.tv';
            page.querySelector('#txtAccessToken').value = currentUserConfig.AccessToken || '';
            page.querySelector('#chkSkipUnwatchedImportFromReTrak').checked = currentUserConfig.SkipUnwatchedImportFromReTrak;
            page.querySelector('#chkSkipWatchedImportFromReTrak').checked = currentUserConfig.SkipWatchedImportFromReTrak;
            page.querySelector('#chkSkipPlaybackProgressImportFromReTrak').checked = currentUserConfig.SkipPlaybackProgressImportFromReTrak;
            page.querySelector('#chkPostWatchedHistory').checked = currentUserConfig.PostWatchedHistory;
            page.querySelector('#chkPostUnwatchedHistory').checked = currentUserConfig.PostUnwatchedHistory;
            page.querySelector('#chkPostSetWatched').checked = currentUserConfig.PostSetWatched;
            page.querySelector('#chkPostSetUnwatched').checked = currentUserConfig.PostSetUnwatched;
            page.querySelector('#chkExtraLogging').checked = currentUserConfig.ExtraLogging;
            page.querySelector('#chkExportMediaInfo').checked = currentUserConfig.ExportMediaInfo;
            page.querySelector('#chkSyncCollections').checked = currentUserConfig.SynchronizeCollections;
            page.querySelector('#chkScrobble').checked = currentUserConfig.Scrobble;
            page.querySelector('#chkDontRemoveItemFromReTrak').checked = currentUserConfig.DontRemoveItemFromReTrak;
            ApiClient.getVirtualFolders(userId).then(function (result) {
                ReTrakConfigurationPage.loadFolders(currentUserConfig, result, page);
            });
            Dashboard.hideLoadingMsg();
        });
    },
    populateUsers: function (users) {
        let html = '';
        for (let i = 0, length = users.length; i < length; i++) {
            const user = users[i];
            html += '<option value="' + user.Id + '">' + user.Name + '</option>';
        }
        document.querySelector('#selectUser').innerHTML = html;
    },
    loadFolders: function (currentUserConfig, virtualFolders, page) {
        let html = '';
        html += '<div data-role="controlgroup">';
        for (let i = 0, length = virtualFolders.length; i < length; i++) {
            const virtualFolder = virtualFolders[i];
            html += ReTrakConfigurationPage.getFolderHtml(currentUserConfig, virtualFolder, i);
        }
        html += '</div>';
        const divReTrakLocations = page.querySelector('#divReTrakLocations');
        divReTrakLocations.innerHTML = html;
        divReTrakLocations.dispatchEvent(new Event('create'));
    },
    getFolderHtml: function (currentUserConfig, virtualFolder, index) {
        let html = '';
        for (let i = 0, length = virtualFolder.Locations.length; i < length; i++) {
            const id = 'chkFolder' + index + '_' + i;
            const location = virtualFolder.Locations[i];
            const isChecked = currentUserConfig.LocationsExcluded.filter(function (current) {
                return current.toLowerCase() == location.toLowerCase();
            }).length;
            const checkedAttribute = isChecked ? 'checked="checked"' : '';
            html += '<label><input is="emby-checkbox" class="chkReTrakLocation" type="checkbox" data-mini="true" id="' + id + '" name="' + id + '" data-location="' + location + '" ' + checkedAttribute + ' /><span>' + location + '</span></label>';
        }
        return html;
    }
};

function save(page) {
    return new Promise((resolve) => {
        const currentUserId = page.querySelector('#selectUser').value;
        ApiClient.getPluginConfiguration(ReTrakConfigurationPage.pluginUniqueId).then(function (config) {
            config = config || {};
            config.ReTrakUsers = config.ReTrakUsers || [];
            let currentUserConfig = config.ReTrakUsers.filter(function (curr) {
                return curr.LinkedMbUserId == currentUserId;
            })[0];
            if (!currentUserConfig) {
                currentUserConfig = {};
                config.ReTrakUsers.push(currentUserConfig);
            }
            currentUserConfig.SkipUnwatchedImportFromReTrak = page.querySelector('#chkSkipUnwatchedImportFromReTrak').checked;
            currentUserConfig.SkipWatchedImportFromReTrak = page.querySelector('#chkSkipWatchedImportFromReTrak').checked;
            currentUserConfig.SkipPlaybackProgressImportFromReTrak = page.querySelector('#chkSkipPlaybackProgressImportFromReTrak').checked;
            currentUserConfig.PostWatchedHistory = page.querySelector('#chkPostWatchedHistory').checked;
            currentUserConfig.PostUnwatchedHistory = page.querySelector('#chkPostUnwatchedHistory').checked;
            currentUserConfig.PostSetWatched = page.querySelector('#chkPostSetWatched').checked;
            currentUserConfig.PostSetUnwatched = page.querySelector('#chkPostSetUnwatched').checked;
            currentUserConfig.ExtraLogging = page.querySelector('#chkExtraLogging').checked;
            currentUserConfig.ExportMediaInfo = page.querySelector('#chkExportMediaInfo').checked;
            currentUserConfig.SynchronizeCollections = page.querySelector('#chkSyncCollections').checked;
            currentUserConfig.Scrobble = page.querySelector('#chkScrobble').checked;
            currentUserConfig.DontRemoveItemFromReTrak = page.querySelector('#chkDontRemoveItemFromReTrak').checked;
            currentUserConfig.AccessToken = page.querySelector('#txtAccessToken').value;
            currentUserConfig.LinkedMbUserId = currentUserId;
            currentUserConfig.LocationsExcluded = Array.prototype.map.call(page.querySelectorAll('.chkReTrakLocation:checked'), elem => {
                return elem.getAttribute('data-location');
            });
            config.ReTrakUrl = page.querySelector('#txtReTrakUrl').value;
            ApiClient.updatePluginConfiguration(ReTrakConfigurationPage.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
                ApiClient.getUsers().then(function (users) {
                    const currentUserId = page.querySelector('#selectUser').value;
                    ReTrakConfigurationPage.populateUsers(users);
                    page.querySelector('#selectUser').value = currentUserId;
                    ReTrakConfigurationPage.loadConfiguration(currentUserId, page);
                    resolve();
                });
            });
        });
    });
}

export default function (view) {
    view.querySelector('#selectUser').addEventListener('change', function () {
        ReTrakConfigurationPage.loadConfiguration(this.value, view);
    });

    view.querySelector('#retrakConfigurationForm').addEventListener('submit', function (e) {
        save(view);
        e.preventDefault();
        return false;
    });

    view.addEventListener('viewshow', function () {
        const page = this;
        ApiClient.getUsers().then(function (users) {
            ReTrakConfigurationPage.populateUsers(users);
            const currentUserId = page.querySelector('#selectUser').value;
            ReTrakConfigurationPage.loadConfiguration(currentUserId, page);
        });
    });
}

# ReTrak for Jellyfin

ReTrak for Jellyfin is a server plugin that scrobbles playback progress and synchronizes watched history and collections with your [ReTrak](https://retrak.tv) profile.

## Features

- **Real-time scrobbles**: Reports play, pause, resume, and stop events from any Jellyfin client to ReTrak.
- **Library synchronization**: Syncs movie and show watched states between Jellyfin and ReTrak.
- **Collection sync**: Syncs your Jellyfin library to your ReTrak collection.
- **Per-user API keys**: Admins configure the server URL; each user supplies their own `dnt_` API key.

## Requirements

- Jellyfin 10.11+
- .NET 9 SDK (for building from source)
- A ReTrak account with an API key from **Settings**

## Installation

### From the plugin catalog (recommended)

1. In Jellyfin, go to **Dashboard > Plugins > Repositories**.
2. Click **+** and add this manifest URL:

   ```text
   https://raw.githubusercontent.com/redeuxx/jellyfin-plugin-retrak/master/manifest-release/manifest.json
   ```

3. Restart Jellyfin (or refresh the plugin catalog).
4. Go to **Dashboard > Plugins > Catalog**, find **ReTrak**, and install.
5. Restart Jellyfin again after installation.

### From a GitHub release (manual)

1. Download the latest plugin zip from [GitHub Releases](https://github.com/redeuxx/jellyfin-plugin-retrak/releases).
2. Extract `ReTrak.dll` into `<jellyfin-data>/plugins/ReTrak/`.
3. Ensure the plugin folder is writable by the Jellyfin service user.
4. Restart Jellyfin.

### Build from source

```bash
dotnet publish ReTrak/ReTrak.csproj --configuration Release --output bin
```

Copy `ReTrak.dll` into your Jellyfin `plugins/ReTrak` folder and restart the server.

## Configuration

### Admin setup

1. Open **Dashboard > Plugins > ReTrak**.
2. Set the **ReTrak URL** (defaults to `https://retrak.tv`).
3. Select a Jellyfin user and paste their **ReTrak API key** (`dnt_...`).
4. Adjust sync and scrobble options, then save.

### Per-user setup

Users who are not admins can open this URL directly (replace the host with your server):

```text
https://<your-jellyfin-server>/web/index.html#!/configurationpage?name=retrakuser
```

They can enter their own API key and sync preferences there.

## Scheduled tasks

Two tasks appear under **Dashboard > Scheduled Tasks**:

| Task | Purpose |
| --- | --- |
| Import watched states and playback progress from ReTrak | Pulls watch state from ReTrak into Jellyfin |
| Export library playstates to ReTrak | Pushes Jellyfin watch state and collections to ReTrak |

Run these on a schedule that fits your library size and sync needs.

## Related projects

- [ReTrak](https://github.com/redeuxx/ReTrak) - the web app and API
- [retrak-emby](https://github.com/redeuxx/retrak-emby) - the Emby server plugin

## License

MIT. See [LICENSE.md](./LICENSE.md).

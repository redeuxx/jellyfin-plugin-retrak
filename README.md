# ReTrak for Jellyfin

ReTrak for Jellyfin is a server plugin that scrobbles playback progress and synchronizes watched history and collections with your [ReTrak](https://retrak.tv) profile.

## Features

- **Real-time scrobbles**: Reports play, pause, resume, and stop events from any Jellyfin client to ReTrak.
- **Library synchronization**: Syncs movie and show watched states between Jellyfin and ReTrak.
- **Collection sync**: Syncs your Jellyfin library to your ReTrak collection.
- **Per-user API keys**: Admins configure the server URL; each user supplies their own `dnt_` API key.

## Requirements

- Jellyfin 10.11+
- A ReTrak account with an API key from **Settings**

To build or publish from source:

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [JPRM](https://pypi.org/project/jprm/) (`pip install jprm`) for catalog packaging
- [GitHub CLI](https://cli.github.com/) (`gh auth login`) for releases

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

   ```bash
   # Linux example
   sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/ReTrak
   ```

4. Restart Jellyfin.

### Build from source

From the repo root, use the build script:

```powershell
.\build.ps1
```

Output is written to `dist/ReTrak.dll`. Copy it into your Jellyfin plugins folder:

```text
<jellyfin-data>/plugins/ReTrak/ReTrak.dll
```

Then restart Jellyfin.

You can also build with `dotnet` directly:

```powershell
dotnet publish ReTrak/ReTrak.csproj --configuration Release --output dist
```

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

## Developing and publishing

These steps are for maintainers releasing a new plugin version.

### Build only

Build the DLL locally without changing version metadata or touching git:

```powershell
.\build.ps1
```

### Test a release build locally

Build the plugin, create the JPRM catalog zip, and update `manifest-release/manifest.json` without pushing to GitHub:

```powershell
.\publish.ps1 -Version 1.0.2 -Changelog "Fix episode sync for multi-user setups" -SkipPublish
```

This writes:

- `dist/ReTrak.dll`
- `dist/retrak_1.0.2.0.zip` (catalog install package)

### Publish a release to GitHub

Publish a full release (version bump, manifest update, git tag, GitHub release):

```powershell
.\publish.ps1 -Version 1.0.2 -Changelog "Fix episode sync for multi-user setups"
```

You will be prompted to confirm before commit, push, and release creation.

With custom GitHub release notes from a file:

```powershell
.\publish.ps1 `
  -Version 1.0.2 `
  -Changelog "Fix episode sync for multi-user setups" `
  -ReleaseNotesFile .\release-notes.md
```

With inline release notes instead of a file:

```powershell
.\publish.ps1 `
  -Version 1.0.2 `
  -Changelog "Fix episode sync" `
  -ReleaseNotes "Fixes episode sync when multiple Jellyfin users share one library."
```

Re-publish over an existing tag (use with care):

```powershell
.\publish.ps1 -Version 1.0.2 -Changelog "Fix episode sync" -Force
```

### Version format

| You pass | Assembly / catalog version | Git tag | Zip filename |
| --- | --- | --- | --- |
| `1.0.2` | `1.0.2.0` | `v1.0.2` | `retrak_1.0.2.0.zip` |
| `1.0.2.0` | `1.0.2.0` | `v1.0.2` | `retrak_1.0.2.0.zip` |

### Publish prerequisites

Install JPRM once:

```powershell
pip install jprm
```

Authenticate GitHub CLI once:

```powershell
gh auth login
```

## Related projects

- [ReTrak](https://github.com/redeuxx/ReTrak) - the web app and API
- [retrak-emby](https://github.com/redeuxx/retrak-emby) - the Emby server plugin

## License

MIT. See [LICENSE.md](./LICENSE.md).

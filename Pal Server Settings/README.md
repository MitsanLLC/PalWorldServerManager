# Pal Server Hub

**Version 0.9.0 Preview**

Pal Server Hub is a Windows desktop application for managing a Palworld Dedicated Server from one interface.

**Developed by Stancean — a Mitsan LLC product**

- Product: https://palworldserverhub.com
- Short domain: https://palserverhub.com
- Developer: https://stancean.com
- Support development: https://ko-fi.com/mitsanllc

> Pal Server Hub is an independent community tool and is not affiliated with or endorsed by Pocketpair.

## Features

- Start, graceful stop, restart, and force-stop PalServer
- Attach to an already-running PalServer process
- First-run Initial Setup wizard
- Remember and automatically reload the last `PalWorldSettings.ini`
- Server Health + live metrics
- Startup diagnostics
- Palworld REST API integration and player administration
- Live server console and admin commands
- Persistent daily activity logs
- Manual and scheduled backups
- Backup retention
- Scheduled saves and restarts
- PAK mod management
- Automatic crash recovery
- Discord webhook notifications
- Optional SteamCMD update-before-start
- Optional start with Windows
- Built-in Help & Guides

## Initial Setup

On first launch:

1. Select `PalServer.exe`.
2. Select the active `PalWorldSettings.ini`.
3. Optionally select `steamcmd.exe`.
4. Choose startup preferences.
5. Finish setup.
6. Open **Settings > Network** and verify REST API settings.
7. Start PalServer and confirm Server Health becomes Healthy.

The settings file is normally located under:

```text
Pal\Saved\Config\WindowsServer\PalWorldSettings.ini
```

## SteamCMD

SteamCMD is optional. A simple installation location is:

```text
C:\SteamCMD\steamcmd.exe
```

Run SteamCMD once manually, then select it under:

**Settings > Advanced > SteamCMD**

## Discord Notifications

Create a webhook from:

**Discord channel > Edit Channel > Integrations > Webhooks**

Then open:

**Settings > Notifications**

Paste the webhook URL and click **Send Test**.

Treat webhook URLs like passwords. Never commit or publish them.

## Support Development

Pal Server Hub is independently developed. If the app is useful to you, development can be supported at:

https://ko-fi.com/mitsanllc

## Logs

Daily manager logs are available through:

**Console > Open Logs Folder**

For compatibility with existing preview installations, the v0.9.0 preview may continue using the previous local application-data directory internally. This does not affect the Pal Server Hub product name or installer branding.

## Publishing

The project produces:

```text
PalServerHub.exe
```

Run:

```powershell
.\publish-release.ps1
```

Then test:

```text
release\publish\PalServerHub.exe
```

outside Visual Studio.

## Installer

Compile `installer.iss` with Inno Setup after the standalone Release build has been tested.

Expected installer:

```text
release\installer\Pal-Server-Hub-v0.9.0-Setup.exe
```

Installer metadata:

- Product: **Pal Server Hub**
- Publisher: **Mitsan LLC**
- Developer brand: **Stancean**
- Website: **palworldserverhub.com**

## Security

Do not commit:

- Discord webhook URLs
- Palworld administrator passwords
- Live credential-bearing `PalWorldSettings.ini`
- Local preferences
- Logs
- Backups
- `bin`, `obj`, or `release` output

## Release Status

v0.9.0 is a preview build intended for testing before v1.0.0.

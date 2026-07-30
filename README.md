<!-- markdownlint-disable MD033 MD041 -->

[![Crowdin](https://badges.crowdin.net/playnite-extensions/localized.svg)](https://crowdin.com/project/playnite-extensions)
[![GitHub release](https://img.shields.io/github/v/release/Lacro59/playnite-backgroundchanger-plugin?logo=github&color=8A2BE2)](https://github.com/Lacro59/playnite-backgroundchanger-plugin/releases/latest)
[![GitHub Release Date](https://img.shields.io/github/release-date/Lacro59/playnite-backgroundchanger-plugin?logo=github)](https://github.com/Lacro59/playnite-backgroundchanger-plugin/releases/latest)
[![GitHub downloads](https://img.shields.io/github/downloads/Lacro59/playnite-backgroundchanger-plugin/total?logo=github)](https://github.com/Lacro59/playnite-backgroundchanger-plugin/releases)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/Lacro59/playnite-backgroundchanger-plugin/devel?logo=github)](https://github.com/Lacro59/playnite-backgroundchanger-plugin/graphs/commit-activity)
[![GitHub contributors](https://img.shields.io/github/contributors/Lacro59/playnite-backgroundchanger-plugin?logo=github)](https://github.com/Lacro59/playnite-backgroundchanger-plugin/graphs/contributors)
[![GitHub license](https://img.shields.io/github/license/Lacro59/playnite-backgroundchanger-plugin?logo=github)](https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/LICENSE)

# BackgroundChanger for Playnite

Manage cover, background, and icon images with extra formats and rotation options directly inside [Playnite](https://playnite.link).

## ✨ Features

- **Multi-asset manager**: manage backgrounds, covers, and icons per game from the game menu; pin a favorite that stays first after Playnite metadata updates.
- **Image sources**: import from local files or URLs, [SteamGridDB](https://www.steamgriddb.com) (filters and sorting per media kind), Google Image search, or official Steam artwork (search and select by App ID).
- **Animated media via FFmpeg**: import APNG, WebP, GIF, and WebM; they are auto-converted to MP4 on import and at startup for reliable playback (requires FFmpeg / ffprobe).
- **Shuffle & rotation**: when the theme supports it, keep multiple assets and rotate with a no-repeat shuffle queue on game selection and/or on a timer; optional random MP4 start point on first play.
- **Bulk media download**: download backgrounds, covers, and icons for many games at once from Steam official and/or SteamGridDB (pick mode, filters, skip-existing options) from the main menu.
- **Theme integration**: custom controls (`PluginBackgroundImage`, `PluginCoverImage`, `PluginIconImage`) for game details and list views, with stabler transitions and theme sync.

## 📸 Screenshots

### Main interface

<a href="https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/forum/main_01.jpg?raw=true">
  <picture>
    <img alt="BackgroundChanger main window for managing game background and cover images" src="https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/forum/main_01.jpg?raw=true" height="200px">
  </picture>
</a>

### SteamGridDB browser

<a href="https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/forum/steamgriddb_01.jpg?raw=true">
  <picture>
    <img alt="SteamGridDB image browser with search and filters" src="https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/forum/steamgriddb_01.jpg?raw=true" height="200px">
  </picture>
</a>

### Settings panel

<a href="https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/forum/settings_01.jpg?raw=true">
  <picture>
    <img alt="BackgroundChanger plugin settings for backgrounds, covers, and media conversion" src="https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/forum/settings_01.jpg?raw=true" height="200px">
  </picture>
</a>

## ⚙️ Configuration

Open **Settings → Extensions → BackgroundChanger**.

### General behavior

- Enable management for backgrounds, covers, and/or icons.
- Choose exclusive selection modes: random on Playnite start, random on game selection, or timer-based auto-changer (modes do not overlap).
- Optionally delay video playback start and randomize the MP4 start point on first play.

### Media conversion

- Point to FFmpeg and ffprobe binaries (usually in the same folder).
- Tune default CRF and per-format options for animated WebP, APNG, GIF, and WebM (converted to MP4 on import and at startup).
- Note: conversion to MP4 drops alpha/transparency.

### SteamGridDB

- Enter your SteamGridDB API key to browse and download assets (invalid or rate-limited keys surface clear notifications).

### Integration

- Enable or disable theme controls for background, cover, and icon images.

> Animated assets rely on FFmpeg conversion to MP4. Without FFmpeg / ffprobe configured, animated imports will not convert and playback may be limited.

## 📥 Installation

### Install from Playnite Add-ons Browser (recommended)

1. Open Playnite.
2. Go to **Add-ons → Browse → Generic**.
3. Search for `BackgroundChanger` and install it.
4. Restart Playnite if requested.

Official Playnite guide: [Installing Extensions](https://api.playnite.link/docs/manual/features/extensionsSupport/installingExtensions.html)

### Manual installation (`.pext`)

1. Download the latest `.pext` file from [Releases](https://github.com/Lacro59/playnite-backgroundchanger-plugin/releases/latest).
2. In Playnite, open **Add-ons → Install from file**.
3. Select the downloaded `.pext`.
4. Restart Playnite, then optionally set your SteamGridDB API key and FFmpeg paths under **Settings → Extensions → BackgroundChanger**.

## 🤝 Contributing & Feedback

- **Bug reports**: [Open an issue](https://github.com/Lacro59/playnite-backgroundchanger-plugin/issues/new?template=bug_report.md)
- **Feature requests**: [Request an enhancement](https://github.com/Lacro59/playnite-backgroundchanger-plugin/issues/new?template=feature_request.md)
- **Pull requests**: [Submit a PR](https://github.com/Lacro59/playnite-backgroundchanger-plugin/pulls) targeting the `devel` branch
- **Translations**: [Contribute on Crowdin](https://crowdin.com/project/playnite-extensions)
- **Wiki & troubleshooting**: [Project wiki](https://github.com/Lacro59/playnite-backgroundchanger-plugin/wiki) (including [custom theme integration](https://github.com/Lacro59/playnite-backgroundchanger-plugin/wiki/Addition-in-a-custom-theme))

## 💝 Support

[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/lacro59)

If this plugin helps you, you can also support:

- [Playnite](https://www.patreon.com/playnite)
- [SteamGridDB](https://www.patreon.com/steamgriddb)

## 📄 License

This project is licensed under the [MIT License](https://github.com/Lacro59/playnite-backgroundchanger-plugin/blob/master/LICENSE).

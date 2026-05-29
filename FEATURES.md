# NovaSetup Feature Inventory

Last updated: 2026-05-27

This file lists features that are currently present in the NovaSetup codebase. It is an inventory of implemented app behavior, not a roadmap.

## Catalog

- Local app catalog stored in `NovaSetup/Configs/apps.json`.
- 66 catalog apps currently defined.
- Dynamic catalog categories:
  - Accessories
  - Browsers
  - Coding
  - Communication
  - Creative
  - Dependencies
  - Development Tools
  - Drivers
  - Gaming
  - Media
  - Office / Productivity
  - Portable Tools
  - Utilities
- App metadata includes name, category, publisher, homepage, description, license, version, release notes URL, tags, dependencies, supported platforms, and install definitions.
- App logo URL support through `logoUrl`.
- App logos render in list and grid views with a fallback monogram when no logo is available or the remote logo cannot load.
- Runtime logo fetch throttling and in-memory logo path caching to reduce startup/network spikes.
- Catalog cache handling prefers newer or larger bundled catalog data over stale local cache.

## App Browsing UI

- Main dashboard, apps, drivers, my lists, updates, downloads/history, logs, and about sections.
- App list view and app grid/tile view.
- Toggle between list and grid view.
- Search filter across app name, category, publisher, description, versions, status, and tags.
- Filter chips for all apps, games, drivers, recommended, dev tools, utilities, ARM64, and updates.
- Dynamic category dropdown populated from catalog categories.
- Show or hide already installed apps through settings.
- Unsupported apps are visually dimmed and marked as not available on the current platform.
- Rich app detail panel with:
  - version info
  - installed version
  - update status
  - license
  - tags
  - release notes link
  - VirusTotal link when available
  - install method summary
  - primary action button
- Right-click context menu on app rows with install, update, uninstall, open install location, open homepage, copy app name, and per-app preference toggles.

## Presets And Profiles

- Built-in quick setup presets through `PresetService`.
- Preset cards in the apps page only.
- Presets select matching supported apps safely and skip unavailable apps.
- Preset-applied confirmation banner with auto-dismiss.
- Selection profiles through `NovaProfile` and `ProfileService`.
- Save, load, export, delete, and auto-save profile support.
- Existing selected apps can be restored from saved selection config.

## Installation

- Windows installer support through winget, direct installer URLs, MSI/EXE execution, and configured commands.
- Linux install command support through catalog `LinuxInstall`.
- macOS install command support through catalog `MacOSInstall`.
- Three-way platform install routing for Windows, Linux, and macOS.
- Silent install support with global setting.
- Per-app disable silent install preference.
- Interactive install fallback when silent install is disabled for a specific app.
- Install dependency resolution and install ordering.
- Missing dependency banner before install.
- Live install queue panel showing pending, downloading, installing, done, failed, skipped, and cancelled states.
- Per-app live queue progress.
- Per-app cancel support.
- Cancel all active installs.
- Download progress text showing downloaded size and total size when available.
- Download progress percentage for progress bars.
- Custom download folder setting.
- Keep installers after install setting.
- Restart behavior setting:
  - ask before restart
  - restart automatically
  - never restart automatically
- Restart-required detection through installer metadata and common restart exit codes.
- Safe placeholder handling for self-delete after install.

## Portable Apps

- Portable app catalog support with `IsPortable`.
- Portable archive metadata:
  - archive URL
  - executable name
  - archive type
  - subfolder
- Portable ZIP extraction to the configured portable apps folder.
- Portable app detection by executable lookup instead of registry lookup.
- Portable app version detection from executable file metadata.
- Configurable default portable apps folder.
- 7z portable archives are explicitly detected as unsupported for extraction for now.

## Updates

- Installed app update detection.
- App update list in the Updates section.
- Update all and single-app update flows.
- Per-app disable update scanning preference.
- Per-app disable auto-update preference.
- Scheduled update service with daily, weekly, and monthly frequency settings.
- Run scheduled updates now command.
- Windows Task Scheduler integration for scheduled updates.
- Headless scheduled update mode.
- Missed scheduled update handling.
- Batch winget upgrade lookup for update scanning.

## Detection

- Windows installed app detection through registry uninstall entries, App Paths, PATH, known install paths, and portable folder detection.
- macOS detection through `/Applications`, brew, and executable lookup.
- Linux command-based executable detection.
- Hardware detection for GPU, motherboard, and accessories.
- Hardware-based app recommendations.
- Startup installed-app detection.
- Best-effort installed-app detection cache under LocalApplicationData.
- Detection cache invalidation after successful install, update, uninstall, and manual refresh.
- Batch `winget list` lookup for installed package versions to avoid one winget process per app during detection.
- Platform detection for Windows, Linux, and macOS.
- Architecture detection for x86, x64, and ARM64.
- Cached package manager detection for the current app session.

## Cross-Platform Support

- Platform service exposes current platform and package manager.
- Package manager detection:
  - Windows: winget, choco, direct
  - Linux: apt, snap, flatpak, direct
  - macOS: brew, direct
- Platform support metadata includes Windows, Linux, and macOS.
- macOS catalog install definitions.
- ARM64 installer URL and silent argument metadata.
- ARM64 filter chip visible only on ARM64 machines.
- ARM64 app badge visible only on ARM64 machines for apps with native ARM64 support.

## Security And Trust

- SHA256 hash verification before running downloaded installers.
- Architecture-specific hash metadata support.
- VirusTotal URL, ratio, and scan date metadata in install definitions.
- VirusTotal ratio display in app rows.
- VirusTotal clean/warning/no-data logging before downloaded installer execution.
- Pre-install and post-install PowerShell script support.
- Global setting to allow or block install scripts.
- Per-app trust toggle for install scripts.
- Script failures log warnings but do not change install results.
- Allowed command tracking to prevent duplicate active installs.

## Per-App Preferences

- Per-user app preferences saved under LocalApplicationData.
- Per-app preferences include:
  - disable silent install
  - disable update scanning
  - disable auto-update
  - allow install scripts
- Preferences load at startup and apply to catalog apps.
- Preferences save immediately when changed through the UI.
- Preferences popup in app rows.

## History, Logs, And Status

- Install history service and history/downloads page.
- Latest install summary, queue, and activity shown in Downloads.
- Clear history command.
- Live developer logs view.
- Log level filtering.
- Copy logs, copy selected log, export logs, clear logs, and open log file commands.
- Status banners for update availability, dependency info, preset applied, restart required, and about update status.
- Splash screen startup status updates.

## Settings

- Theme setting: dark, light, system.
- Language setting model support.
- Global silent install setting.
- OS-supported apps filter setting.
- Parallel install setting.
- Show already installed apps setting.
- Scan PC on startup setting.
- Download location mode setting.
- Portable apps folder setting with folder picker.
- Keep installer files setting.
- Launch Nova at startup setting.
- Check for Nova updates automatically setting.
- Scheduled update settings.
- Auto-save profiles setting.
- Developer mode setting.
- Reset settings command.

## Nova App Updates

- Nova version check through `UpdateCheckerService`.
- About page update status.
- Update available banner.
- Download/update link opening through browser service.

## Current Known Notes

- Portable 7z extraction is not implemented yet.
- Some catalog apps intentionally use `logoUrl: null` when no reliable Simple Icons slug is configured.
- VirusTotal data is static catalog metadata and must be updated manually when installer versions change.

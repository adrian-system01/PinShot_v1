# PinShot

PinShot captures clipboard images into floating windows and stores notes locally.

## Data and backups

- Notes and settings are stored under `%LOCALAPPDATA%\PinShot`.
- Use **Settings > Export backup** to create a portable ZIP of notes and images.
- Import merges notes and assigns new IDs; it does not overwrite existing notes.
- Notes can be searched by text or tag, tagged in the editor, and pinned from the library context menu.
- The editor shows save status and supports `Ctrl+S`; the library supports `Ctrl+F`, `Ctrl+N`, and `Esc` to clear search.
- Velopack checks public GitHub Releases at startup and every six hours. It downloads updates silently and installs/restarts only after all app windows are closed.
- Automatic updates require installation with the Velopack setup. Existing ZIP/manual copies must be migrated by installing the first Velopack setup; they are not updated in place.
- During that one-time migration, turn off startup in the old copy, exit it, and then install the Velopack setup. Your notes stay under `%LOCALAPPDATA%\PinShot`.
- GitHub Releases are public. The first update check starts working after the first tagged release is published.
- Before each development update, run `scripts\New-UpdateBackup.ps1` with a semantic version bump:

```powershell
.\PinShot\scripts\New-UpdateBackup.ps1 -Bump minor
```

Use `-Bump patch` for fixes and `-Bump major` for breaking changes. The script creates the next numbered source backup in the sibling `PinShot_Respaldos` folder, excludes build output, then updates the project version.

Push a matching version tag (for example `v3.4.0`) to run `.github/workflows/release.yml`. It tests, publishes a self-contained Windows build, creates the Velopack installer/update feed, and publishes it as a GitHub Release. The workflow and app source must first be present in the GitHub repository.

## Main components

- `App.xaml.cs`: application lifecycle and clipboard capture coordination.
- `TrayIconController.cs`: notification-area menu and icon.
- `WindowsStartupService.cs`: Windows startup registration.
- `NoteStore.cs`: note persistence, recovery, and note archive import/export.
- `LibraryWindow` and `NoteEditorWindow`: note browsing and editing.
- `PinShot.Tests`: isolated storage tests.

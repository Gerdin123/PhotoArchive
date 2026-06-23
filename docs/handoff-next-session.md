# Handoff For Next Session

Last verified: 2026-06-23.

## Current Status

PhotoArchive has been rebuilt around the `AGENTS.md` direction rather than the obsolete API/Angular/import/cleaner implementation. Milestones 1 through 5 are implemented in the new solution layout:

- `src/PhotoArchive.Core`: domain models and deterministic preprocessing rules.
- `src/PhotoArchive.Infrastructure`: filesystem adapters, ExifTool fallback reader, manifest/log writing, EF Core SQLite persistence, and XMP sidecar write-back.
- `src/PhotoArchive.Cli`: preprocessing, database import, and metadata write-back commands.
- `src/PhotoArchive.App`: Avalonia desktop review UI.
- `tests/PhotoArchive.Core.Tests` and `tests/PhotoArchive.IntegrationTests`: core and integration coverage.

The old implementation directories are deleted in the working tree. Do not restore them unless explicitly asked.

## Key Decisions

- Decade folders are strict calendar decades only: `1990-1999`, `2000-2009`, `2010-2019`.
- Data access is EF Core with SQLite. Schema changes should use migrations.
- Metadata write-back is sidecar-first in v1. The current writer creates `.xmp` files and does not modify originals.
- Preprocessing is deterministic and traceable through JSON manifests and operation logs.
- Duplicate detection is exact SHA-256 only. Similarity is review aid behavior, not deletion automation.
- The Avalonia UI has three views:
  - directory setup and preprocessing,
  - paged directory home,
  - image metadata edit/review.
- The Avalonia views are implemented as separate components: `SetupPage`, `HomePage`, and `EditPage`; `MainWindow` should remain a navigation shell.

## Verification Commands

Run all tests:

```powershell
dotnet test PhotoArchive.slnx
```

Run a build:

```powershell
dotnet build PhotoArchive.slnx
```

Current verified result:

- `dotnet build PhotoArchive.slnx`
- `dotnet test PhotoArchive.slnx`
- 114 passing tests total.
- `PhotoArchive.Core.Tests`: 45 passed.
- `PhotoArchive.IntegrationTests`: 69 passed.

## Recent Test Expansion

The test suite now covers positive, negative, and edge cases for:

- EXIF/XMP/date filename/filesystem date inference priority.
- Implausible date rejection and boundary years.
- Strict calendar decade buckets.
- Supported image naming and same-day sequencing.
- Unknown date routing.
- Duplicate routing and canonical selection rules.
- Planned collision resolution.
- Recursive scanning order.
- Image signature classification and unsupported extensions.
- Output path validation, existing destination validation, and archive executor non-overwrite behavior.
- Hash mismatch handling after copy.
- Dry-run manifest contents and enum names in JSON.
- EF Core import/re-import behavior.
- Metadata write-back sidecar creation, missing date rejection, missing target skips, corrected-only mode, and write-all mode.
- Directory setup preprocessing, reuse of existing database, missing input rejection, and output-inside-input rejection.
- Directory setup defaults derive `<selected folder>cleaned` beside the selected input folder and place `photoarchive.db` inside that cleaned folder. The setup UI uses native folder/file pickers.
- Directory setup remembers the last selected input, output, and database paths in local app data (`PhotoArchive\settings.json`) and restores them when the Directory page opens.
- Directory setup has an explicit force-clean option that deletes the selected SQLite database files and PhotoArchive-managed output folders (`Photos`, `Duplicates`, `Unsupported`, `Manifests`) before rerunning preprocessing. It rejects force-clean when output or database paths are inside the original input folder.
- Force-clean also rejects any original/cleaned folder overlap in either direction, including the dangerous case where the cleaned output folder is a parent of the original input folder.
- Application logging writes daily text logs under local app data (`PhotoArchive\Logs`) by default, and the setup view displays the log folder path.
- Directory preprocessing reports UI progress with phase, percentage, files found, processed count, elapsed time, and ETA.
- Preprocessing skips thumbnail/system artifacts such as `Thumbs.db`, `.thm`/`.THM`, `THM_*.jpg`, `thumb.jpg`, and `thumbnail-*` files before planning/importing, so they do not appear in the review UI.
- Avalonia page controls are no longer reused across navigation; each page component owns its own controls to avoid visual-parent conflicts.
- Home page filters are preserved while navigating away and back. Year and decade inputs show prefix-based suggestions from dates present in the archive. Tag filtering uses an unselected-tag dropdown plus active removable tag chips, with AND semantics across selected tags.
- Home renders photos as a manually populated wrapping card grid with low-resolution image previews, filename, date, and tags. Edit review lists also render low-resolution image previews as the primary visual.
- Preview decoding is best-effort and falls back to a placeholder for corrupt or unsupported image data instead of crashing.
- Home defaults to supported, non-deleted, non-duplicate image cards. Unsupported/unknown files, deleted/hidden rows, and duplicates are opt-in through explicit Home controls.
- Home page totals no longer have the previous 5000-row cap. The page label distinguishes currently visible filtered photos from archive summary counts for supported, duplicate, unsupported, and deleted rows.
- Duplicate files are excluded from Home by default. Home has an `Include duplicates` checkbox and a duplicate-only filter; Edit no longer shows duplicate group entries by default.
- Review repository paging, filters, sorting, details, related photo reasons, tag add/remove idempotency, date correction with missing metadata, duplicate marking failures, and hide behavior.
- Manual date correction now resequences affected supported-image archive paths for the old and new days, physically renames already-copied output files when present, detects destination collisions, and records resequence operation logs.
- Directory setup generates thumbnail cache entries behind `IThumbnailService`, persists thumbnail paths/status, and force-clean removes PhotoArchive-managed `Thumbnails` output.
- Directory setup reports thumbnail generation as its own progress phase with a dedicated progress bar, progress text, elapsed time, and ETA.
- Thumbnail generation now uses SkiaSharp instead of the disabled Avalonia bitmap stub. Valid supported images produce bounded JPEG thumbnails, corrupt images fail without blocking preprocessing, and opening an existing archive regenerates missing/failed thumbnails.
- The Avalonia shell, setup page, Home page, Edit page, and photo cards now have lightweight styling. Home filters wrap dynamically, and Edit switches between side-by-side and stacked layouts based on available width.
- Review UI colors now use a shared `UiTheme` helper for common backgrounds, borders, text, and selected states instead of duplicating brush literals across pages.
- Related-photo scoring uses persisted average color and perceptual hash metadata. Thumbnail generation now extracts and persists average color and a stable 64-bit grayscale perceptual hash for valid supported images.
- A lightweight Avalonia smoke test constructs and initializes the Directory, Home, and Edit page flow.
- A representative golden manifest test compares full normalized preprocessing JSON.

The manifest writer was also updated to serialize enum values as names instead of numbers, which makes the permanent manifest more readable and audit-friendly.

## Useful Run Commands

Dry-run preprocessing:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --input "E:\Photos\Input" --output "E:\Photos\Output"
```

Execute preprocessing and copy files:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --input "E:\Photos\Input" --output "E:\Photos\Output" --execute
```

Persist preprocessing into SQLite:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --input "E:\Photos\Input" --output "E:\Photos\Output" --db "E:\Photos\photoarchive.db"
```

Run metadata sidecar write-back:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --write-metadata --db "E:\Photos\photoarchive.db"
```

Run the Avalonia app:

```powershell
dotnet run --project src\PhotoArchive.App\PhotoArchive.App.csproj -- --db "E:\Photos\photoarchive.db"
```

## Known Gaps

- Metadata write-back only writes XMP sidecars. Embedded EXIF/XMP writes should remain a future explicit option with backup/copy safety.
- UI smoke coverage exists, but no full GUI automation, responsive-layout assertions, or screenshot verification has been added for the Avalonia UI.

## Suggested Next Work

1. Add GUI automation or screenshot verification for the Avalonia three-view flow, including the setup progress stage, thumbnail progress row, Home wrapping filters/cards, and Edit responsive layout.
2. Add embedded metadata write-back as a future explicit option with backup/copy safety, keeping XMP sidecars as the default.
3. Consider pushing more Home filtering/sorting into SQLite if very large archives make the current deterministic in-memory date sorting too slow.

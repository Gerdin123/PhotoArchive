# Handoff For Next Session

Last verified: 2026-06-18.

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

- `dotnet test PhotoArchive.slnx`
- 75 passing tests total.
- `PhotoArchive.Core.Tests`: 35 passed.
- `PhotoArchive.IntegrationTests`: 40 passed.

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
- Review repository paging, filters, sorting, details, related photo reasons, tag add/remove idempotency, date correction with missing metadata, duplicate marking failures, and hide behavior.

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

- No generated thumbnails yet. UI preview currently points at image paths but does not include a full thumbnail cache pipeline.
- Average color and perceptual hash are not implemented yet. Related-photo heuristics currently use same day, source folder, camera model, dimensions, shared tags, and exact hash.
- Date correction records database changes but does not yet resequence affected archive filenames.
- Metadata write-back only writes XMP sidecars. Embedded EXIF/XMP writes should remain a future explicit option with backup/copy safety.
- UI folder selection uses text inputs instead of native folder picker dialogs.
- No GUI automation or screenshot verification has been added for the Avalonia UI.
- Golden/snapshot tests for full output plans are still recommended by `AGENTS.md`; current tests assert important paths and manifest fields directly.

## Suggested Next Work

1. Add thumbnail generation behind `IThumbnailService` and persist thumbnail paths/state.
2. Add average color or perceptual hash fields and use them in related-photo scoring.
3. Implement resequencing when manual date corrections affect output filenames.
4. Add native folder/database picker dialogs to the setup view.
5. Add UI-level tests or smoke tests for the three-view Avalonia flow.
6. Add snapshot tests for representative preprocessing manifests.

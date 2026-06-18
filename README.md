# PhotoArchive

PhotoArchive is a local-first photo archive tool. It is being rebuilt from a clean scaffold to match the product and safety requirements in [AGENTS.md](AGENTS.md).

The previous implementation has been removed because it no longer represented the intended architecture. The repository now contains v1 Milestones 1 through 5: preprocessing CLI, SQLite persistence, Avalonia review UI, search/filter plus related-photo review aids, and explicit metadata write-back.

## Current State

Milestones 1 through 5 are implemented. The CLI can scan an input folder, build a deterministic dry-run plan, optionally copy files into an organized output archive, persist preprocessing results into SQLite, and explicitly write corrected metadata to XMP sidecars. The desktop app can browse, filter, and review the local database.

Implemented:

- Solution structure aligned with `AGENTS.md`.
- Core domain entities for files, metadata, duplicates, tags, corrections, and operation logs.
- Preprocessing service interfaces for scanning, classification, hashing, metadata reading/writing, date inference, output planning, execution, thumbnails, and tags.
- Recursive file scanner.
- File classifier using supported extensions and common image signatures.
- SHA-256 hashing for exact duplicate detection.
- ExifTool metadata reader with filesystem timestamp fallback.
- Deterministic date inference service.
- Strict calendar decade bucket policy, such as `1990-1999` and `2010-2019`.
- Output planner for `Photos`, `Duplicates`, `Unsupported`, and `Manifests`.
- Dry-run JSON manifest and operation log generation.
- Copy-then-verify archive execution behind `--execute`.
- EF Core + SQLite schema and initial migration.
- Database import of archive files, metadata rows, duplicate groups, and operation logs.
- Repeat imports update file/metadata state while preserving operation history.
- Avalonia review UI with three views: directory setup/preprocess, paged directory home, and image metadata editing.
- Directory setup opens an existing SQLite archive or preprocesses the selected original folder into the selected cleaned folder when the database is empty.
- Home view shows a paged subset of images with sorting and filters for text, year, decade, tags, duplicate state, uncertain/unprocessed files, date range, and review status.
- Edit view shows photo detail/metadata display, nearby photos, related photos, duplicate group view, date correction, tag editing, duplicate marking, and hide action.
- Search/filter by text, year, decade, tag, duplicate state, date range, and review status.
- Related-photo scoring using same day, same source folder, camera model, dimensions, shared tags, and exact hash.
- Explicit metadata write-back command that writes XMP sidecars from corrected database dates and records operation logs.
- Expanded xUnit coverage for date inference, decade buckets, output planning, manifest JSON, filesystem scanning/classification, archive execution safety, SQLite persistence, metadata write-back, directory setup, paging, filters, related-photo heuristics, and review edit workflows.

## Solution Layout

```text
PhotoArchive/
  src/
    PhotoArchive.App/              # Future Avalonia desktop UI shell
    PhotoArchive.Core/             # Domain models and business rules
    PhotoArchive.Infrastructure/   # File system, ExifTool, SQLite, image library adapters
    PhotoArchive.Cli/              # Future preprocessing CLI
  tests/
    PhotoArchive.Core.Tests/
    PhotoArchive.IntegrationTests/
  docs/
    architecture.md
    metadata-policy.md
```

## Safety Principles

The implementation must preserve these rules:

- Never destroy originals by default.
- Generate and validate a dry-run plan before file changes.
- Use SHA-256 content hashes for exact duplicate detection.
- Treat EXIF/XMP dates as evidence, not absolute truth.
- Keep metadata extraction, date inference, duplicate detection, and file operations behind separate services.
- Do not silently overwrite files.
- Write manifests and operation logs for traceability.
- Store manual corrections in the database first; write metadata back only through an explicit action.

## Requirements

- .NET 10 SDK
- ExifTool on `PATH` for EXIF/XMP metadata dates. If it is unavailable, the CLI falls back to filename and filesystem timestamp evidence.
- SQLite for the local database
- Avalonia templates/packages for the future desktop UI milestone

## Build

```powershell
dotnet build PhotoArchive.slnx
```

## Test

```powershell
dotnet test PhotoArchive.slnx
```

Current suite status: 75 passing tests across core and integration projects.

## CLI

Dry-run an input folder:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --input "E:\Photos\Input" --output "E:\Photos\Output"
```

Execute the approved plan:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --input "E:\Photos\Input" --output "E:\Photos\Output" --execute
```

Persist results to SQLite:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --input "E:\Photos\Input" --output "E:\Photos\Output" --db "E:\Photos\photoarchive.db"
```

Without `--execute`, the CLI writes only a manifest and operation log. With `--execute`, it writes the manifest before copying, copies each file, verifies the copied SHA-256 hash, then rewrites the manifest and operation log with execution results. With `--db`, the CLI applies EF Core migrations and imports archive files, metadata, duplicate groups, and operation logs.

Write corrected metadata to XMP sidecars:

```powershell
dotnet run --project src\PhotoArchive.Cli\PhotoArchive.Cli.csproj -- --write-metadata --db "E:\Photos\photoarchive.db"
```

By default this writes only photos with manual date corrections. Use `--write-all-metadata` to write sidecars for every supported image with an inferred date.

## Target Output

The target archive layout is:

```text
Output/
  Photos/
    2010-2019/
      2010/
        20100501 - 1.jpg
  Duplicates/
    2010-2019/
      2010/
        original-name.jpg
  Unsupported/
    1990-1999/
      1999/
        notes.txt
  Manifests/
    preprocessing-YYYYMMDD-HHMMSS.json
    operations-YYYYMMDD-HHMMSS.log
```

Supported images will be renamed as:

```text
yyyyMMdd - n.ext
```

Unsupported files and duplicates must keep their original filenames by default.

## Database

The database layer lives in `PhotoArchive.Infrastructure.Persistence` and uses EF Core with SQLite. Schema changes are represented with EF Core migrations under `src/PhotoArchive.Infrastructure/Persistence/Migrations`.

Stored entities include:

- `ArchiveFile`
- `PhotoMetadata`
- `DuplicateGroup`
- `Tag`
- `PhotoTag`
- `ManualCorrection`
- `OperationLog`

## Review UI

Run the Avalonia desktop app:

```powershell
dotnet run --project src\PhotoArchive.App\PhotoArchive.App.csproj -- --db "E:\Photos\photoarchive.db"
```

The review UI supports:

- Choose original and cleaned folders, then preprocess automatically if the local database has not been populated.
- Browse a paged home view for the selected directory.
- Browse by search text, year, decade, tag, duplicate state, and review status.
- Sort by date, file name, status, and date confidence.
- Filter for unprocessed or uncertain-date images.
- View photo preview, paths, inferred date, date confidence, dimensions, camera fields, and hash.
- View nearby photos by date, related photos by heuristic score, and exact duplicate group members.
- Correct taken date, which writes `ManualCorrection` and `OperationLog` rows.
- Add and remove tags.
- Mark one file as a duplicate of another.
- Hide a file through a reversible database status change.

## Metadata Write-Back

Metadata write-back is explicit and sidecar-first in v1. The command writes standard XMP date fields next to supported image files:

- `exif:DateTimeOriginal`
- `xmp:CreateDate`
- `xmp:MetadataDate`

Original image files are not modified by the v1 writer. Each attempted write produces an `OperationLog` row.

See [AGENTS.md](AGENTS.md) for the complete product rules, roadmap, and non-goals.

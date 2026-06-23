# AGENTS.md — PhotoArchive

## Project goal
PhotoArchive is a local-first photo archive tool. It takes an unsorted, unorganized input folder and produces a clean, reviewable output archive. The first phase is deterministic preprocessing: detect exact duplicates, read metadata, infer capture dates, rename supported images, and organize files into a decade/year/day-friendly structure. The second phase is a manual review and correction UI for dates, duplicates, metadata, tags, and search/filtering.

The app must prioritize data safety, traceability, and repeatability over speed or clever automation.

## Core principles
1. Never destroy originals by default.
2. Every file operation must be reversible or traceable through a manifest/audit log.
3. Preprocessing must be deterministic: the same input and settings should produce the same plan.
4. Metadata extraction, date inference, duplicate detection, and file moving/renaming must be separate services.
5. The app must support a dry-run mode before changing files.
6. Manual corrections must be stored in the app database and optionally written back to EXIF/XMP only when explicitly requested.
7. Treat EXIF dates as evidence, not absolute truth. Some archives have damaged, missing, or overwritten metadata.
8. Use content hashes for exact duplicate detection, not filenames, file size alone, or timestamps alone.
9. Unsupported files should be preserved and organized, but not renamed.
10. Do not overwrite files silently. Resolve collisions explicitly and record the decision.

## Recommended stack
- Backend/runtime: C# on .NET 10 LTS.
- UI: Avalonia UI for a cross-platform desktop app.
- Database: SQLite for local metadata, indexes, tags, corrections, and processing state.
- Data access: EF Core with SQLite. Use migrations for schema changes and keep entity mappings explicit and reviewable.
- Metadata: ExifTool as an external process for robust EXIF/IPTC/XMP read/write support. Wrap it behind an `IMetadataReader` / `IMetadataWriter` abstraction.
- Image decoding/thumbnails: ImageSharp, Magick.NET, or SkiaSharp. Keep this behind an abstraction.
- Background jobs: .NET `BackgroundService`, channels, or TPL Dataflow for scanning/hash/thumbnail pipelines.
- Logging: Serilog structured logs.
- Tests: xUnit + Verify/snapshot tests for generated manifests and output paths.

## Suggested solution structure
```text
PhotoArchive/
  src/
    PhotoArchive.App/              # Avalonia desktop UI
    PhotoArchive.Core/             # Domain models and business rules
    PhotoArchive.Infrastructure/   # File system, ExifTool, SQLite, image libraries
    PhotoArchive.Cli/              # Optional CLI for preprocessing and tests
  tests/
    PhotoArchive.Core.Tests/
    PhotoArchive.IntegrationTests/
  docs/
    architecture.md
    metadata-policy.md
```

## Domain model
Recommended core entities:

- `ArchiveFile`
  - `Id`
  - `OriginalPath`
  - `CurrentPath`
  - `OriginalFileName`
  - `Extension`
  - `FileSizeBytes`
  - `Sha256Hash`
  - `MediaKind` = `SupportedImage`, `Unsupported`, `Duplicate`, `Unknown`
  - `Status` = `Scanned`, `Planned`, `Processed`, `NeedsReview`, `Deleted`, `Duplicate`
  - `CreatedAtUtc`, `UpdatedAtUtc`

- `PhotoMetadata`
  - `ArchiveFileId`
  - `ExifDateTimeOriginal`
  - `ExifCreateDate`
  - `FileCreatedDate`
  - `FileModifiedDate`
  - `InferredTakenDate`
  - `DateConfidence` = `High`, `Medium`, `Low`, `Unknown`
  - `CameraMake`, `CameraModel`
  - `Width`, `Height`
  - `GpsLatitude`, `GpsLongitude`

- `DuplicateGroup`
  - `Id`
  - `Hash`
  - `CanonicalFileId`
  - `CreatedAtUtc`

- `Tag`
  - `Id`
  - `Name`
  - `Type` = `Person`, `Place`, `Event`, `Custom`

- `PhotoTag`
  - `ArchiveFileId`
  - `TagId`

- `ManualCorrection`
  - `ArchiveFileId`
  - `FieldName`
  - `OldValue`
  - `NewValue`
  - `Reason`
  - `CreatedAtUtc`

- `OperationLog`
  - `Id`
  - `OperationType`
  - `SourcePath`
  - `DestinationPath`
  - `Result`
  - `ErrorMessage`
  - `CreatedAtUtc`

## Preprocessing pipeline
The preprocessing phase should run in these steps:

1. Scan input folder recursively.
2. Classify files by extension and detected MIME/signature.
3. Compute SHA-256 hash for each file.
4. Group exact duplicates by hash.
5. Pick one canonical file per duplicate group.
6. Extract metadata for supported images.
7. Infer capture date using a clear priority order.
8. Generate an output plan without moving files yet.
9. Validate the plan for collisions and unsupported edge cases.
10. Execute the plan only after dry-run/preview approval.
11. Write a manifest and operation log.

## Date inference policy
Use this order when deciding the photo date:

1. `EXIF:DateTimeOriginal`
2. `EXIF:CreateDate`
3. `XMP:DateCreated`
4. Filename date patterns, if unambiguous
5. File system created/modified date
6. Unknown date bucket

Date confidence:

- `High`: EXIF/XMP capture date exists and is plausible.
- `Medium`: date was inferred from filename or nearby grouped photos.
- `Low`: date came only from file system timestamps.
- `Unknown`: no reliable date could be inferred.

Never silently treat CD burn/import timestamps as capture dates with high confidence.

## Output folder rules
Root output structure:

```text
Output/
  Photos/
    2000-2009/
      2007/
        20070414 - 1.jpg
        20070414 - 2.jpg
  Duplicates/
    2000-2009/
      2007/
        <original filename>
  Unsupported/
    2000-2009/
      2007/
        <original filename>
    UnknownDate/
      <original filename>
  Manifests/
    preprocessing-YYYYMMDD-HHMMSS.json
    operations-YYYYMMDD-HHMMSS.log
```

Decade folders must use strict calendar decades only. Examples: `1990-1999`, `2000-2009`, `2010-2019`, `2020-2029`. Do not use inclusive 11-year buckets such as `2000-2010`, `2011-2020`, or `2021-2030`.

Supported images are renamed as:

```text
yyyyMMdd - n.ext
```

Where `n` is the chronological order for that day. The earliest photo that day gets `1`. If timestamps are missing but dates match, use stable ordering by original path and hash.

Unsupported files are organized by inferred date when possible, but must keep their original filenames.

Duplicates should not be renamed by default. Store them under `Duplicates` and keep a manifest link to their canonical file.

## Manual review UI requirements
The review UI should support:

- A directory setup view for choosing the original unprocessed folder, cleaned output folder, and local SQLite database. If the selected archive database is empty, the app should run deterministic preprocessing before opening review.
- A directory home view with a paged subset of images for the selected archive.
- Browse photos by date, decade, year, tag, duplicate group, and review status.
- Sort and filter from the home view by date, tags, review status, duplicate state, unprocessed state, and uncertain dates.
- Show metadata and date confidence clearly.
- Show nearby photos by date/time.
- Show visually similar/related photos using one or more heuristics:
  - same day or nearby dates
  - same folder/source batch
  - average color / perceptual hash
  - camera model
  - dimensions
- Mark a file as duplicate of another file.
- Delete or hide a file through a reversible app action first.
- Correct taken date/time.
- Rename/resequence all photos affected by a date correction.
- Add, edit, merge, and remove tags.
- Filter by date ranges, people, places, events, and review status.

## Metadata editing rules
Manual metadata edits should first update the database. Writing changes back to files should be a separate explicit action.

When writing metadata:

- Prefer writing standard EXIF/XMP fields.
- Preserve original metadata where possible.
- Keep an operation log of every metadata write.
- Prefer XMP sidecars by default in v1 so originals are not modified. Embedded writes must be an explicit future option with backup/copy safety.
- Always test metadata writes on copied files, never originals.

## Duplicate detection
Phase 1 duplicate detection is exact only:

- Use SHA-256 hash.
- Files with the same hash are exact duplicates.
- Select canonical file using deterministic rules:
  1. supported image over unsupported when applicable
  2. better metadata completeness
  3. longer original path/folder context if useful
  4. stable lexical path ordering as final tie-breaker

Phase 2 may add perceptual similarity, but it must not auto-delete files. Similarity is only a review aid.

## Safety and transaction rules
Before moving/copying files:

- Ensure output path is not inside input path unless explicitly allowed.
- Ensure there is enough disk space.
- Detect filename collisions before writing.
- Write a manifest first.
- Prefer copy-then-verify-hash-then-optionally-delete-source over direct move.
- Never overwrite existing files unless the manifest says it is safe and user approved it.

## Testing requirements
Always run the affected tests after making changes. If affected tests fail, fix the issues before handing off. If a change exposes missing coverage, add tests for the expected behavior plus negative and edge cases before considering the task complete.

Add tests for:

- SHA-256 duplicate grouping.
- Date inference priority.
- Decade/year folder generation.
- Same-day sequence numbering.
- Collision handling.
- Unsupported file handling.
- Dry-run manifest generation.
- Re-running preprocessing on the same input.
- Manual correction causing resequencing.
- Metadata read/write wrapper behavior using fixture copies.

Use golden/snapshot tests for output plans.

## Non-goals for the first version
Do not build these in v1 unless the core flow is stable:

- Cloud sync.
- Face recognition.
- AI auto-tagging.
- Automatic deletion of visually similar images.
- Multi-user collaboration.
- Mobile app.

## Suggested milestones
### Milestone 1 — CLI preprocessing
- Recursive scan.
- Hashing.
- Metadata read.
- Date inference.
- Dry-run output plan.
- Copy/organize execution.
- Manifest and logs.

### Milestone 2 — Local database
- SQLite schema.
- Import scan results.
- Store metadata, hashes, paths, duplicate groups, and operation logs.

### Milestone 3 — Review UI
- Avalonia gallery.
- Photo detail view.
- Date correction.
- Tag editing.
- Duplicate marking.

### Milestone 4 — Search/filter
- Date range filtering.
- Tag filtering.
- Review status filtering.
- Similar/nearby image view.

### Milestone 5 — Metadata write-back
- Explicit write-back command.
- Backup/copy safety.
- EXIF/XMP update tests.

## Coding guidance for agents
When implementing code in this repository:

1. Keep domain logic in `PhotoArchive.Core`.
2. Do not put file system calls directly in UI code.
3. Do not put ExifTool calls directly in domain code.
4. Add tests for every rule that changes filenames, dates, duplicate handling, or file movement.
5. Prefer small services with explicit interfaces:
   - `IFileScanner`
   - `IFileClassifier`
   - `IHashService`
   - `IMetadataReader`
   - `IMetadataWriter`
   - `IDateInferenceService`
   - `IOutputPlanner`
   - `IArchiveExecutor`
   - `IThumbnailService`
   - `ITagRepository`
6. Any operation that changes files must produce an `OperationLog` entry.
7. Any operation that changes date metadata must create a `ManualCorrection` entry.
8. Do not add AI/ML features until deterministic preprocessing and manual review are reliable.
9. Prefer clarity over cleverness.
10. If uncertain, preserve the file and mark it `NeedsReview`.

## Example output naming
For three photos taken on 2007-04-14:

```text
20070414 - 1.jpg
20070414 - 2.jpg
20070414 - 3.jpg
```

Ordering should be by exact capture timestamp when available. If only date exists, use stable fallback ordering.

## Manifest requirements
Every preprocessing run should output a JSON manifest containing:

- app version
- run timestamp
- input root
- output root
- settings used
- every source file
- hash
- detected media kind
- inferred date and confidence
- planned destination
- duplicate group id if applicable
- execution result
- error, if any

This manifest is part of the archive and should be stored permanently.

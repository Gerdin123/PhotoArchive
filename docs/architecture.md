# Architecture

PhotoArchive is organized around deterministic preprocessing followed by manual review.

## Layers

- `PhotoArchive.Core`: domain entities, business rules, output planning contracts, date inference, duplicate grouping rules, and safety policies.
- `PhotoArchive.Infrastructure`: adapters for file system access, ExifTool, EF Core with SQLite, image decoding, thumbnails, and structured logging.
- `PhotoArchive.Cli`: command-line entry point for Milestone 1 preprocessing.
- `PhotoArchive.App`: Avalonia review UI. UI code uses repository services over SQLite and must not perform ExifTool operations.
- `tests`: focused unit and integration tests for rules that affect filenames, dates, duplicates, manifests, and file operations.

## Pipeline

The intended preprocessing pipeline is:

1. Scan input folder recursively.
2. Classify files by extension and detected MIME/signature.
3. Compute SHA-256 hash for each file.
4. Group exact duplicates by hash.
5. Pick one canonical file per duplicate group.
6. Extract metadata for supported images.
7. Infer capture dates with a documented priority order.
8. Generate an output plan without moving files.
9. Validate collisions and safety constraints.
10. Execute only after explicit approval.
11. Write a permanent manifest and operation log.
12. Optionally import the plan into SQLite for review and processing state.

## Boundaries

Domain code must not call the file system, ExifTool, SQLite, or image libraries directly. Those dependencies belong behind interfaces in `PhotoArchive.Core` and implementations in `PhotoArchive.Infrastructure`.

## Persistence

Milestone 2 uses EF Core with SQLite. Runtime code applies migrations before importing preprocessing results. The database stores archive files, inferred metadata, duplicate groups, tags, manual corrections, and operation logs. Re-importing the same preprocessing results updates file and metadata state while preserving operation history.

## Review UI

Milestone 3 uses Avalonia for a local desktop review workflow. The UI loads from the SQLite database, displays gallery and detail views, supports date corrections through `ManualCorrection`, manages tags through `Tag` and `PhotoTag`, marks duplicates through `DuplicateGroup`, and records review actions through `OperationLog`.

Milestone 4 expands the review workflow with search/filtering and related-photo aids. Related photos are scored from deterministic heuristics: same day, same source folder, shared tags, camera model, dimensions, and exact hash. These are review aids only; they do not auto-delete or auto-merge files.

## Metadata Write-Back

Milestone 5 provides an explicit metadata write-back command. The implemented v1 writer uses XMP sidecars to avoid modifying originals. `MetadataWriteBackService` reads corrected dates from SQLite, delegates writes through `IMetadataWriter`, and records every attempt in `OperationLog`.

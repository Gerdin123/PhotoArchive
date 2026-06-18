# Metadata Policy

Metadata is evidence. It is not automatically trusted as the final truth.

## Date Inference Priority

Use this order when inferring capture date:

1. `EXIF:DateTimeOriginal`
2. `EXIF:CreateDate`
3. `XMP:DateCreated`
4. Unambiguous filename date patterns
5. File system created/modified dates
6. Unknown date bucket

## Confidence

- `High`: EXIF/XMP capture date exists and is plausible.
- `Medium`: date came from an unambiguous filename or future nearby-photo inference.
- `Low`: date came only from file system timestamps.
- `Unknown`: no reliable date was found.

File system timestamps must not be treated as high-confidence capture dates, especially for imported, copied, or CD-burned archives.

## Manual Corrections

Manual corrections are written to the local database first. Writing back to EXIF/XMP must be a separate explicit action, must produce an operation log entry, and should operate on copies or sidecars when embedded writes are risky.

## Write-Back

The v1 write-back path is sidecar-first. `PhotoArchive.Cli --write-metadata --db <file>` writes XMP sidecars for manually corrected supported images by default. It writes `exif:DateTimeOriginal`, `xmp:CreateDate`, and `xmp:MetadataDate` from the database value and records each attempt in `OperationLog`.

Original image files are not modified by the v1 sidecar writer. Embedded metadata writes can be added later behind `IMetadataWriter`, but must include backup/copy safety and fixture-copy tests before use.

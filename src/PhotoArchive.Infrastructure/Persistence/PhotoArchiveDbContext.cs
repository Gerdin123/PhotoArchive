using Microsoft.EntityFrameworkCore;
using PhotoArchive.Core.Domain;

namespace PhotoArchive.Infrastructure.Persistence;

public sealed class PhotoArchiveDbContext : DbContext
{
    public PhotoArchiveDbContext(DbContextOptions<PhotoArchiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<ArchiveFile> ArchiveFiles => Set<ArchiveFile>();
    public DbSet<PhotoMetadata> PhotoMetadata => Set<PhotoMetadata>();
    public DbSet<DuplicateGroup> DuplicateGroups => Set<DuplicateGroup>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PhotoTag> PhotoTags => Set<PhotoTag>();
    public DbSet<ManualCorrection> ManualCorrections => Set<ManualCorrection>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArchiveFile>(entity =>
        {
            entity.HasKey(file => file.Id);
            entity.Property(file => file.OriginalPath).IsRequired();
            entity.Property(file => file.OriginalFileName).IsRequired();
            entity.Property(file => file.Extension).IsRequired();
            entity.Property(file => file.MediaKind).HasConversion<string>().IsRequired();
            entity.Property(file => file.Status).HasConversion<string>().IsRequired();
            entity.HasIndex(file => file.Sha256Hash);
            entity.HasIndex(file => file.OriginalPath).IsUnique();
        });

        modelBuilder.Entity<PhotoMetadata>(entity =>
        {
            entity.HasKey(metadata => metadata.ArchiveFileId);
            entity.Property(metadata => metadata.DateConfidence).HasConversion<string>().IsRequired();
        });

        modelBuilder.Entity<DuplicateGroup>(entity =>
        {
            entity.HasKey(group => group.Id);
            entity.Property(group => group.Hash).IsRequired();
            entity.HasIndex(group => group.Hash).IsUnique();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(tag => tag.Id);
            entity.Property(tag => tag.Name).IsRequired();
            entity.Property(tag => tag.Type).HasConversion<string>().IsRequired();
            entity.HasIndex(tag => new { tag.Name, tag.Type }).IsUnique();
        });

        modelBuilder.Entity<PhotoTag>(entity =>
        {
            entity.HasKey(photoTag => new { photoTag.ArchiveFileId, photoTag.TagId });
        });

        modelBuilder.Entity<ManualCorrection>(entity =>
        {
            entity.HasKey(correction => correction.Id);
            entity.Property(correction => correction.FieldName).IsRequired();
            entity.Property(correction => correction.NewValue).IsRequired();
            entity.Property(correction => correction.Reason).IsRequired();
        });

        modelBuilder.Entity<OperationLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.OperationType).IsRequired();
            entity.Property(log => log.SourcePath).IsRequired();
            entity.Property(log => log.Result).IsRequired();
            entity.HasIndex(log => log.SourcePath);
        });
    }
}

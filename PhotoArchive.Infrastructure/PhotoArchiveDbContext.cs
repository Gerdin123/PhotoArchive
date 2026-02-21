using Microsoft.EntityFrameworkCore;
using PhotoArchive.Domain.Entities;

namespace PhotoArchive.Infrastructure;

public sealed class PhotoArchiveDbContext(DbContextOptions<PhotoArchiveDbContext> options)
    : DbContext(options)
{
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PhotoPerson> PhotoPeople => Set<PhotoPerson>();
    public DbSet<PhotoTag> PhotoTags => Set<PhotoTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Photo>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourcePath).HasMaxLength(2048);
            entity.Property(x => x.OutputPath).HasMaxLength(2048);
            entity.Property(x => x.FileName).HasMaxLength(512);
            entity.Property(x => x.Extension).HasMaxLength(32);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.Bucket).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.GroupingDateSource).HasConversion<string>().HasMaxLength(64);

            entity.HasIndex(x => x.Sha256);
            entity.HasIndex(x => x.GroupingDate);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.NormalizedName).HasMaxLength(256);
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.NormalizedName).HasMaxLength(128);
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<PhotoPerson>(entity =>
        {
            entity.HasKey(x => new { x.PhotoId, x.PersonId });

            entity.HasOne(x => x.Photo)
                .WithMany(x => x.PhotoPeople)
                .HasForeignKey(x => x.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Person)
                .WithMany(x => x.PhotoPeople)
                .HasForeignKey(x => x.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PhotoTag>(entity =>
        {
            entity.HasKey(x => new { x.PhotoId, x.TagId });

            entity.HasOne(x => x.Photo)
                .WithMany(x => x.PhotoTags)
                .HasForeignKey(x => x.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Tag)
                .WithMany(x => x.PhotoTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

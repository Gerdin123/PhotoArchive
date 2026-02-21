namespace PhotoArchive.Domain.Entities
{
    /// <summary>
    /// A descriptive tag that can be linked to photos.
    /// </summary>
    public class Tag
    {
        /// <summary>Primary key.</summary>
        public int Id { get; set; }
        /// <summary>Tag label.</summary>
        public required string Name { get; set; }
        public required string NormalizedName { get; set; }

        /// <summary>Many-to-many links between tag and photos.</summary>
        public ICollection<PhotoTag> PhotoTags { get; set; } = [];
    }
}

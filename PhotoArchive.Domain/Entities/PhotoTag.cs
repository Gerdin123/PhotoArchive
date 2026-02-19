namespace PhotoArchive.Domain.Entities
{
    /// <summary>
    /// Join entity between <see cref="Photo"/> and <see cref="Tag"/>.
    /// </summary>
    public class PhotoTag
    {
        /// <summary>Foreign key to photo.</summary>
        public int PhotoId { get; set; }
        /// <summary>Referenced photo.</summary>
        public Photo? Photo { get; set; }

        /// <summary>Foreign key to tag.</summary>
        public int TagId { get; set; }
        /// <summary>Referenced tag.</summary>
        public Tag? Tag { get; set; }
    }
}

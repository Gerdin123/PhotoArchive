namespace PhotoArchive.Core.Entities
{
    /// <summary>
    /// Join entity between <see cref="Photo"/> and <see cref="Person"/>.
    /// </summary>
    public class PhotoPerson
    {
        /// <summary>Foreign key to photo.</summary>
        public int PhotoId { get; set; }
        /// <summary>Referenced photo.</summary>
        public Photo? Photo { get; set; }

        /// <summary>Foreign key to person.</summary>
        public int PersonId { get; set; }
        /// <summary>Referenced person.</summary>
        public Person? Person { get; set; }
    }
}

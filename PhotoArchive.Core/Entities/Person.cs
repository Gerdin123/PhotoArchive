namespace PhotoArchive.Core.Entities
{
    /// <summary>
    /// A person that can be linked to photos.
    /// </summary>
    public class Person
    {
        /// <summary>Primary key.</summary>
        public int Id { get; set; }
        /// <summary>Display name.</summary>
        public required string Name { get; set; }
        public required string NormalizedName { get; set; }

        /// <summary>Many-to-many links between person and photos.</summary>
        public ICollection<PhotoPerson> PhotoPeople { get; set; } = [];
    }
}

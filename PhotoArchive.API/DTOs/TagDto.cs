namespace PhotoArchive.API.DTOs
{
    public class TagDto
    {
        /// <summary>Primary key.</summary>
        public int Id { get; set; }
        /// <summary>Tag label.</summary>
        public string Name { get; set; } = string.Empty;
    }
}

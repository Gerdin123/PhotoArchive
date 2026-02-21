namespace PhotoArchive.API.DTOs
{
    public class PersonDto
    {
        /// <summary>Primary key.</summary>
        public int Id { get; set; }
        /// <summary>Display name.</summary>
        public string Name { get; set; } = string.Empty;
    }
}

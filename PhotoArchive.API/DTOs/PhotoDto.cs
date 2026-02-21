namespace PhotoArchive.API.DTOs
{
    public class PhotoDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime GroupingDate { get; set; }
    }
}

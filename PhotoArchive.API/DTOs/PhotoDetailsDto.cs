namespace PhotoArchive.API.DTOs
{
    public class PhotoDetailsDto
    {
        public int Id { get; set; }
        public DateTime GroupingDate { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public List<PersonDto> People { get; set; } = [];
        public List<TagDto> Tags { get; set; } = [];
    }
}

namespace PhotoArchive.API.DTOs
{
    public class UpdatePhotoDto
    {
        public bool IsDuplicate { get; set; }
        public DateTime GroupingDate { get; set; }

        public IEnumerable<int>? TagIds { get; set; } = [];
        public IEnumerable<int>? PersonIds { get; set; } = [];
    }
}

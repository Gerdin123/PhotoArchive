using PhotoArchive.Domain.Entities;

namespace PhotoArchive.API.DTOs
{
    public class PhotoQueryRequest : PagnationRequest
    {
        public string? Extension { get; set; }

        public bool? IsDuplicate { get; set; }
        public int? GroupingYear { get; set; }
        public DateTime? GroupingDateFrom { get; set; }
        public DateTime? GroupingDateTo { get; set; }

        public int[] TagIds { get; set; } = [];
        public int[] PersonIds { get; set; } = [];
    }
}

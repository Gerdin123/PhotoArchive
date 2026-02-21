namespace PhotoArchive.API.DTOs
{
    public class PagedResponse<T>
    {
        public IEnumerable<T> Items { get; set; } = [];

        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public bool HasPrevious => PageNumber > 1;
        public bool HasNext => PageNumber < TotalPages;
    }
}

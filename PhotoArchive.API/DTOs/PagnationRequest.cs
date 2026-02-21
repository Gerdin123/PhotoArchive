namespace PhotoArchive.API.DTOs
{
    /// <summary>
    /// Represents a request for paginated data, specifying the page number and size.
    /// </summary>
    /// <remarks>The default page size is set to 50, and it cannot exceed a maximum of 200. The page number
    /// starts at 1.</remarks>
    public class PagnationRequest
    {
        private const int MaxPageSize = 200;
        private const int MinPageSize = 1;
        private int _pageNumber = 1;

        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? 1 : value;
        }

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value < MinPageSize)
                {
                    _pageSize = MinPageSize;
                    return;
                }

                _pageSize = value > MaxPageSize ? MaxPageSize : value;
            }
        }
    }
}

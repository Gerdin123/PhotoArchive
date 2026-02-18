namespace PhotoArchive.Core.Entities
{
    public class Photo
    {
        public int Id { get; set; }
        public DateTime? TakenAt { get; set; }

        public required string FilePath { get; set; }
        public required string FileName { get; set; }

        public required string Hash { get; set; }
        public long FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsDuplicate { get; set; }
        public int? DuplicateOfPhotoId { get; set; }


        public ICollection<PhotoTag>? PhotoTags { get; set; }
        public ICollection<PhotoPerson>? PhotoPeople { get; set; }
    }

}

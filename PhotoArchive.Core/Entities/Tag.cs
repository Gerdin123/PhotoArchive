namespace PhotoArchive.Core.Entities
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<PhotoTag>? PhotoTags { get; set; }
    }

}

namespace PhotoArchive.Core.Entities
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<PhotoPerson>? PhotoPeople { get; set; }
    }
}

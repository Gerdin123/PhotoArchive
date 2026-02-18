namespace PhotoArchive.Cleaner
{
    internal interface IFileClassifier
    {
        FileType Classify(string filename);
    }
    internal enum FileType
    {
        Unsupported = 0,
        Image = 1,
    }

    internal class FileClassifier : IFileClassifier
    {
        public FileType Classify(string filename)
        {
            string? extension = Path.GetExtension(filename);

            if(extension == ".jpg")
                return FileType.Image;

            return FileType.Unsupported;
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace PhotoArchive.Cleaner
{
    internal interface IFileMover
    {
        void MoveToImages(string file);
        void MoveToDuplicates(string file);
        void MoveToOthers(string file);

    }

    internal class FileMover : IFileMover
    {
        private readonly string rootPath = "";
        public void MoveToDuplicates(string file)
        {
            throw new NotImplementedException();
        }

        public void MoveToImages(string file)
        {
            throw new NotImplementedException();
        }

        public void MoveToOthers(string file)
        {
            throw new NotImplementedException();
        }

        private string BuildPath(string destination, string file)
        {
            throw new NotImplementedException();
        }

        private void Move(string file, string destination)
        {
            throw new NotImplementedException(); 
        }
    }
}

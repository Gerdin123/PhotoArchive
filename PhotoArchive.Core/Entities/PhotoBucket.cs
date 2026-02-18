namespace PhotoArchive.Core.Entities
{
    /// <summary>
    /// Output bucket assigned by the cleaner.
    /// </summary>
    public enum PhotoBucket
    {
        /// <summary>Primary image set used by the app.</summary>
        Images,
        /// <summary>Files with duplicate content hashes.</summary>
        Duplicates,
        /// <summary>Non-image or filtered-out files.</summary>
        Others
    }
}

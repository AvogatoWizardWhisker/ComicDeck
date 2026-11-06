using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Library
{
    public interface ILibraryScanner
    {
        ///<summary>
        /// Syncs the repository with the comic file under <paramref name="folderPath"/>
        /// Throws <see cref="DirectoryNotFoundException"/> if the folder is missing (e.g unplugged drive),
        /// so a missing folder never wipes its comic from the library 
        /// </summary>
        Task<ScanResults> ScanAsync(
            string folderPath,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}

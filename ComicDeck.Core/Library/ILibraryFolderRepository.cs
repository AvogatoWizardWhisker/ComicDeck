using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Library
{
    public interface ILibraryFolderRepository
    {
        Task<IReadOnlyList<LibraryFolder>> GetFoldersAsync(CancellationToken cancellationToken = default);

        /// <summary>Adds the folder, or returns the existing entry if it was aleady added</summary>
        Task<LibraryFolder> AddFolderAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>Removes only the folder entry. Removing its comic is the caller's decision</summary>
        Task RemoveFolderAsync(int folderId, CancellationToken cancellationToken = default);
    }
}

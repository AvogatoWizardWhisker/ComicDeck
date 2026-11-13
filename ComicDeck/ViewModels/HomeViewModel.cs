using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;

namespace ComicDeck.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly ILibraryFolderRepository _folders;
        public HomeViewModel(ILibraryFolderRepository folders) => _folders = folders;

        [ObservableProperty]
        public partial string Status { get; set; } = "Starting...";

        public async Task LoadAsync()
        {
            var folders = await _folders.GetFoldersAsync();
            Status = $"Database ready. Library folders: {folders.Count}";
        }
    }
}

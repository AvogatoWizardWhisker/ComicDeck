using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using Microsoft.UI.Xaml;
using ComicDeck.Services;

namespace ComicDeck.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILibraryFolderRepository _folders;
    private readonly IComicRepository _comics;

    public SettingsViewModel(ILibraryFolderRepository folders, IComicRepository comics, ThemeService themeService)
    {
        _folders = folders;
        _comics = comics;
        Folders = Array.Empty<LibraryFolder>();
        Status = string.Empty;
        _themeService = themeService;
    }

    private readonly ThemeService _themeService;

    public ElementTheme Theme => _themeService.Theme;

    public string ThemeText => Theme switch
    {
        ElementTheme.Light => "Light",
        ElementTheme.Dark => "Dark",
        _ => "Use system setting"
    };

    public void SetTheme(ElementTheme theme)
    {
        _themeService.Set(App.CurrentWindow, theme);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(ThemeText));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoFolders))]
    public partial IReadOnlyList<LibraryFolder> Folders { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    public bool HasNoFolders => Folders.Count == 0;

    public async Task LoadAsync() => Folders = await _folders.GetFoldersAsync();

    /// <summary>Removes the folder and the library entries (progress and favorites included) for its comics. Files on disk are never touched.</summary>
    public async Task RemoveFolderAsync(LibraryFolder folder)
    {
        var remaining = (await _folders.GetFoldersAsync())
            .Where(f => f.Id != folder.Id)
            .Select(f => f.Path)
            .ToList();

        var inFolder = await _comics.GetComicsInFolderAsync(folder.Path);
        var ids = FolderRemoval.ComicsToRemove(inFolder, remaining);

        // Comics first: if this fails midway the folder is still listed and a rescan restores them.
        await _comics.RemoveComicsAsync(ids);
        await _folders.RemoveFolderAsync(folder.Id);

        await LoadAsync();
        Status = $"Removed the folder and {ids.Count} comic(s) from your library. Your files were not deleted.";
    }

    public async Task AddFolderAsync(string path)
    {
        await _folders.AddFolderAsync(path);
        await LoadAsync();
        Status = "Folder added. Open the library page  and click Rescan to import its comics";
    }
}
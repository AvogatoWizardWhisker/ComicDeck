using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Utilities;
using ComicDeck.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicDeck.ViewModels
{
    public sealed partial class ReaderViewModel : ObservableObject, IDisposable
    {
        private readonly IComicArchiveFactory _archiveFactory;
        private readonly PageImageLoader _loader;

        private IComicArchive? _archive;
        private PageCache? _cache;
        private DecodeRequest _request;
        private int _loadVersion;

        public ReaderViewModel(IComicArchiveFactory archiveFactory, PageImageLoader loader)
        {
            _archiveFactory = archiveFactory;
            _loader = loader;
        }

        [ObservableProperty] public partial string Title { get; set; } = string.Empty;
        [ObservableProperty] public partial ImageSource? CurrentImage { get; set; }
        [ObservableProperty] public partial bool IsLoading { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(PageText))]
        public partial int CurrentIndex { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(PageText))]
        public partial int PageCount { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(PageText))]
        public partial string? ErrorMessage { get; set; }

        public bool HasError => ErrorMessage is not null;
        public string PageText => PageCount == 0 ? string.Empty : $"{CurrentIndex + 1} / {PageCount}";

        public async Task OpenAsync(string path, DecodeRequest request)
        {
            Dispose();
            _request = request;
            ErrorMessage = null;
            Title = Path.GetFileNameWithoutExtension(path);

            try
            {
                // Opening reads the archive's file list, so keep it off the UI thread
                _archive = await Task.Run(() => _archiveFactory.Open(path));
            }
            catch (ArchiveException ex)
            {
                ErrorMessage = ex.Message;
                throw;
            }

            _cache = new PageCache(_archive, _loader);
            PageCount = _archive.Pages.Count;
            await ShowPageAsync(0);
        }

        public Task NextAsync() => ShowPageAsync(CurrentIndex + 1);
        public Task PreviousAsync() => ShowPageAsync(CurrentIndex - 1);
        public Task FirstAsync() => ShowPageAsync(0);
        public Task LastAsync() => ShowPageAsync(PageCount - 1);

        ///<summary>Called when the viewport changes. Reloads the page only if the decode size changed.</summary>
        public async Task UpdateViewPortAsync(DecodeRequest request)
        {
            if (request == _request) return;
            _request = request;
            if (_cache is not null)
                await ShowPageAsync(CurrentIndex);
        }

        private async Task ShowPageAsync(int index)
        {
            if (_cache is null || PageCount == 0) return;

            index = Math.Clamp(index, 0, PageCount - 1);
            CurrentIndex = index; // the counter moves immediately; the image follows when it's ready

            var version = ++_loadVersion;
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var page = await _cache.GetAsync(index, _request);
                if (version != _loadVersion) return; // the reader moved on while this was loading

                CurrentImage = page.Image;
            }
            catch (OperationCanceledException)
            {
                // Superseded or closed
            }
            catch (Exception ex) when (ex is ArchiveException or PageLoadException)
            {
                if (version != _loadVersion) return;
                CurrentImage = null;
                ErrorMessage = ex.Message;
            }
            finally
            {
                if (version == _loadVersion) IsLoading = false;
            }
        }

        public void Dispose()
        {
            _loadVersion++; // any load still in flight is now stale
            _cache?.Dispose(); // the cache first
            _cache = null;
            _archive?.Dispose(); // then the archive
            _archive = null;
            CurrentImage = null;
            PageCount = 0;
            CurrentIndex = 0;
        }
    }
}

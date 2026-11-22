using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Library;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using ComicDeck.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicDeck.ViewModels;

public sealed partial class ReaderViewModel : ObservableObject, IDisposable
{
    private const bool CoverAlone = true;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1.5);

    private readonly IComicArchiveFactory _archiveFactory;
    private readonly PageImageLoader _loader;
    private readonly IComicRepository _comics;
    private readonly TimeProvider _time;
    private readonly IMetadataService _metadata;
    private readonly ReaderSettings _settings;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly SemaphoreSlim _navGate = new(1, 1);
    private readonly Dictionary<int, bool> _landscape = new();

    private IComicArchive? _archive;
    private PageCache? _cache;
    private DecodeRequest _request;
    private SpreadView _view;
    private int _loadVersion;

    private ReadingProgress? _progress;   // null when the comic isn't in the library
    private bool _dirty;
    private CancellationTokenSource? _saveDelay;

    public ReaderViewModel(
        IComicArchiveFactory archiveFactory,
        PageImageLoader loader,
        IComicRepository comics,
        TimeProvider time,
        IMetadataService metadata,
        ReaderSettings settings)
    {
        _archiveFactory = archiveFactory;
        _loader = loader;
        _comics = comics;
        _time = time;
        _metadata = metadata;
        _settings = settings;

        Title = string.Empty;
        FitMode = settings.FitMode;
        IsDoublePage = settings.DoublePage;
    }

    /// <summary>Raised on the UI thread after the displayed images changed.</summary>
    public event EventHandler? DisplayChanged;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial ImageSource? LeftImage { get; set; }

    [ObservableProperty]
    public partial ImageSource? RightImage { get; set; }

    /// <summary> True when two pages are on screen </summary>
    [ObservableProperty]
    public partial bool IsSpread { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    public partial int CurrentIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    public partial int ViewSize { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    public partial int PageCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial FitMode FitMode { get; set; }

    [ObservableProperty]
    public partial bool IsDoublePage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirectionText))]
    [NotifyPropertyChangedFor(nameof(DirectionGlyph))]
    public partial bool IsRightToLeft { get; set; }

    public bool HasError => ErrorMessage is not null;
    public string DirectionText => IsRightToLeft ? "Right to left" : "Left to right";
    public string DirectionGlyph => IsRightToLeft ? "\uE72A" : "\uE72B";

    public string PageText =>
        PageCount == 0 ? string.Empty
        : ViewSize == 2 ? $"{CurrentIndex + 1} – {CurrentIndex + 2} / {PageCount}"
        : $"{CurrentIndex + 1} / {PageCount}";

    // opening

    public async Task OpenAsync(string path, int? comicId, DecodeRequest request)
    {
        Dispose();
        var token = _loadVersion;   // changes if the reader is closed while we are still opening

        _request = request;
        ErrorMessage = null;
        Title = Path.GetFileNameWithoutExtension(path);

        IComicArchive archive;
        try
        {
            archive = await Task.Run(() => _archiveFactory.Open(path));
        }
        catch (ArchiveException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        if (token != _loadVersion)
        {
            archive.Dispose();
            return;
        }

        _archive = archive;
        PageCount = archive.Pages.Count;
        RebuildCache();

        var rtl = await ReadIsRightToLeftAsync(archive);
        if (token != _loadVersion) return;
        IsRightToLeft = rtl;

        var start = 0;
        if (comicId is int id)
        {
            var progress = await LoadProgressAsync(id);
            if (token != _loadVersion) return;

            _progress = progress;
            if (progress.Status == ReadStatus.InProgress)
                start = Math.Clamp(progress.CurrentPage, 0, PageCount - 1);
        }

        await NavigateAsync(() => PlanAtAsync(start), track: false);
    }

    private async Task<bool> ReadIsRightToLeftAsync(IComicArchive archive)
    {
        try
        {
            var meta = await Task.Run(() => _metadata.ReadAsync(archive));
            return meta.ReadingDirection == ReadingDirection.RightToLeft;
        }
        catch (Exception)
        {
            return false;   // bad metadata must never stop a comic from opening
        }
    }

    private async Task<ReadingProgress> LoadProgressAsync(int comicId)
    {
        try
        {
            return await _comics.GetProgressAsync(comicId) ?? new ReadingProgress { ComicId = comicId };
        }
        catch (Exception)
        {
            return new ReadingProgress { ComicId = comicId };
        }
    }

    private void RebuildCache()
    {
        _cache?.Dispose();
        // Two pages on screen need a slightly wider window around the current one.
        _cache = _archive is null ? null : new PageCache(_archive, _loader, IsDoublePage ? 3 : 2);
    }

    // commands

    public Task NextAsync() => NavigateAsync(PlanNextAsync);
    public Task PreviousAsync() => NavigateAsync(() => PlanBeforeAsync(_view.Start));
    public Task FirstAsync() => NavigateAsync(() => PlanAtAsync(0));
    public Task LastAsync() => NavigateAsync(() => PlanBeforeAsync(PageCount));
    public Task GoToAsync(int index) => NavigateAsync(() => PlanAtAsync(index));

    /// <summary>Arrow keys and click zones are visual, so right means "previous" in right-to-left comics.</summary>
    public Task StepRightAsync() => IsRightToLeft ? PreviousAsync() : NextAsync();
    public Task StepLeftAsync() => IsRightToLeft ? NextAsync() : PreviousAsync();

    public async Task UpdateRequestAsync(DecodeRequest request)
    {
        if (request == _request) return;
        _request = request;
        if (_cache is not null)
            await RedisplayAsync();   // a resize isn't reading progress
    }

    public async Task SetFitModeAsync(FitMode fit, DecodeRequest request)
    {
        FitMode = fit;
        _settings.FitMode = fit;
        _request = request;
        if (_cache is not null)
            await RedisplayAsync();
    }

    public async Task SetDoublePageAsync(bool on)
    {
        if (on == IsDoublePage) return;
        IsDoublePage = on;
        _settings.DoublePage = on;

        if (_cache is null) return;
        RebuildCache();
        await NavigateAsync(() => PlanAtAsync(_view.Start), track: false);
    }

    public async Task ToggleDirectionAsync()
    {
        IsRightToLeft = !IsRightToLeft;
        if (_cache is not null)
            await RedisplayAsync();
    }

    private Task RedisplayAsync() =>
        NavigateAsync(() => Task.FromResult(_view.Count == 0 ? (SpreadView?)null : _view), track: false);

    // planning: decide which pages form the next view

    private Task<SpreadView?> PlanNextAsync()
    {
        var next = _view.Start + _view.Count;
        return next >= PageCount ? Task.FromResult<SpreadView?>(null) : ViewAtAsync(next);
    }

    private Task<SpreadView?> PlanAtAsync(int index) =>
        ViewAtAsync(Math.Clamp(index, 0, Math.Max(PageCount - 1, 0)));

    private async Task<SpreadView?> ViewAtAsync(int start)
    {
        if (IsDoublePage && !await EnsureShapesAsync(start, start + 1)) return null;
        return SpreadPlanner.ViewAt(start, PageCount, IsDoublePage, CoverAlone, IsLandscape);
    }

    private async Task<SpreadView?> PlanBeforeAsync(int start)
    {
        if (start <= 0) return null;
        if (IsDoublePage && !await EnsureShapesAsync(start - 1, start - 2)) return null;
        return SpreadPlanner.ViewBefore(start, PageCount, IsDoublePage, CoverAlone, IsLandscape);
    }

    private bool IsLandscape(int index) => _landscape.TryGetValue(index, out var wide) && wide;

    /// <summary> Makes sure we know whether each page is landscape. Returns false if the reader closed meanwhile. </summary>
    private async Task<bool> EnsureShapesAsync(params int[] indices)
    {
        var token = _loadVersion;
        try
        {
            foreach (var index in indices)
            {
                if (index < 0 || index >= PageCount || _landscape.ContainsKey(index)) continue;

                var cache = _cache;
                if (cache is null) return false;

                try
                {
                    var page = await cache.GetAsync(index, _request);
                    if (token != _loadVersion) return false;
                    _landscape[index] = page.IsLandscape;
                }
                catch (Exception ex) when (ex is ArchiveException or PageLoadException)
                {
                    _landscape[index] = false;   // the display step will report the error
                }
            }
            return token == _loadVersion && _cache is not null;
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    // showing a view

    private async Task NavigateAsync(Func<Task<SpreadView?>> plan, bool track = true)
    {
        SpreadView view;
        int version;

        // Plans run one at a time, so each builds on the latest view even when keys repeat quickly.
        await _navGate.WaitAsync();
        try
        {
            if (_cache is null || PageCount == 0) return;

            if (await plan() is not { } planned) return;
            view = planned;

            _view = view;
            CurrentIndex = view.Start;
            ViewSize = view.Count;
            if (track) RecordProgress();

            version = ++_loadVersion;
            IsLoading = true;
            ErrorMessage = null;
        }
        finally
        {
            _navGate.Release();
        }

        await DisplayAsync(view, version);
    }

    private async Task DisplayAsync(SpreadView view, int version)
    {
        var cache = _cache;
        if (cache is null) return;

        try
        {
            var loads = new Task<LoadedPage>[view.Count];
            for (var i = 0; i < view.Count; i++)
                loads[i] = cache.GetAsync(view.Start + i, _request);

            var pages = await Task.WhenAll(loads);
            if (version != _loadVersion) return;   // the reader moved on while this was loading

            foreach (var page in pages)
                _landscape[page.Index] = page.IsLandscape;

            if (pages.Length == 1)
            {
                LeftImage = pages[0].Image;
                RightImage = null;
                IsSpread = false;
            }
            else
            {
                // The first page in reading order sits on the right in right-to-left comics.
                LeftImage = IsRightToLeft ? pages[1].Image : pages[0].Image;
                RightImage = IsRightToLeft ? pages[0].Image : pages[1].Image;
                IsSpread = true;
            }

            DisplayChanged?.Invoke(this, EventArgs.Empty);

            // Evicted bitmaps free their native memory only when the Garbage Collector finalizes them,
            // and the GC can't see that memory, so prompt it now and then
            if(++_turnsSinceCollect >= 8)
            {
                _turnsSinceCollect = 0;
                _ = Task.Run(() => GC.Collect(2, GCCollectionMode.Forced, blocking: false));
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded or closed.
        }
        catch (ObjectDisposedException)
        {
            // Closed while loading.
        }
        catch (Exception ex) when (ex is ArchiveException or PageLoadException)
        {
            if (version != _loadVersion) return;
            LeftImage = null;
            RightImage = null;
            IsSpread = false;
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }

    // progress saving

    private void RecordProgress()
    {
        if (_progress is null) return;

        // A view that contains the last page counts as finished; otherwise remember where it starts
        var lastVisible = _view.Start + _view.Count - 1;
        var page = lastVisible >= PageCount - 1 ? PageCount - 1 : _view.Start;

        _progress.UpdatePage(page, PageCount, _time.GetUtcNow().UtcDateTime);
        _dirty = true;

        _saveDelay?.Cancel();
        var cts = _saveDelay = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(cts.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaveDelay, token);
            await SaveNowAsync();
        }
        catch (OperationCanceledException)
        {
            // Another page turn restarted the timer, or the reader closed (which saves itself)
        }
    }

    private async Task SaveNowAsync()
    {
        var progress = _progress;
        if (progress is null || !_dirty) return;

        await _saveGate.WaitAsync();
        try
        {
            if (!_dirty) return;
            _dirty = false;

            var snapshot = new ReadingProgress
            {
                Id = progress.Id,
                ComicId = progress.ComicId,
                CurrentPage = progress.CurrentPage,
                Status = progress.Status,
                LastReadUtc = progress.LastReadUtc,
                CompletedUtc = progress.CompletedUtc
            };

            await _comics.SaveProgressAsync(snapshot);
            progress.Id = snapshot.Id;
        }
        catch (Exception)
        {
            _dirty = true;   // best effort; the next page turn tries again
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary> Saves any pending progress, then releases everything. Call this when leaving the reader </summary>
    public async Task CloseAsync()
    {
        _saveDelay?.Cancel();
        await SaveNowAsync();
        Dispose();
    }

    public void Dispose()
    {
        _loadVersion++;
        _saveDelay?.Cancel();
        _saveDelay = null;
        _progress = null;
        _dirty = false;

        _cache?.Dispose();       // the cache first,
        _cache = null;
        _archive?.Dispose();     // then the archive
        _archive = null;

        _landscape.Clear();
        _view = default;
        LeftImage = null;
        RightImage = null;
        IsSpread = false;
        PageCount = 0;
        CurrentIndex = 0;
        ViewSize = 1;
    }

    private int _turnsSinceCollect;
}

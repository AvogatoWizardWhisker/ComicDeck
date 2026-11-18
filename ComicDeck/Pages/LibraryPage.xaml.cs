using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ComicDeck.ViewModels;
using ComicDeck.Helpers;
using ComicDeck.Services;
using ComicDeck.Core.Library;

namespace ComicDeck.Pages
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; } = App.GetService<LibraryViewModel>();
        public LibraryPage()
        {
            InitializeComponent();

            // the grouped grid and the letter grid are filled from code, because the groups are rebuilt on every query.
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LibraryViewModel.Groups)) BindGroups();
            };
            Loaded += async (_, _) => await ViewModel.LoadAsync();
        }

        private void BindGroups()
        {
            GroupedSource.Source = ViewModel.Groups;
            GroupedGrid.ItemsSource = GroupedSource.View;
            AlphaGrid.ItemsSource = ViewModel.Tiles;
        }

        // toolbar
        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = await FilePickers.PickFolderAsync();
            if (path is not null)
                await ViewModel.AddFolderAsync(path);
        }

        private async void Rescan_Click(object sender, RoutedEventArgs e) => await ViewModel.RescanAsync();
        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
            await ViewModel.SetSearchAsync(sender.Text);

        private async void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (SortBox.SelectedItem is ComboBoxItem { Tag: string tag } && Enum.TryParse<LibrarySort>(tag, out var sort))
                await ViewModel.SetSortAsync(sort);
        }

        private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FilterBox.SelectedItem is ComboBoxItem { Tag: string tag } && Enum.TryParse<LibraryFilter>(tag, out var filter))
                await ViewModel.SetFilterAsync(filter);
        }

        private async void Reverse_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.SetReverseAsync(ReverseToggle.IsChecked == true);

        private void FocusSearch(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            SearchBox.Focus(FocusState.Keyboard);
        }

        private void ShowAlphabet_Click(object sender, RoutedEventArgs e) =>
            Zoom.IsZoomedInViewActive = false;

        // jump list

        private void Zoom_ViewChangeStarted(object sender, SemanticZoomViewChangedEventArgs e)
        {
            if (e.IsSourceZoomedInView)
            {
                // Zooming out: highlight the letter of the comic at the top of the grid.
                if (e.SourceItem.Item is ComicItemViewModel comic)
                    e.DestinationItem.Item = ViewModel.TileFor(comic);
            }
            else if (e.SourceItem.Item is AlphaTile { First: { } first })
            {
                // Zooming in: scroll to the first comic of the chosen letter.
                e.DestinationItem.Item = first;
            }
        }

        private void AlphaGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            // Letters with no comics are greyed out and can't be clicked.
            if (args.Item is AlphaTile tile)
                args.ItemContainer.IsEnabled = tile.HasItems;
        }

        // cards (shared by both grids)

        private void ComicGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ComicItemViewModel comic)
                Frame.Navigate(typeof(ReaderPage), new ReaderRequest(comic.FilePath, comic.Id));
        }

        private void ComicGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not ComicItemViewModel comic) return;

            if (args.InRecycleQueue)
                comic.UnloadCover();            // scrolled away: free the bitmap
            else
                _ = comic.LoadCoverAsync();     // scrolled into view: load (or create) the cover
        }

        private async void MarkRead_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ComicItemViewModel comic)
                await ViewModel.SetReadAsync(comic, read: true);
        }

        private async void MarkUnread_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ComicItemViewModel comic)
                await ViewModel.SetReadAsync(comic, read: false);
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ComicItemViewModel comic)
                await ViewModel.ToggleFavoriteAsync(comic);
        }
    }
}

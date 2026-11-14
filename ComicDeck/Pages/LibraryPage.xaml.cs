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

namespace ComicDeck.Pages
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; } = App.GetService<LibraryViewModel>();
        public LibraryPage()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ViewModel.LoadAsync();
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = await FilePickers.PickFolderAsync();
            if (path is not null)
                await ViewModel.AddFolderAsync(path);
        }

        private async void Rescan_Click(object sender, RoutedEventArgs e) => await ViewModel.RescanAsync();
        /*
        private void ComicGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ComicItemViewModel comic)
                Frame.Navigate(typeof(ReaderPage), comic.FilePath);
        }
        */
        private void ComicGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not ComicItemViewModel comic) return;

            if (args.InRecycleQueue)
                comic.UnloadCover();
            else
                _ = comic.LoadCoverAsync();
        }

        private void ComicGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ComicItemViewModel comic)
                Frame.Navigate(typeof(ReaderPage), new ReaderRequest(comic.FilePath, comic.Id));
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
    }
}

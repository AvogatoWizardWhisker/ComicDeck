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
using ComicDeck.Services;
using ComicDeck.ViewModels;
using Microsoft.UI.Xaml.Automation;


namespace ComicDeck.Pages
{
    public sealed partial class SeriesDetailPage : Page
    {
        public SeriesDetailViewModel ViewModel { get; } = App.GetService<SeriesDetailViewModel>();
        private string? _series;
        public SeriesDetailPage()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                if (_series is not null) await ViewModel.LoadAsync(_series);
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) => _series = e.Parameter as string;

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ContinueRequest is { } request)
                Frame.Navigate(typeof(ReaderPage), request);
        }

        private void Issue_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ComicItemViewModel comic)
                Frame.Navigate(typeof(ReaderPage), new ReaderRequest(comic.FilePath, comic.Id));
        }

        private void Issue_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not ComicItemViewModel comic) return;

            if (args.InRecycleQueue) comic.UnloadCover();
            else _ = comic.LoadCoverAsync();
        }

        private void Breadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Index != 0) return;   // the last item is the current page

            // Go back if we came from the series list, so the back stack doesn't grow.
            if (Frame.CanGoBack && Frame.BackStack.LastOrDefault()?.SourcePageType == typeof(SeriesPage))
                Frame.GoBack();
            else
                Frame.Navigate(typeof(SeriesPage));
        }

        private async void MarkRead_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ComicItemViewModel comic)
                await ViewModel.SetReadAsync(comic, read: true);
        }

        private async void MarkUnread_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ComicItemViewModel comic)
                await ViewModel.SetReadAsync(comic, read:false);
        }

        private async void MarkAllRead_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.SetAllReadAsync(true);

        private async void MarkAllUnread_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.SetAllReadAsync(false);
    }
}

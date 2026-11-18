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

namespace ComicDeck.Pages
{
    public sealed partial class SeriesPage : Page
    {
        public SeriesViewModel ViewModel { get; } = App.GetService<SeriesViewModel>();
        public SeriesPage()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ViewModel.LoadAsync();
        }

        private void SeriesGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SeriesCardViewModel series)
                Frame.Navigate(typeof(SeriesDetailPage), series.Name);
        }

        private void SeriesGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not SeriesCardViewModel series) return;

            if (args.InRecycleQueue) series.UnloadCover();
            else _ = series.LoadCoverAsync();
        }
    }
}

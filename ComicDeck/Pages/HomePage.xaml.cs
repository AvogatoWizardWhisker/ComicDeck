using ComicDeck.Helpers;
using ComicDeck.Services;
using ComicDeck.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace ComicDeck.Pages
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; } = App.GetService<HomeViewModel>();
        
        public HomePage()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ViewModel.LoadAsync();
        }

        private async void OpenComic_Click(object sender, RoutedEventArgs e)
        {
            var path = await FilePickers.PickComicAsync();
            if (path is not null)
                Frame.Navigate(typeof(ReaderPage), new ReaderRequest(path));
        }

        private void ContinueList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ComicItemViewModel comic)
                Frame.Navigate(typeof(ReaderPage), new ReaderRequest(comic.FilePath, comic.Id));
        }

        private void ContinueList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not ComicItemViewModel comic) return;

            if (args.InRecycleQueue)
                comic.UnloadCover();
            else
                _ = comic.LoadCoverAsync();
        }
    }
}

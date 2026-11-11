using Microsoft.UI.Xaml.Controls;
using ComicDeck.ViewModels;
using ComicDeck.Core.Archives;
using ComicDeck.Services;
using ComicDeck.Core.Utilities;
using Microsoft.UI.Xaml;
using ComicDeck.Helpers;

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
                Frame.Navigate(typeof(ReaderPage), path);
        }
    }
}

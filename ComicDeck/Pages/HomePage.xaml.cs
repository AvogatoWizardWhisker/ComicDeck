using Microsoft.UI.Xaml.Controls;
using ComicDeck.ViewModels;
using ComicDeck.Core.Archives;
using ComicDeck.Services;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Pages
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; } = App.GetService<HomeViewModel>();
        
        public HomePage()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ViewModel.LoadAsync();
            /*Loaded += async (_, _) =>
            {
                await ViewModel.LoadAsync();

                using var archive = App.GetService<IComicArchiveFactory>().Open(@"C:\path\to\a\real.cbz");
                var page = await App.GetService<PageImageLoader>().LoadAsync(archive, 0, new DecodeRequest(0, 800));

                ViewModel.Status = $"Page 1: natural {page.NaturalWidth}x{page.NaturalHeight}, " +
                                   $"decoded {page.Image.PixelWidth}x{page.Image.PixelHeight}";
            };*/
        }
    }
}

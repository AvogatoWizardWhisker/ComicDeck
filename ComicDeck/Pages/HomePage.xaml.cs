using Microsoft.UI.Xaml.Controls;
using ComicDeck.ViewModels;

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
    }
}

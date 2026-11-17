using ComicDeck.Core.Models;
using ComicDeck.ViewModels;
using ComicDeck.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace ComicDeck.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

        private bool _loadingTheme;

        public SettingsPage()
        {
            InitializeComponent();

            _loadingTheme = true; // selecting the saved option must not count as a user change
            (ViewModel.Theme switch
            {
                ElementTheme.Light => LightRadio,
                ElementTheme.Dark => DarkRadio,
                _ => SystemRadio
            }).IsChecked = true;
            _loadingTheme = false;

            Loaded += async (_, _) => await ViewModel.LoadAsync();
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingTheme) return;
            if (sender is RadioButton { Tag: string tag } && Enum.TryParse<ElementTheme>(tag, out var theme))
                ViewModel.SetTheme(theme);
        }

        private async void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not LibraryFolder folder) return;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Remove this folder?",
                Content = $"{folder.Path}\n\nIts comics will leave your library, with their reading progress and favorites. The files stay on your your disk.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await ViewModel.RemoveFolderAsync(folder);
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = await FilePickers.PickFolderAsync();
            if (path is not null)
                await ViewModel.AddFolderAsync(path);
        }

        public string Copyright => $"© {DateTime.Now.Year} Solus Gameolus";

        public string VersionText
        {
            get
            {
                var v = Package.Current.Id.Version;
                return $"Version {v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
        }

    }
}

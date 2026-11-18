using ComicDeck.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Library;
using ComicDeck.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace ComicDeck
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon("Assets/AppIcon.ico");
        }

        private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }

        private void TitleBar_BackRequested(TitleBar sender, object args)
        {
            NavFrame.GoBack();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag)
                {
                    case "home":
                        NavFrame.Navigate(typeof(HomePage));
                        break;
                    case "library":
                        NavFrame.Navigate(typeof(LibraryPage));
                        break;
                    case "series":
                        NavFrame.Navigate(typeof(SeriesPage));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
                }
            }
        }

        public bool IsFullScreen => AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

        /// <summary> the reader hides the navigation pan while it is open</summary>
        public void SetReaderMode(bool on)
        {
            NavView.IsPaneVisible = !on;
            AppTitleBar.IsPaneToggleButtonVisible = !on;

            if (on) NavView.IsPaneOpen = false; // so it isn't left open when the reader closes
            else SetFullScreen(false);
        }

        public void SetFullScreen(bool on)
        {
            if (on != IsFullScreen)
                AppWindow.SetPresenter(on ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);

            AppTitleBar.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            TitleRow.Height = new GridLength(on ? 0 : 40);
        }

        public async Task OpenComicAsync(string path)
        {
            if (!ComicFileTypes.IsComic(path)) return;

            int? id = null;
            try
            {
                var found = await App.GetService<IComicRepository>().FindComicIdAsync(path);
            }
            catch (Exception)
            {
                // a database problem must not stop the comic from opening
            }

            NavFrame.Navigate(typeof(ReaderPage), new ReaderRequest(path, id));
        }

        private void Root_DragOver(object sender, DragEventArgs e)
        {
            var ok = e.DataView.Contains(StandardDataFormats.StorageItems);
            e.AcceptedOperation = ok ? DataPackageOperation.Copy : DataPackageOperation.None;
            if (ok) e.DragUIOverride.Caption = "Open comic"; 
        }

        private async void Root_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            var path = items.OfType<StorageFile>().Select(f => f.Path).FirstOrDefault(ComicFileTypes.IsComic);
            if (path is not null) await OpenComicAsync(path);
        }
    }
}

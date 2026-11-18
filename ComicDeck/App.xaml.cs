using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Data;
using ComicDeck.Core.Library;
using ComicDeck.Core.Metadata;
using ComicDeck.Services;
using ComicDeck.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.Security.Cryptography.Core;

namespace ComicDeck
{
    public partial class App : Application
    {
        private Window? _window;

        public static IServiceProvider Services { get; private set; } = null!;

        public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

        public App()
        {
            InitializeComponent();
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // opening the database takes a few milliseconds, and pages need it as soon as they load
            var database = await ComicDatabase.OpenAsync(AppPaths.DatabasePath);
            Services = ConfigureServices(database);

            _window = new MainWindow();
            GetService<ThemeService>().Apply(_window);
            CurrentWindow = _window;
            _window.Closed += async (_, _) => await database.DisposeAsync();
            _window.Activate();

            if (GetLaunchFilePath() is { } openPath && _window is MainWindow main)
            {
                // low priority runs after the navigation view has made its first (Home) selection,
                // so the reader isn't overwritten by it
                main.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () => await main.OpenComicAsync(openPath));
            }
        }

        private static string? GetLaunchFilePath()
        {
            var activiation = AppInstance.GetCurrent().GetActivatedEventArgs();
            return activiation.Kind == ExtendedActivationKind.File
                && activiation.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs file
                ? file.Files.FirstOrDefault()?.Path
                : null;
        }

        private static IServiceProvider ConfigureServices(ComicDatabase database)
        {
            var services = new ServiceCollection();

            // Core
            services.AddSingleton(database);
            services.AddSingleton<IComicRepository, SqliteComicRepository>();
            services.AddSingleton<ILibraryFolderRepository, SqliteLibraryFolderRepository>();
            services.AddSingleton<IComicArchiveFactory, ComicArchiveFactory>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<LibraryScanner, LibraryScanner>();

            //App services
            services.AddSingleton<PageImageLoader>();
            services.AddSingleton<CoverThumbnailService>();
            services.AddSingleton<ReaderSettings>();
            services.AddSingleton<ThemeService>();
            
            
            // ViewModels : one per page, created each time the page is opened
            services.AddTransient<HomeViewModel>();
            services.AddTransient<ReaderViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SeriesViewModel>();
            services.AddTransient<SeriesDetailViewModel>();

            return services.BuildServiceProvider();

        }

        public static Window CurrentWindow { get; private set; } = null!;
    }

}

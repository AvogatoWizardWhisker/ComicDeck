using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage;

namespace ComicDeck.Services
{
    /// <summary>
    /// Where the app keeps its files. Packaged apps use the private LocalState folder
    /// </summary>
    public static class AppPaths
    {
        public static string DataFolder => ApplicationData.Current.LocalFolder.Path;

        public static string DatabasePath => Path.Combine(DataFolder, "library.db");
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ComicDeck.Helpers
{
    public static class FilePickers
    {
        ///<summary>Returns the choosen comic's path, or null if the user cancelled.</summary>
        public static async Task<string?> PickComicAsync()
        {
            var picker = new FileOpenPicker();

            // WinUI 3 pickers must be told which window owns them, or they throw
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.CurrentWindow));

            foreach (var extension in new[] { ".cbz", ".cbr", ".cb7", ".cbt" })
                picker.FileTypeFilter.Add(extension);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}

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
using ComicDeck.Core.Utilities;
using ComicDeck.ViewModels;
using Windows.System;

namespace ComicDeck.Pages
{
    public sealed partial class ReaderPage : Page
    {
        public ReaderViewModel ViewModel { get; } = App.GetService<ReaderViewModel>();

        private string? _path;
        private bool _opened;
        
        public ReaderPage()
        {
            InitializeComponent();
            Loaded += ReaderPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) => _path = e.Parameter as string;

        protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Dispose();

        private async void ReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_path is null || _opened) return;
            _opened = true;
            await ViewModel.OpenAsync(_path, CurrentRequest());
        }

        private async void PageImage_SizeChanged(object sender, SizeChangedEventArgs e) =>
            await ViewModel.UpdateViewPortAsync(CurrentRequest());

        private DecodeRequest CurrentRequest() => DecodeRequest.ForViewport(
            PageImage.ActualWidth, PageImage.ActualHeight, XamlRoot?.RasterizationScale ?? 1.0);

        private async void OnAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            switch (sender.Key)
            {
                case VirtualKey.Right:
                case VirtualKey.Space:
                case VirtualKey.PageDown:
                    await ViewModel.NextAsync();
                    break;
                case VirtualKey.Left:
                case VirtualKey.PageUp:
                    await ViewModel.PreviousAsync();
                    break;
                case VirtualKey.Home:
                    await ViewModel.FirstAsync();
                    break;
                case VirtualKey.End:
                    await ViewModel.LastAsync();
                    break;
                case VirtualKey.Escape:
                    GoBack();
                    break;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) => GoBack();

        private void GoBack()
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}

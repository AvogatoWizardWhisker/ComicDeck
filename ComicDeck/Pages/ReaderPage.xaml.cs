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
using System.ComponentModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using ComicDeck.ViewModels;
using ComicDeck.Services;
using Windows.System;

namespace ComicDeck.Pages
{
    public sealed partial class ReaderPage : Page
    {
        private const double ZoomHeadroom = 1.0;   // decode this much larger than the screen, so zoom stays sharp
        private const double DoubleTapZoom = 1.5;

        public ReaderViewModel ViewModel { get; } = App.GetService<ReaderViewModel>();

        private readonly DispatcherTimer _tapTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        private ReaderRequest? _launch;
        private bool _opened;
        private bool _syncingSlider;

        public ReaderPage()
        {
            InitializeComponent();
            Loaded += ReaderPage_Loaded;

            ViewModel.DisplayChanged += OnDisplayChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            _tapTimer.Tick += (_, _) => { _tapTimer.Stop(); ToggleOverlay(); };
            _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); SetOverlayVisible(false); };

            UpdateFitChecks();
        }

        private MainWindow? Shell => App.CurrentWindow as MainWindow;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _launch = e.Parameter as ReaderRequest;
            Shell?.SetReaderMode(true);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _tapTimer.Stop();
            _hideTimer.Stop();
            ViewModel.DisplayChanged -= OnDisplayChanged;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            Shell?.SetReaderMode(false);
            _ = ViewModel.CloseAsync();
        }

        private async void ReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_launch is null || _opened) return;
            _opened = true;

            await ViewModel.OpenAsync(_launch.Path, _launch.ComicId, CurrentRequest());
            _hideTimer.Start();   // the overlay gets out of the way a few seconds after opening
        }

        // sizing and layout

        private DecodeRequest CurrentRequest(FitMode? fit = null) => DecodeRequest.ForFit(
            fit ?? ViewModel.FitMode,
            Scroller.ActualWidth,
            Scroller.ActualHeight,
            XamlRoot?.RasterizationScale ?? 1.0,
            ZoomHeadroom);

        private async void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyLayout(resetView: false);
            await ViewModel.UpdateRequestAsync(CurrentRequest());
        }

        private void OnDisplayChanged(object? sender, EventArgs e) => ApplyLayout(resetView: true);

        private void ApplyLayout(bool resetView)
        {
            var width = Scroller.ActualWidth;
            var height = Scroller.ActualHeight;
            if (width <= 0 || height <= 0) return;

            var count = ViewModel.IsSpread ? 2 : 1;

            // The host is at least as big as the viewport, so smaller pages stay centred.
            PageHost.MinWidth = width;
            PageHost.MinHeight = height;

            var maxWidth = double.PositiveInfinity;
            var maxHeight = double.PositiveInfinity;
            var stretch = Stretch.Uniform;

            switch (ViewModel.FitMode)
            {
                case FitMode.FitPage:
                    maxWidth = width / count;
                    maxHeight = height;
                    break;
                case FitMode.FitWidth:
                    maxWidth = width / count;
                    break;
                case FitMode.FitHeight:
                    maxHeight = height;
                    break;
                case FitMode.OriginalSize:
                    stretch = Stretch.None;
                    break;
            }

            foreach (var image in new[] { LeftPage, RightPage })
            {
                image.MaxWidth = maxWidth;
                image.MaxHeight = maxHeight;
                image.Stretch = stretch;
            }

            if (ViewModel.IsSpread)
            {
                // Pages touch in the middle.
                Grid.SetColumn(LeftPage, 1);
                Grid.SetColumnSpan(LeftPage, 1);
                LeftPage.HorizontalAlignment = HorizontalAlignment.Right;

                Grid.SetColumn(RightPage, 2);
                RightPage.HorizontalAlignment = HorizontalAlignment.Left;
                RightPage.Visibility = Visibility.Visible;
            }
            else
            {
                Grid.SetColumn(LeftPage, 1);
                Grid.SetColumnSpan(LeftPage, 2);
                LeftPage.HorizontalAlignment = HorizontalAlignment.Center;
                RightPage.Visibility = Visibility.Collapsed;
            }

            if (resetView)
            {
                Scroller.UpdateLayout();
                var x = ViewModel.IsRightToLeft ? Scroller.ScrollableWidth : 0;
                var y = _landAtBottom ? Scroller.ScrollableHeight : 0;
                Scroller.ChangeView(x, y, 1f, disableAnimation: true);
            }
        }

        // keyboard

        private async void OnAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            switch (sender.Key)
            {
                case VirtualKey.Down:
                    await AdvanceAsync(forward: true, fraction: 0.15);
                    break;
                case VirtualKey.Up:
                    await AdvanceAsync(forward: false, fraction: 0.15);
                    break;
                case VirtualKey.Right:
                    await AdvanceAsync(forward: !ViewModel.IsRightToLeft, fraction: 0.15);
                    break;
                case VirtualKey.Left:
                    await AdvanceAsync(forward: ViewModel.IsRightToLeft, fraction: 0.15);
                    break;
                case VirtualKey.Space:
                case VirtualKey.PageDown:
                    await AdvanceAsync(forward: true);
                    break;
                case VirtualKey.PageUp:
                    await AdvanceAsync(forward: false);
                    break;
                case VirtualKey.Home:
                    await ViewModel.FirstAsync();
                    break;
                case VirtualKey.End:
                    await ViewModel.LastAsync();
                    break;
                case VirtualKey.Escape:
                    if (Shell?.IsFullScreen == true) Shell.SetFullScreen(false);
                    else GoBack();
                    break;
                case VirtualKey.F11:
                    ToggleFullscreen();
                    break;
                case VirtualKey.D:
                    await ViewModel.SetDoublePageAsync(!ViewModel.IsDoublePage);
                    break;
                case VirtualKey.R:
                    await ViewModel.ToggleDirectionAsync();
                    break;
                case VirtualKey.F:
                    await SetFitAsync((FitMode)(((int)ViewModel.FitMode + 1) % 4));
                    break;
            }
        }

        // mouse

        private bool _landAtBottom; // set while going back, so the previous page opens at its bottom
        private long _lastWheelTurn;

        private async void Scroller_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // Ctrl+wheel is zoom and Shift+wheel is horizontal scroll: leave both to the ScrollViewer.
            if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) return;

            var props = e.GetCurrentPoint(Scroller).Properties;
            if (props.IsHorizontalMouseWheel || props.MouseWheelDelta == 0) return;

            e.Handled = true;   // the ScrollViewer must not scroll on its own

            const double edge = 2;           // pixels of tolerance at the very top or bottom
            const double pixelsPerNotch = 100;

            var forward = props.MouseWheelDelta < 0;
            var offset = Scroller.VerticalOffset;
            var atEdge = forward
                ? Scroller.ScrollableHeight - offset <= edge
                : offset <= edge;

            if (!atEdge)
            {
                var distance = Math.Abs(props.MouseWheelDelta) / 120.0 * pixelsPerNotch;
                var target = forward
                    ? Math.Min(offset + distance, Scroller.ScrollableHeight)
                    : Math.Max(offset - distance, 0);
                Scroller.ChangeView(null, target, null, disableAnimation: true);
                return;
            }

            // At the edge: turn the page, at most once per short interval.
            if (Environment.TickCount64 - _lastWheelTurn < 450) return;
            _lastWheelTurn = Environment.TickCount64;

            if (forward)
            {
                await ViewModel.NextAsync();
            }
            else
            {
                _landAtBottom = true;
                try { await ViewModel.PreviousAsync(); }
                finally { _landAtBottom = false; }
            }

            _lastWheelTurn = Environment.TickCount64;
        }

        /// <summary> Scrolls the page by <paramref name="fraction"/> of the viewport height. 
        /// At the top or bottom edge, turns the page instead. Going back lands at the bottom of the previous page.
        /// </summary>
        private async Task AdvanceAsync(bool forward, double fraction = 0.9)
        {
            const double edge = 4;   // pixels of tolerance at the very top or bottom
            var step = Scroller.ViewportHeight * fraction;

            if (forward)
            {
                if (Scroller.ScrollableHeight - Scroller.VerticalOffset > edge)
                {
                    Scroller.ChangeView(null, Math.Min(Scroller.VerticalOffset + step, Scroller.ScrollableHeight), null);
                    return;
                }
                await ViewModel.NextAsync();
            }
            else
            {
                if (Scroller.VerticalOffset > edge)
                {
                    Scroller.ChangeView(null, Math.Max(Scroller.VerticalOffset - step, 0), null);
                    return;
                }

                _landAtBottom = true;
                try { await ViewModel.PreviousAsync(); }
                finally { _landAtBottom = false; }
            }
        }

        // pointer: click zones and double-tap zoom

        private async void Scroller_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var x = e.GetPosition(Scroller).X / Scroller.ActualWidth;

            if (x < 0.3)
            {
                await ViewModel.StepLeftAsync();
            }
            else if (x > 0.7)
            {
                await ViewModel.StepRightAsync();
            }
            else
            {
                // Wait briefly: a second tap means "zoom", not "toggle the overlay".
                _tapTimer.Stop();
                _tapTimer.Start();
            }
        }

        private void Scroller_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var position = e.GetPosition(Scroller);
            var x = position.X / Scroller.ActualWidth;
            if (x is < 0.3 or > 0.7) return;   // the edges are page-turn zones

            _tapTimer.Stop();
            e.Handled = true;

            if (Scroller.ZoomFactor > 1.05f)
            {
                Scroller.ChangeView(null, null, 1f);
                return;
            }

            var zoom = (float)DoubleTapZoom;
            var contentX = (Scroller.HorizontalOffset + position.X) / Scroller.ZoomFactor;
            var contentY = (Scroller.VerticalOffset + position.Y) / Scroller.ZoomFactor;
            Scroller.ChangeView(contentX * zoom - position.X, contentY * zoom - position.Y, zoom);
        }

        // overlay

        private void ToggleOverlay()
        {
            _hideTimer.Stop();
            SetOverlayVisible(TopBar.Visibility != Visibility.Visible);
        }

        private void SetOverlayVisible(bool visible)
        {
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            TopBar.Visibility = visibility;
            BottomBar.Visibility = visibility;
        }

        private void Back_Click(object sender, RoutedEventArgs e) => GoBack();

        private void GoBack()
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

        private void ToggleFullscreen()
        {
            if (Shell is { } shell)
                shell.SetFullScreen(!shell.IsFullScreen);
        }

        private async void Double_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.SetDoublePageAsync(DoubleToggle.IsChecked == true);

        private async void Direction_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.ToggleDirectionAsync();

        private async void Fit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioMenuFlyoutItem { Tag: string tag } && Enum.TryParse<FitMode>(tag, out var fit))
                await SetFitAsync(fit);
        }

        private async Task SetFitAsync(FitMode fit)
        {
            await ViewModel.SetFitModeAsync(fit, CurrentRequest(fit));
            UpdateFitChecks();
        }

        private void UpdateFitChecks()
        {
            FitPageItem.IsChecked = ViewModel.FitMode == FitMode.FitPage;
            FitWidthItem.IsChecked = ViewModel.FitMode == FitMode.FitWidth;
            FitHeightItem.IsChecked = ViewModel.FitMode == FitMode.FitHeight;
            OriginalItem.IsChecked = ViewModel.FitMode == FitMode.OriginalSize;
        }

        // page slider

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ReaderViewModel.CurrentIndex)
                or nameof(ReaderViewModel.PageCount)
                or nameof(ReaderViewModel.IsRightToLeft))
            {
                SyncSlider();
            }
        }

        private void SyncSlider()
        {
            _syncingSlider = true;   // setting the slider from code must not count as a user drag
            try
            {
                PageSlider.Maximum = Math.Max(ViewModel.PageCount, 2);
                PageSlider.IsEnabled = ViewModel.PageCount > 1;
                PageSlider.FlowDirection = ViewModel.IsRightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
                PageSlider.Value = ViewModel.CurrentIndex + 1;
            }
            finally
            {
                _syncingSlider = false;
            }
        }

        private async void PageSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_syncingSlider) return;
            await ViewModel.GoToAsync((int)Math.Round(e.NewValue) - 1);
        }
    }
}
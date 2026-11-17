using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace ComicDeck.Services
{
    /// <summary> Stores the chosen app theme and applies it to the window content.</summary>
    public sealed class ThemeService
    {
        private readonly IPropertySet _values = ApplicationData.Current.LocalSettings.Values;

        public ElementTheme Theme
        {
            get => _values.TryGetValue(nameof(Theme), out var v) && v is int i && Enum.IsDefined((ElementTheme)i)
                ? (ElementTheme)i
                : ElementTheme.Default; // Default = follow the system theme
            set => _values[nameof(Theme)] = (int)value;
        }

        ///<summary> Applies the saved theme to the window's root element</summary>
        public void Apply(Window window)
        {
            if (window.Content is not FrameworkElement root) return;

            root.RequestedTheme = Theme;
            UpdateCaptionButtons(window, root);

            // following the system: re-apply whe, Windows switches theme while the app runs.
            root.ActualThemeChanged -= OnActualThemeChanged;
            root.ActualThemeChanged += OnActualThemeChanged;

            void OnActualThemeChanged(FrameworkElement sender, object args) => UpdateCaptionButtons(window, sender);
        }

        public void Set(Window window, ElementTheme theme)
        {
            Theme = theme;
            Apply(window);
        }

        private void UpdateCaptionButtons(Window window, FrameworkElement root)
        {
            var dark = root.ActualTheme == ElementTheme.Dark;
            var titleBar = window.AppWindow.TitleBar;

            var foregroud = dark ? Colors.White : Colors.Black;
            var hover = dark ? Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A) : Color.FromArgb(0xFF, 0xE5, 0xE5, 0xE5);
            var pressed = dark ? Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B) : Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC);

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foregroud;
            titleBar.ButtonInactiveForegroundColor = dark 
                ? Color.FromArgb(0x77, 0x9E, 0x9E, 0x9E)
                : Color.FromArgb(0x77, 0x6E, 0x6E, 0x6E);
            titleBar.ButtonHoverBackgroundColor = hover;
            titleBar.ButtonHoverForegroundColor = foregroud;
            titleBar.ButtonPressedBackgroundColor = pressed;
            titleBar.ButtonPressedForegroundColor= foregroud;


        }
    }
}

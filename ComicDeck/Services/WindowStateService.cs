using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using Microsoft.UI.Windowing;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Storage;
using System.Xml.Linq;

namespace ComicDeck.Services
{
    /// <summary> Restores the window's last size, position, maximized state </summary>
    public sealed class WindowStateService
    {
        private const string Key = "WindowBounds";

        private readonly IPropertySet _values = ApplicationData.Current.LocalSettings.Values;
        private RectInt32 _normal;
        private bool _maximized;

        public void Attach(AppWindow window)
        {
            Restore(window);
            if (!_maximized) _normal = Bounds(window);

            window.Changed += OnChanged;
            window.Closing += (_, _) => Save();
        }

        private void Restore(AppWindow window)
        {
            if (!TryRead(out var rect, out var maximized))
            {
                window.Resize(new SizeInt32(1280, 820));
                return;
            }

            // Ignore the saved position if that monitor is no longer connected.
            var titleBarPoint = new PointInt32(rect.X + rect.Width / 2, rect.Y + 20);
            if (DisplayArea.GetFromPoint(titleBarPoint, DisplayAreaFallback.None) is null) return;

            window.MoveAndResize(rect);
            _normal = rect;
            _maximized = maximized;
            if (maximized && window.Presenter is OverlappedPresenter presenter)
                presenter.Maximize();
        }

        private void OnChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidSizeChange && !args.DidPositionChange && !args.DidPresenterChange) return;
            if (sender.Presenter is not OverlappedPresenter presenter) return;   // fullscreen keeps the last bounds
            if (presenter.State == OverlappedPresenterState.Minimized) return;

            _maximized = presenter.State == OverlappedPresenterState.Maximized;
            if (!_maximized) _normal = Bounds(sender);
        }

        private void Save() => _values[Key] = string.Create(CultureInfo.InvariantCulture,
            $"{_normal.X},{_normal.Y},{_normal.Width},{_normal.Height},{(_maximized ? 1 : 0)}");

        private bool TryRead(out RectInt32 rect, out bool maximized)
        {
            rect = default;
            maximized = false;

            var parts = (_values.TryGetValue(Key, out var v) ? v as string : null)?.Split(',');
            if (parts is not { Length: 5 }) return false;

            var n = new int[5];
            for (var i = 0; i < 5; i++)
                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i]))
                    return false;

            if (n[2] < 640 || n[3] < 400) return false;

            rect = new RectInt32(n[0], n[1], n[2], n[3]);
            maximized = n[4] == 1;
            return true;
        }

        private static RectInt32 Bounds(AppWindow w) =>
            new(w.Position.X, w.Position.Y, w.Size.Width, w.Size.Height);
    }
}

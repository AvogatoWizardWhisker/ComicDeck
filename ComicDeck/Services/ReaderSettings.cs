using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace ComicDeck.Services
{
    /// <summary> Reader preference that survive restarts </summary>
    public sealed class ReaderSettings
    {
        private readonly IPropertySet _values = ApplicationData.Current.LocalSettings.Values;

        public FitMode FitMode
        {
            get => _values.TryGetValue(nameof(FitMode), out var v) && v is int i && Enum.IsDefined((FitMode)i)
                ? (FitMode)i
                : FitMode.FitPage;
            set => _values[nameof(FitMode)] = (int)value;
        }

        public bool DoublePage
        {
            get => _values.TryGetValue(nameof(DoublePage), out var v) && v is true;
            set => _values[nameof(DoublePage)] = value;
        }
    }
}

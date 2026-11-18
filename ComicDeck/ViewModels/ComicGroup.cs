using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.ViewModels
{
    /// <summary> One letter section of the library grid.</summary>
    public sealed class ComicGroup : List<ComicItemViewModel>
    {
        public ComicGroup(string key, IEnumerable<ComicItemViewModel> items) : base(items) => Key = key;

        public string Key { get; }
    }

    ///<summary>One tile in the zoomed-out letter grid. <see cref="First"/> is null when the letter has no comics.</summary>
    public sealed record AlphaTile(string Key, ComicItemViewModel? First)
    {
        public bool HasItems => First is not null;
    }
}

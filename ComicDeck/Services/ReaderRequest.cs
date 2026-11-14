using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Services
{
    ///<summary> What the reader page is asked to open. A null ComicId means "not in the library": no progress is saved</summary>
    public sealed record ReaderRequest(string Path, int? ComicId = null);
}

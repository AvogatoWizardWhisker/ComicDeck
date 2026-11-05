using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Archives
{
    public enum ArchiveErrorKind
    {
        Corrupt, Unsupported, PasswordProtected, NotFound, Empty
    }

    public sealed class ArchiveException : Exception
    {
        public ArchiveErrorKind Kind { get; }

        public ArchiveException(ArchiveErrorKind kind, string message, Exception? inner = null) : base(message, inner) => Kind = kind;

    }
}

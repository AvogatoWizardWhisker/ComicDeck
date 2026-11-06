using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Models
{
    public class ReadingProgress
    {
        public int Id { get; set; }
        public int ComicId { get; set; }

        /// <summary> 0-based index of the page the reader was last on. </summary>
        public int CurrentPage { get; set; }

        public ReadStatus Status { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }

        /// <summary> 
        /// Records thats the last reader is now on <paramref name="pageIndex"/>
        /// The last page marks the comic as completed; page 0 alone doesn't count as started.
        /// </summary>
        public void UpdatePage(int pageIndex, int pageCount, DateTime nowUtc)
        {
            if (pageCount <= 0) return;

            CurrentPage = Math.Clamp(pageIndex, 0, pageCount - 1);
            LastReadUtc = nowUtc;

            if (CurrentPage == pageCount - 1)
            {
                if (Status != ReadStatus.Completed)
                    CompletedUtc = nowUtc;
                Status = ReadStatus.Completed;
            }
            else if (CurrentPage > 0)
            {
                Status = ReadStatus.InProgress;
            }
            // page 0 : leave the status as -is, since it doesn't count as started
        }

        public void MarkCompleted(int pageCount, DateTime nowUtc)
        {
            CurrentPage = Math.Max(pageCount - 1, 0);
            Status = ReadStatus.Completed;
            LastReadUtc = nowUtc;
            CompletedUtc ??= nowUtc;
        }

        public void MarkUnread()
        {
            CurrentPage = 0;
            Status = ReadStatus.Unread;
            LastReadUtc = null;
            CompletedUtc = null;
        }

        /// <summary> 0.0 to 1.0 for progress bars </summary>
        public double GetFraction(int pageCount) => pageCount <= 1 ? (Status == ReadStatus.Completed ? 1.0 : 0.0)
            : Math.Clamp((double)CurrentPage / (pageCount - 1), 0.0, 1.0);
    }
}

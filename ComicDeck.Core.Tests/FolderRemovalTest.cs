using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;
using ComicDeck.Core.Library;

namespace ComicDeck.Core.Tests
{
    public class FolderRemovalTest
    {
        private static Comic C(int id, string path) => new() { Id = id, FilePath = path };

        [Fact]
        public void Removes_everything_when_no_other_folder_covers_it()
        {
            var ids = FolderRemoval.ComicsToRemove(new[] { C(1, @"D:\Comics\a.cbz"), C(2, @"D:\Comics\Sub\b.cbz") }, Array.Empty<string>());

            Assert.Equal(new[] { 1, 2 }, ids);
        }

        [Fact]
        public void Keeps_comics_still_under_a_remaning_folder()
        {
            var ids = FolderRemoval.ComicsToRemove(
                new[] { C(1, @"D:\Comics\a.cbz"), C(2, @"D:\Comics\Sub\b.cbz")},
                new[] { @"d:\comics\sub"});

            Assert.Equal(new[] { 1 }, ids);
        }

        [Fact]
        public void A_folder_with_a_similar_name_does_not_count_as_covering()
        {
            var ids = FolderRemoval.ComicsToRemove(
                new[] { C(1, @"D:\Comics\a.cbz") },
                new[] { @"D:\Com" });

            Assert.Equal(new[] { 1 }, ids);
        }
    }
}

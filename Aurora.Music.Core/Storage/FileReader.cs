﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aurora.Shared.Helpers;
using TagLib;
using Windows.Storage;

namespace Aurora.Music.Core.Storage
{
    public class FileReader
    {
        private static readonly string[] types = new[] { ".mp3", ".m4a", ".flac", ".wav" };

        public event EventHandler<ProgressReport> ProgressUpdated;
        public event EventHandler Completed;
        private ProgressReport report = new ProgressReport();
        public event EventHandler<SongsAddedEventArgs> NewSongsAdded;


        /// <summary>
        /// TODO: Only pick files which not in the database, and find deleted files to delete
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        private async Task<List<StorageFile>> GetFilesAsync(StorageFolder folder)
        {
            var files = new List<StorageFile>();
            files.AddRange(await folder.GetFilesAsync());
            var folders = await folder.GetFoldersAsync();
            foreach (var item in folders)
            {
                files.AddRange(await GetFilesAsync(item));
            }
            return files;
        }

        public async static Task<List<Models.Album>> GetAlbumsAsync()
        {
            var opr = SQLOperator.Current();
            var albums = await opr.GetAllAsync<Album>();
            return albums.ConvertAll(a => new Models.Album(a));
        }

        public async Task Read(IList<StorageFolder> folder)
        {
            var list = new List<StorageFile>();
            double i = 1;
            report.Stage = 1;
            report.StageTotal = 3;
            report.Percent = 0;
            foreach (var item in folder)
            {
                list.AddRange(await GetFilesAsync(item));

                report.Stage = 1;
                report.Percent = 100 * i / folder.Count;
                i++;
                ProgressUpdated?.Invoke(this, report);
            }
            list.Distinct();
            report.Stage = 1;
            report.Percent = 100;
            ProgressUpdated?.Invoke(this, report);
            await ReadFileandSave(list);
        }

        private async Task ReadFileandSave(IList<StorageFile> files)
        {
            var opr = SQLOperator.Current();
            List<Models.Song> tempList = new List<Models.Song>();
            double i = 1;
            var total = files.Count;
            foreach (var file in files)
            {
                bool b = false;

                foreach (var item in types)
                {
                    if (item.Equals(file.FileType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        b = true;
                        break;
                    }
                }
                if (!b)
                {
                    continue;
                }

                using (var tagTemp = File.Create(file.Path))
                {
                    var proper = await file.Properties.GetMusicPropertiesAsync();
                    tempList.Add(await Models.Song.Create(tagTemp.Tag, file.Path, proper));
                }
                report.Stage = 2;
                report.Percent = 100 * i / files.Count;
                i++;
                ProgressUpdated?.Invoke(this, report);
            }

            report.Stage = 3;
            report.Percent = 0;
            i = 1;
            ProgressUpdated?.Invoke(this, report);
            var newlist = new List<Song>();
            foreach (var song in tempList)
            {
                var t = await opr.UpdateSongAsync(song);
                if (t != null)
                {
                    newlist.Add(t);
                }
                report.Stage = 3;
                report.Percent = 100 * i / tempList.Count;
                i++;
                ProgressUpdated?.Invoke(this, report);
            }
            if (newlist.Count > 0)
            {
                NewSongsAdded?.Invoke(this, new SongsAddedEventArgs(newlist.ToArray()));
            }
        }

        public async Task<List<Models.Song>> GetSongsAsync()
        {
            var opr = SQLOperator.Current();
            var songs = await opr.GetAllAsync<Song>();
            return songs.ConvertAll(a => new Models.Song(a));
        }

        public async Task AddToAlbums(IEnumerable<Song> songs)
        {
            await Task.Run(async () =>
            {
                report.Stage = 4;
                report.Percent = 0;
                ProgressUpdated?.Invoke(this, report);
                double i = 1;

                var albums = from song in songs group song by song.Album into album select album;
                var opr = SQLOperator.Current();

                var count = albums.Count();
                foreach (var item in albums)
                {
                    await opr.AddAlbumAsync(item);
                    report.Stage = 4;
                    report.Percent = 100 * (i++) / count;
                    ProgressUpdated?.Invoke(this, report);
                }
            });
        }
    }

    public class ProgressReport
    {
        public int Stage { get; set; }
        public int StageTotal { get; set; }

        private double progress;

        public double Percent
        {
            get { return progress; }
            set
            {
                if (value > 100)
                    progress = 100;
                else
                    progress = value;
            }
        }

    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.IO.Hashing;

namespace FindDuplicates
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private List<FileInfo> GetFilesInDirectory(string directory)
        {
            var files = new List<FileInfo>();
            try
            {
                var directories = Directory.GetDirectories(directory);
                try
                {
                    var di = new DirectoryInfo(directory);
                    files.AddRange(di.GetFiles("*"));
                }
                catch
                {
                }
                foreach (var dir in directories)
                {
                    files.AddRange(GetFilesInDirectory(System.IO.Path.Combine(directory, dir)));
                }
            }
            catch
            {
            }

            return files;
        }

        private async Task<List<IGrouping<long, FileInfo>>> GetPossibleDuplicatesAsync(string selectedPath)
        {
            List<IGrouping<long, FileInfo>> files = null;
            await Task.Factory.StartNew(() =>
            {
                files = GetFilesInDirectory(selectedPath)
                               .OrderByDescending(f => f.Length)
                                 .GroupBy(f => f.Length)
                                 .Where(g => g.Count() > 1)
                                 .Take(100)
                                 .ToList();
            });
            return files;
        }

        private async void StartClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFolderDialog();
            if (ofd.ShowDialog() != true)
                return;
            var sw = new Stopwatch();
            sw.Start();
            FilesList.ItemsSource = null;
            var selectedPath = ofd.FolderName;

            var files = await GetPossibleDuplicatesAsync(selectedPath);
            FilesList.ItemsSource = await GetRealDuplicatesAsync(files);
            sw.Stop();
            var allFiles = files.SelectMany(f => f).ToList();
            TotalFilesText.Text = $"{allFiles.Count} files found " +
                $"({allFiles.Sum(f => f.Length):N0} total duplicate bytes) {sw.ElapsedMilliseconds} ms";
        }

        private async Task<Dictionary<uint, List<FileInfo>>> GetRealDuplicatesAsync(
            List<IGrouping<long, FileInfo>> files)
        {
            var dictFiles = new ConcurrentDictionary<uint, ConcurrentBag<FileInfo>>();
            await Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(files.SelectMany(f => f), file => 
                {
                    var crc = GetCrc32FromFile(file.FullName);
                    if (crc.Length > 0)
                    {
                        var hash = BitConverter.ToUInt32(GetCrc32FromFile(file.FullName),0);
                        if (dictFiles.ContainsKey(hash))
                            dictFiles[hash].Add(file);
                        else
                            dictFiles.TryAdd(hash, new ConcurrentBag<FileInfo>(new[] { file }));
                    }
                });
            });
            return dictFiles.Where(p => p.Value.Count > 1)
                .OrderByDescending(p => p.Value.First().Length)
                .ToDictionary(p => p.Key, p => p.Value.ToList());
        }

        public byte[] GetCrc32FromFile(string fileName)
        {
            try
            {
                using (var file = new FileStream(fileName, FileMode.Open,FileAccess.Read))
                {
                    // const int NumBytes = 1000;
                    // var bytes = new byte[NumBytes];
                    // var numRead = file.Read(bytes, 0, NumBytes);
                    // if (numRead == 0)
                    //     return [];
                    // var crc = new Crc32();
                    // while (numRead > 0)
                    // {
                    //     crc.Append(bytes);
                    //     numRead = file.Read(bytes, 0, NumBytes);
                    // }
                    var crc = new Crc32();
                    crc.Append(file);
                    return crc.GetCurrentHash();
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return [];
            }
        }

    }
}

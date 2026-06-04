using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace JxrConverter
{
    public class FileEntry : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public string Directory => Path.GetDirectoryName(FilePath);

        public string SizeDisplay
        {
            get
            {
                try
                {
                    long size = new FileInfo(FilePath).Length;
                    if (size < 1024) return $"• {size} B";
                    if (size < 1024 * 1024) return $"• {size / 1024.0:F1} KB";
                    return $"• {size / (1024.0 * 1024.0):F1} MB";
                }
                catch { return ""; }
            }
        }

        private string _status = "Queued";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    "Queued" => "● Queued",
                    "Converting" => "◉ Converting",
                    "Done" => "✓ Done",
                    "Error" => "✗ Error",
                    _ => Status
                };
            }
        }

        public SolidColorBrush StatusColor
        {
            get
            {
                return Status switch
                {
                    "Queued" => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xa0)),
                    "Converting" => new SolidColorBrush(Color.FromRgb(0xfb, 0xbf, 0x24)),
                    "Done" => new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80)),
                    "Error" => new SolidColorBrush(Color.FromRgb(0xf8, 0x71, 0x71)),
                    _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xa0))
                };
            }
        }

        private Visibility _removeVisibility = Visibility.Visible;
        public Visibility RemoveVisibility
        {
            get => _removeVisibility;
            set { _removeVisibility = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<FileEntry> _files = new();
        private bool _converting = false;
        private string _exePath;
        private string _outputDir;

        public MainWindow()
        {
            InitializeComponent();

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            _outputDir = Path.Combine(appDir, "Converted PNGs");

            try
            {
                _exePath = ExtractConverterExe();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize conversion engine: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            OutputPathLabel.Text = _outputDir;
            FileList.ItemsSource = _files;
        }

        private string ExtractConverterExe()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "JxrConverter");
            Directory.CreateDirectory(tempDir);
            string tempExePath = Path.Combine(tempDir, "jxr_to_png.exe");

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string resourceName = "jxr_to_png.exe"; 

            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    throw new FileNotFoundException("The embedded conversion engine was not found in the application resources.");
                }

                bool shouldWrite = true;
                if (File.Exists(tempExePath))
                {
                    try
                    {
                        using (var existingFile = File.OpenRead(tempExePath))
                        {
                            if (existingFile.Length == resourceStream.Length)
                            {
                                shouldWrite = false;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (shouldWrite)
                {
                    using (FileStream fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }

            return tempExePath;
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_converting) return;

            var dialog = new OpenFileDialog
            {
                Title = "Select JXR Files",
                Filter = "JPEG XR Files (*.jxr)|*.jxr|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddFilePaths(dialog.FileNames);
            }
        }

        private void AddFilePaths(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(_files.Select(f => f.FilePath), StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                if (!path.EndsWith(".jxr", StringComparison.OrdinalIgnoreCase)) continue;
                if (existing.Contains(path)) continue;
                if (!File.Exists(path)) continue;

                _files.Add(new FileEntry { FilePath = path });
                existing.Add(path);
            }

            UpdateUI();
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (_converting) return;
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string path)
            {
                var item = _files.FirstOrDefault(f => f.FilePath == path);
                if (item != null) _files.Remove(item);
                UpdateUI();
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_converting) return;
            _files.Clear();
            ProgressBar.Width = 0;
            ProgressLabel.Text = "";
            OpenFolderBtn.Visibility = Visibility.Collapsed;
            UpdateUI();
        }

        private void UpdateUI()
        {
            int count = _files.Count;
            FileCountLabel.Text = $"{count} file{(count != 1 ? "s" : "")} selected";
            ConvertBtn.IsEnabled = count > 0 && !_converting;
            EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (_converting) { e.Effects = DragDropEffects.None; return; }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool hasJxr = files.Any(f => f.EndsWith(".jxr", StringComparison.OrdinalIgnoreCase));
                e.Effects = hasJxr ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (_converting) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFilePaths(files);
            }
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (_converting || _files.Count == 0) return;

            if (!File.Exists(_exePath))
            {
                ProgressLabel.Text = $"Error: jxr_to_png.exe not found at {_exePath}";
                ProgressLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xf8, 0x71, 0x71));
                return;
            }

            _converting = true;
            ConvertBtn.IsEnabled = false;
            ConvertBtn.Content = "Converting...";
            AddFilesBtn.IsEnabled = false;
            ClearAllBtn.IsEnabled = false;
            OpenFolderBtn.Visibility = Visibility.Collapsed;

            foreach (var item in _files)
                item.RemoveVisibility = Visibility.Collapsed;

            System.IO.Directory.CreateDirectory(_outputDir);

            int total = _files.Count;
            int successCount = 0;
            int errorCount = 0;
            double containerWidth = ((FrameworkElement)ProgressBar.Parent).ActualWidth;

            for (int i = 0; i < _files.Count; i++)
            {
                var item = _files[i];
                item.Status = "Converting";
                ProgressLabel.Text = $"Converting {item.FileName}...";
                ProgressLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xa0));
                ProgressBar.Width = (double)(i) / total * containerWidth;

                string outputPath = Path.Combine(_outputDir, Path.GetFileNameWithoutExtension(item.FilePath) + ".png");

                try
                {
                    bool success = await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = _exePath,
                            Arguments = $"\"{item.FilePath}\" \"{outputPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using var proc = Process.Start(psi);
                        proc.WaitForExit(120_000);
                        return proc.ExitCode == 0;
                    });

                    if (success)
                    {
                        item.Status = "Done";
                        successCount++;
                    }
                    else
                    {
                        item.Status = "Error";
                        errorCount++;
                    }
                }
                catch
                {
                    item.Status = "Error";
                    errorCount++;
                }
            }

            ProgressBar.Width = containerWidth;

            string summary = $"✓ {successCount} converted";
            if (errorCount > 0) summary += $"  •  ✗ {errorCount} failed";

            if (errorCount == 0)
            {
                ProgressBar.Background = new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80));
                ProgressLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80));
            }
            else
            {
                ProgressBar.Background = new SolidColorBrush(Color.FromRgb(0xfb, 0xbf, 0x24));
                ProgressLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xfb, 0xbf, 0x24));
            }

            ProgressLabel.Text = summary;

            _converting = false;
            ConvertBtn.IsEnabled = true;
            ConvertBtn.Content = "Convert All";
            AddFilesBtn.IsEnabled = true;
            ClearAllBtn.IsEnabled = true;
            ProgressBar.Background = new SolidColorBrush(Color.FromRgb(0x7c, 0x6a, 0xef));
            OpenFolderBtn.Visibility = Visibility.Visible;

            foreach (var item in _files)
                item.RemoveVisibility = Visibility.Visible;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.Directory.Exists(_outputDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _outputDir,
                    UseShellExecute = true
                });
            }
        }
    }
}

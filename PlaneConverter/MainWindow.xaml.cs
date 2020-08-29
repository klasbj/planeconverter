using PlaneConverter.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;

namespace PlaneConverter
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IDictionary<string, string> lastBrowsedPaths;

        public MainWindow()
        {
            InitializeComponent();

            SourceDirectoryBrowse.Click += (a, b) => PopupFileDialog("source", SourceDirectory);
            TargetDirectoryBrowse.Click += (a, b) => PopupFileDialog("target", TargetDirectory);
            LayoutUpdatePackageDirectoryBrowse.Click += (a, b) => PopupFileDialog("package", LayoutUpdatePackageDirectory);

            Convert.Click += ConvertClick;
            LayoutUpdate.Click += LayoutUpdateClick;

            Tabs.SelectedIndex = Settings.Default.LastUsedTab;
            PackageName.Text = Settings.Default.PackagePrefix + "-";
            TargetDirectory.Text = Settings.Default.TargetFolder;
            lastBrowsedPaths = JsonSerializer.Deserialize<Dictionary<string,string>>(Settings.Default.LastBrowsedPaths);
        }

        private void LayoutUpdateClick(object sender, RoutedEventArgs _e)
        {
            try
            {
                Settings.Default.LastUsedTab = Tabs.SelectedIndex;
                Settings.Default.Save();

                WriteLayout(LayoutUpdatePackageDirectory.Text);
                System.Windows.MessageBox.Show("Successfully updated layout.json");
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show($"Error: {e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConvertClick(object sender, RoutedEventArgs _e)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.Delete(tempPath);
            Directory.CreateDirectory(tempPath);
            Debug.WriteLine($"Using temp directory: {tempPath}");

            try
            {

                Settings.Default.PackagePrefix = PackageName.Text.Split("-")[0];
                Settings.Default.TargetFolder = TargetDirectory.Text;
                Settings.Default.LastUsedTab = Tabs.SelectedIndex;
                Settings.Default.Save();

                var dirName = Path.GetFileName(SourceDirectory.Text);

                WriteManifest(tempPath);
                CopyFiles(Path.Combine(tempPath, "SimObjects", "Airplanes", dirName), SourceDirectory.Text);
                JsonIfyTextures(tempPath);
                WriteLayout(tempPath);
                CopyFiles(Path.Combine(TargetDirectory.Text, PackageName.Text), tempPath);
                
                System.Windows.MessageBox.Show("Successfully converted simobject");
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show($"Error: {e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempPath, true);
                }
                catch { }
            }
        }

        private void JsonIfyTextures(string path)
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!Path.GetExtension(f).Equals(".dds", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var imgData = new { Version = 2, SourceFileDate = DateTime.Today.ToFileTime(), Flags = new[] { "FL_BITMAP_COMPRESSION", "FL_BITMAP_MIPMAP" }, HasTransp = true };
                File.WriteAllText(f + ".json", JsonSerializer.Serialize(imgData));
            }
        }

        private void WriteLayout(string path)
        {
            var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Select(f => new FileInfo(f)).Select(f => new FileData
            {
                Path = Path.GetRelativePath(path, f.FullName).Replace('\\', '/'),
                Size = f.Length
            }).Where(f => !f.Path.Equals("layout.json", StringComparison.InvariantCultureIgnoreCase) && f.Path.Substring(0, 1) != ".").ToList();

            files.ForEach(x => Debug.WriteLine($"File: {x}"));

            var layout = new
            {
                content = files
            };

            File.WriteAllText(Path.Combine(path, "layout.json"), JsonSerializer.Serialize(layout, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }

        private void CopyFiles(string to, string from)
        {
            try
            {
                Directory.CreateDirectory(to);
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show("There was an issue creating the directory\n" + e.Message);
                throw;
            }
            
            foreach (string full in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var f = Path.GetRelativePath(from, full);
                var subDir = Path.GetDirectoryName(f);
                if (!string.IsNullOrEmpty(subDir)) {
                    Directory.CreateDirectory(Path.Combine(to, subDir));
                }
                File.Copy(Path.Combine(from, f), Path.Combine(to, f));
            }
        }

        private void PopupFileDialog(string title, System.Windows.Controls.TextBox textBox)
        {
            var openDialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                SelectedPath = textBox.Text,
                Description = $"Select {title}"
            };

            if (string.IsNullOrEmpty(openDialog.SelectedPath) && lastBrowsedPaths.TryGetValue(title, out var path))
            {
                openDialog.SelectedPath = path;
            }

            var result = openDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                textBox.Text = openDialog.SelectedPath;
                lastBrowsedPaths[title] = openDialog.SelectedPath;
                Settings.Default.LastBrowsedPaths = JsonSerializer.Serialize(lastBrowsedPaths);
                Settings.Default.Save();
            }
        }

        private void WriteManifest(string path)
        {
            var manifest = new
            {
                dependencies = new object[0],
                content_type = "AIRCRAFT",
                title = AircraftTitle.Text,
                manufacturer = Manufacturer.Text,
                creator = Creator.Text,
                package_version = Version.Text,
                minimum_game_version = "1.7.12",
                release_notes = new
                {
                    neutral = new
                    {
                        LastUpdate = string.Empty,
                        OlderHistory = string.Empty
                    }
                }
            };

            File.WriteAllText(Path.Combine(path, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }

    public class FileData
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public long Date => DateTime.Today.ToFileTime();

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}

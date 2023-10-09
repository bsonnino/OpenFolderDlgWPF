using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace OpenFolderDialogWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectFolderClick(object sender, RoutedEventArgs e)
    {
        FilesList.Items.Clear();
        var ofd = new OpenFolderDialog();
        if (ofd.ShowDialog() != true)
        {
            FilesList.Items.Add("No folder selected");
            return;
        }
        var selectedPath = ofd.FolderName;

        var files = new DirectoryInfo(selectedPath)
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Select(f => new { Name = Path.GetRelativePath(selectedPath,f.FullName), Size = f.Length });
        TotalFilesText.Text = $"File count: {files.Count()}";
        LengthFilesText.Text = $"Total size: {files.Sum(f => f.Size).ToString("N0")}";
        FilesList.ItemsSource = files
            .OrderByDescending(f => f.Size)
            .Select(f => $"{f.Name} ({f.Size.ToString("N0")})");
    }
}
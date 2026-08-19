using System.IO;
using System.Windows;

namespace MusicPlayer.Views;

public partial class EditSongDialog : Window
{
    public string TitleValue => TitleBox.Text.Trim();
    public string ArtistValue => ArtistBox.Text.Trim();
    public string AlbumValue => AlbumBox.Text.Trim();
    public bool WriteTag => WriteTagCheck.IsChecked == true;

    public EditSongDialog(string filePath, string title, string artist, string album)
    {
        InitializeComponent();
        FileNameText.Text = Path.GetFileName(filePath);
        TitleBox.Text = title;
        ArtistBox.Text = artist;
        AlbumBox.Text = album;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
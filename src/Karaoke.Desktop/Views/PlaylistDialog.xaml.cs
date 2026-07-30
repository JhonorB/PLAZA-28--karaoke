using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Karaoke.Desktop.Views
{
    public partial class PlaylistDialog : Window
    {
        public string PlaylistTitle { get; private set; } = string.Empty;
        public string PlaylistSubtitle { get; private set; } = string.Empty;
        public string PlaylistImagePath { get; private set; } = string.Empty;

        public PlaylistDialog(string currentTitle = "", string currentSubtitle = "", string currentImagePath = "")
        {
            InitializeComponent();
            
            TxtTitle.Text = currentTitle;
            TxtSubtitle.Text = currentSubtitle;
            PlaylistImagePath = currentImagePath;
            
            if (!string.IsNullOrEmpty(currentImagePath))
            {
                try
                {
                    ImgPreview.Source = new BitmapImage(new Uri(currentImagePath, UriKind.RelativeOrAbsolute));
                }
                catch { }
            }
        }

        private void OnSelectImageClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Seleccionar Portada de Playlist",
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.webp;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                PlaylistImagePath = openFileDialog.FileName;
                try
                {
                    ImgPreview.Source = new BitmapImage(new Uri(PlaylistImagePath, UriKind.Absolute));
                }
                catch { }
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                MessageBox.Show("Por favor, ingresa un nombre para la Playlist.", "Nombre Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PlaylistTitle = TxtTitle.Text.Trim();
            PlaylistSubtitle = TxtSubtitle.Text.Trim();
            
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

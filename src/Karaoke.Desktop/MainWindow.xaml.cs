using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Threading;
using Karaoke.Desktop.ViewModels;

namespace Karaoke.Desktop;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private DispatcherTimer? _syncTimer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F11)
            {
                if (_viewModel != null)
                {
                    _viewModel.IsVideoPopupOpen = !_viewModel.IsVideoPopupOpen;
                }
                else
                {
                    ToggleFullScreen();
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (_viewModel != null && _viewModel.IsVideoPopupOpen)
                {
                    _viewModel.IsVideoPopupOpen = false;
                    e.Handled = true;
                }
                else if (_isFullScreen)
                {
                    ToggleFullScreen();
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Right && _viewModel != null && !(e.OriginalSource is TextBox))
            {
                _viewModel.NextSong();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Left && _viewModel != null && !(e.OriginalSource is TextBox))
            {
                _viewModel.PreviousSong();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Space && _viewModel != null && !(e.OriginalSource is TextBox))
            {
                _viewModel.TogglePlayPause();
                e.Handled = true;
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        
        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _syncTimer.Tick += (s, ev) =>
        {
            if (_viewModel != null && _viewModel.IsPlaying && AudioPlayer.Source != null && AudioPlayer.NaturalDuration.HasTimeSpan)
            {
                var realPos = AudioPlayer.Position;
                _viewModel.SyncPositionFromMediaElement(realPos);
            }
        };
        _syncTimer.Start();
        

        AudioPlayer.Volume = _viewModel.Volume;
        AudioPlayer.MediaEnded += (s, ev) =>
        {
            if (_viewModel != null)
            {
                if (_viewModel.IsLooping)
                {
                    AudioPlayer.Position = TimeSpan.Zero;
                    AudioPlayer.Play();
                }
                else
                {
                    _viewModel.NextSong();
                }
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        if (e.PropertyName == nameof(MainViewModel.SelectedSong))
        {
            var song = _viewModel.SelectedSong;
            if (song != null && !string.IsNullOrEmpty(song.AudioFilePath))
            {
                if (File.Exists(song.AudioFilePath))
                {
                    try
                    {
                        AudioPlayer.Source = new Uri(song.AudioFilePath);
                        if (_viewModel.IsPlaying)
                        {
                            AudioPlayer.Play();
                        }
                    }
                    catch { }
                }
                else if (song.AudioFilePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    _ = LoadAndPlayYouTubeAudioAsync(song);
                }
                else
                {
                    AudioPlayer.Stop();
                    AudioPlayer.Source = null;
                }
            }
            else
            {
                AudioPlayer.Stop();
                AudioPlayer.Source = null;
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsPlaying))
        {
            if (_viewModel.IsPlaying)
            {
                if (AudioPlayer.Source != null)
                {
                    AudioPlayer.Play();
                }
            }
            else
            {
                if (AudioPlayer.Source != null)
                {
                    if (_viewModel.CurrentPositionSeconds <= 0.1)
                    {
                        AudioPlayer.Stop();
                    }
                    else
                    {
                        AudioPlayer.Pause();
                    }
                }
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.Volume) || e.PropertyName == nameof(MainViewModel.IsMuted))
        {
            AudioPlayer.Volume = _viewModel.IsMuted ? 0 : _viewModel.Volume;
        }
        else if (e.PropertyName == nameof(MainViewModel.IsVideoPopupOpen))
        {
            if (_viewModel.IsVideoPopupOpen && !_isFullScreen)
            {
                ToggleFullScreen();
            }
            else if (!_viewModel.IsVideoPopupOpen && _isFullScreen)
            {
                ToggleFullScreen();
            }
        }
    }

    private void OnSeekSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.SeekCommand.Execute(SeekSlider.Value);
            if (AudioPlayer.Source != null)
            {
                AudioPlayer.Position = TimeSpan.FromSeconds(SeekSlider.Value);
            }
        }
    }

    private async Task LoadAndPlayYouTubeAudioAsync(Karaoke.Core.Models.Song song)
    {
        try
        {
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = $"⚡ Conectando al stream en vivo de YouTube para: {song.Title}...";
            }
            var streamUrl = await _viewModel!.GetYouTubeStreamUrlAsync(song.AudioFilePath);
            if (!string.IsNullOrEmpty(streamUrl) && streamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                AudioPlayer.Source = new Uri(streamUrl);
                if (_viewModel != null && _viewModel.IsPlaying)
                {
                    AudioPlayer.Play();
                }
                if (_viewModel != null)
                {
                    _viewModel.StatusMessage = $"🔊 Transmitiendo audio de YouTube: {song.DisplayName}";
                }
            }
        }
        catch (Exception ex)
        {
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = $"❌ Error al transmitir audio de YouTube: {ex.Message}";
            }
        }
    }

    private bool _isFullScreen = false;

    private Window? _popUpStageWindow;

    private void OnOpenPopUpStageClick(object sender, RoutedEventArgs e)
    {
        if (_popUpStageWindow != null && _popUpStageWindow.IsLoaded)
        {
            _popUpStageWindow.Focus();
            return;
        }

        _popUpStageWindow = new Window
        {
            Title = "Karaoke Pro - Escenario Emergente (TV / Proyector)",
            Width = 920,
            Height = 620,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(8, 8, 18)),
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var grid = new Grid();
        
        var img = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.UniformToFill,
            Opacity = 0.7
        };
        img.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("BackgroundImagePath"));
        grid.Children.Add(img);

        var overlay = new System.Windows.Shapes.Rectangle
        {
            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 5, 5, 12))
        };
        grid.Children.Add(overlay);

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(40, 0, 40, 0)
        };

        var lyricsControl = new Karaoke.Desktop.Controls.KaraokeLyricsControl
        {
            Margin = new Thickness(0, 0, 0, 32)
        };
        lyricsControl.SetBinding(Karaoke.Desktop.Controls.KaraokeLyricsControl.TextProperty, new System.Windows.Data.Binding("CurrentLyricText"));
        lyricsControl.SetBinding(Karaoke.Desktop.Controls.KaraokeLyricsControl.ProgressProperty, new System.Windows.Data.Binding("LyricProgress"));
        stack.Children.Add(lyricsControl);

        var nextText = new TextBlock
        {
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 190)),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        nextText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("NextLyricText"));
        nextText.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Colors.Black, BlurRadius = 10, ShadowDepth = 3, Opacity = 1.0 };
        stack.Children.Add(nextText);

        grid.Children.Add(stack);

        _popUpStageWindow.Content = grid;
        _popUpStageWindow.DataContext = this.DataContext;
        _popUpStageWindow.KeyDown += (s, ev) =>
        {
            if (ev.Key == System.Windows.Input.Key.F11)
            {
                if (_popUpStageWindow.WindowStyle == WindowStyle.None)
                {
                    _popUpStageWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    _popUpStageWindow.WindowState = WindowState.Normal;
                }
                else
                {
                    _popUpStageWindow.WindowStyle = WindowStyle.None;
                    _popUpStageWindow.WindowState = WindowState.Maximized;
                }
            }
            else if (ev.Key == System.Windows.Input.Key.Escape)
            {
                _popUpStageWindow.Close();
            }
        };

        _popUpStageWindow.Show();
    }

    private void OnToggleFullScreenClick(object sender, RoutedEventArgs e)
    {
        ToggleFullScreen();
    }

    private void ToggleFullScreen()
    {
        _isFullScreen = !_isFullScreen;
        if (_isFullScreen)
        {
            SidebarBorder.Visibility = Visibility.Collapsed;
            TransportBorder.Visibility = Visibility.Collapsed;
            StatusBorder.Visibility = Visibility.Collapsed;
            LeftColumn.Width = new GridLength(0);
            CenterColumn.Width = new GridLength(0);
            RightColumn.Width = new GridLength(1, GridUnitType.Star);
            StageBorder.Margin = new Thickness(0);
            StageBorder.BorderThickness = new Thickness(0);
            
            if (BtnPopUp != null) BtnPopUp.Visibility = Visibility.Collapsed;
            
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
        else
        {
            SidebarBorder.Visibility = Visibility.Visible;
            TransportBorder.Visibility = Visibility.Visible;
            StatusBorder.Visibility = Visibility.Visible;
            LeftColumn.Width = new GridLength(250);
            CenterColumn.Width = new GridLength(1, GridUnitType.Star);
            RightColumn.Width = new GridLength(400);
            StageBorder.Margin = new Thickness(0);
            StageBorder.BorderThickness = new Thickness(1, 0, 0, 0);
            
            if (BtnPopUp != null) BtnPopUp.Visibility = Visibility.Visible;
            
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
        }
    }
}

public class InverseBooleanToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b && b)
            return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
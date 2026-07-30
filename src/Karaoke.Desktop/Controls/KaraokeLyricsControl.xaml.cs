using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Karaoke.Desktop.Controls;

public partial class KaraokeLyricsControl : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(KaraokeLyricsControl),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(KaraokeLyricsControl),
            new PropertyMetadata(0.0, OnProgressChanged));

    private static readonly DependencyProperty AnimatedProgressProperty =
        DependencyProperty.Register("AnimatedProgress", typeof(double), typeof(KaraokeLyricsControl),
            new PropertyMetadata(0.0, OnAnimatedProgressChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public KaraokeLyricsControl()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KaraokeLyricsControl control)
        {
            var text = e.NewValue as string ?? string.Empty;
            if (control.BaseText != null) control.BaseText.Text = text;
            if (control.HighlightText != null) control.HighlightText.Text = text;
            
            // Al cambiar la letra, cancelar animaciones y reiniciar a 0 al instante
            control.BeginAnimation(AnimatedProgressProperty, null);
            control.SetValue(AnimatedProgressProperty, 0.0);
            control.UpdateClip();

            // Garantizar actualización tras el cálculo de layout de WPF para evitar parpadeos
            control.Dispatcher.InvokeAsync(() =>
            {
                control.BeginAnimation(AnimatedProgressProperty, null);
                if (control.Progress <= 0.01)
                {
                    control.SetValue(AnimatedProgressProperty, 0.0);
                }
                control.UpdateClip();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KaraokeLyricsControl control)
        {
            double newProg = (double)e.NewValue;
            double curProg = (double)control.GetValue(AnimatedProgressProperty);

            // Evitar efecto rayado/parpadeo: si retrocede (cambio a nueva frase), o salto brusco (> 25%), o inicio (<= 1%), aplicar salto instantáneo sin animar hacia atrás
            if (newProg <= curProg || Math.Abs(newProg - curProg) > 0.25 || newProg <= 0.01)
            {
                control.BeginAnimation(AnimatedProgressProperty, null);
                control.SetValue(AnimatedProgressProperty, Math.Clamp(newProg, 0.0, 1.0));
                control.UpdateClip();
                return;
            }

            // Suavizado corto sólo en avance progresivo normal (hacia adelante)
            var animation = new DoubleAnimation
            {
                From = curProg,
                To = Math.Clamp(newProg, 0.0, 1.0),
                Duration = TimeSpan.FromMilliseconds(35)
            };
            control.BeginAnimation(AnimatedProgressProperty, animation);
        }
    }

    private static void OnAnimatedProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KaraokeLyricsControl control)
        {
            control.UpdateClip();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateClip();
    }

    private void UpdateClip()
    {
        if (HighlightText == null || BaseText == null) return;

        double animProg = (double)GetValue(AnimatedProgressProperty);
        double width = HighlightText.ActualWidth * Math.Clamp(animProg, 0.0, 1.0);
        HighlightText.Clip = new RectangleGeometry(new Rect(0, 0, width, HighlightText.ActualHeight * 2));
    }
}

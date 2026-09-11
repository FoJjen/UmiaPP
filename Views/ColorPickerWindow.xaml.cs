using System.Windows;
using System.Windows.Media;

namespace UmiaPP.Views;

public partial class ColorPickerWindow : Window
{
    public Color SelectedColor { get; private set; }

    public ColorPickerWindow(Color initial)
    {
        InitializeComponent();
        SliderR.Value = initial.R;
        SliderG.Value = initial.G;
        SliderB.Value = initial.B;
        UpdateFromSliders();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateFromSliders();
    }

    private void UpdateFromSliders()
    {
        var c = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
        PreviewRect.Fill = new SolidColorBrush(c);
        RBox.Text = ((int)SliderR.Value).ToString();
        GBox.Text = ((int)SliderG.Value).ToString();
        BBox.Text = ((int)SliderB.Value).ToString();
        SelectedColor = c;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

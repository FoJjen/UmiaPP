using System.Windows;

namespace UmiaPP.Views;

public partial class InspectionPointDialog : Window
{
    public string PointTitle { get; set; } = string.Empty;
    public string PointDescription { get; set; } = string.Empty;

    public InspectionPointDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => TitleBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        PointTitle = TitleBox.Text?.Trim() ?? string.Empty;
        PointDescription = DescriptionBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(PointTitle))
        {
            MessageBox.Show("Ange en kontrollpunkt innan den sparas.", "Validering", MessageBoxButton.OK, MessageBoxImage.Warning);
            TitleBox.Focus();
            return;
        }

        DialogResult = true;
    }
}

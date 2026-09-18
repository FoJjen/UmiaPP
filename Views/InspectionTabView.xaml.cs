using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public partial class InspectionTabView : UserControl
{
    public InspectionTabView() => InitializeComponent();

    private void AddPoint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InspectionTabViewModel viewModel) return;

        var dialog = new InspectionPointDialog
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true) return;

        _ = viewModel.AddPointFromDialogAsync(dialog.PointTitle, dialog.PointDescription);
    }

    private void Templates_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not InspectionTemplateChoice choice) return;
        if (DataContext is not InspectionTabViewModel viewModel) return;
        Dispatcher.BeginInvoke(() => _ = viewModel.SaveTemplateAsync(choice), DispatcherPriority.Background);
    }
}

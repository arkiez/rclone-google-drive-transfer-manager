using System.Windows;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Views;

public partial class ConflictWindow : Window
{
    public IReadOnlyList<ConflictDecision> Decisions { get; } = Enum.GetValues<ConflictDecision>();

    public ConflictWindow(IReadOnlyList<ConflictItem> conflicts)
    {
        InitializeComponent();
        DataContext = this;
        ConflictsGrid.ItemsSource = conflicts;
        SummaryText.Text = $"{conflicts.Count} existing file{(conflicts.Count == 1 ? string.Empty : "s")} need a decision. Overwrite is selected by default.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}

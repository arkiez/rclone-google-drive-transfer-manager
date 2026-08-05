using System.Windows;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Views;

public partial class SyncPreviewWindow : Window
{
    public SyncPreviewWindow(IReadOnlyList<SyncChange> changes, IReadOnlyList<string> rawLines)
    {
        InitializeComponent();
        ChangesGrid.ItemsSource = changes;
        RawLinesList.ItemsSource = rawLines;
        var added = changes.Count(c => c.Action.Equals("Add", StringComparison.OrdinalIgnoreCase));
        var updated = changes.Count(c => c.Action.Equals("Update", StringComparison.OrdinalIgnoreCase));
        var deleted = changes.Count(c => c.Action.Equals("Delete", StringComparison.OrdinalIgnoreCase));
        SummaryText.Text = $"{added} added  •  {updated} updated  •  {deleted} deleted";
        DeleteSummaryText.Text = deleted == 0 ? "No destination deletions detected." : $"{deleted} destination deletion{(deleted == 1 ? string.Empty : "s")} detected.";
        ConfirmButton.Content = changes.Count == 0 ? "Run Sync" : "Apply Sync";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}

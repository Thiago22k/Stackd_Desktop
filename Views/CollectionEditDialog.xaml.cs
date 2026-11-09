using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stackd.Models;

namespace Stackd.Views;

/// <summary>Crear o editar una colección. Al editar ofrece también borrarla (botón secundario).</summary>
public sealed partial class CollectionEditDialog : ContentDialog
{
    public string CollectionName { get; private set; } = string.Empty;
    public string? CollectionDescription { get; private set; }

    public CollectionEditDialog(Collection? existing = null)
    {
        InitializeComponent();

        if (existing is not null)
        {
            Title = "Edit collection";
            PrimaryButtonText = "Save";
            SecondaryButtonText = "Delete";
            NameBox.Text = existing.Name;
            DescriptionBox.Text = existing.Description ?? string.Empty;
        }
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = "The name is required.";
            ErrorText.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        var description = DescriptionBox.Text.Trim();
        CollectionName = name;
        CollectionDescription = description.Length == 0 ? null : description;
    }
}

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using PicView.Core.Config;
using PicView.Core.ProcessHandling;

namespace PicView.Avalonia.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppVersion.Text = VersionHelper.GetCurrentVersion();

            KofiImage.PointerEntered += (_, _) =>
            {
                if (!TryGetResource("kofi_button_redDrawingImage", ThemeVariant.Default, out var redDrawing))
                {
                    return;
                }

                if (redDrawing is DrawingImage drawingImage)
                {
                    KofiImage.Source = drawingImage;
                }
            };
            KofiImage.PointerExited += (_, _) =>
            {
                if (!TryGetResource("kofi_button_strokeDrawingImage", ThemeVariant.Default, out var drawing))
                {
                    return;
                }

                if (drawing is DrawingImage drawingImage)
                {
                    KofiImage.Source = drawingImage;
                }
            };

            // TODO: replace with auto update service
            UpdateButton.Click += (_, _) =>
            {
                ProcessHelper.OpenLink("https://picview.org/avalonia-download");
            };
        };
    }
}
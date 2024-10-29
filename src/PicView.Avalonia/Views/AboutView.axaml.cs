using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using PicView.Avalonia.Update;
using PicView.Avalonia.ViewModels;
using PicView.Core.Config;

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
            UpdateButton.Click += async (_, _) =>
            {
                //Set loading and prevent user from interacting with UI
                ParentContainer.Opacity = .1;
                ParentContainer.IsHitTestVisible = false;
                SpinWaiter.IsVisible = true;
                try
                {
                    await UpdateManager.UpdateCurrentVersion(DataContext as MainViewModel);
                }
                finally
                {
                    SpinWaiter.IsVisible = false;
                    ParentContainer.IsHitTestVisible = true;
                    ParentContainer.Opacity = 1;
                }
            };
        };
    }
}
﻿using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.Resizing;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.ImageDecoding;
using PicView.Core.Localization;
using ReactiveUI;

namespace PicView.Avalonia.Views;

public partial class SingleImageResizeView : UserControl
{
    private double _aspectRatio;

    private IDisposable? _imageUpdateSubscription;
    
    private bool _isKeepingAspectRatio = true;

    public SingleImageResizeView()
    {
        InitializeComponent();
        Loaded += delegate
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            _aspectRatio = (double)vm.PixelWidth / vm.PixelHeight;

            SetIsQualitySliderEnabled();
            SaveButton.Click += async (_, _) => await SaveImage(vm).ConfigureAwait(false);
            SaveAsButton.Click += async (_, _) => await SaveImageAs(vm).ConfigureAwait(false);

            PixelWidthTextBox.KeyDown += async (_, e) => await SaveImageOnEnter(e);
            PixelHeightTextBox.KeyDown += async (_, e) => await SaveImageOnEnter(e);

            PixelWidthTextBox.KeyUp += delegate { AdjustAspectRatio(PixelWidthTextBox); };
            PixelHeightTextBox.KeyUp += delegate { AdjustAspectRatio(PixelHeightTextBox); };

            ConversionComboBox.SelectionChanged += delegate { SetIsQualitySliderEnabled(); };

            _imageUpdateSubscription = vm.WhenAnyValue(x => x.FileInfo).Select(x => x is not null).Subscribe(_ =>
            {
                Dispatcher.UIThread.Invoke(SetIsQualitySliderEnabled);
            });

            ResetButton.Click += (_, _) =>
            {
                PixelWidthTextBox.Text = vm.PixelWidth.ToString();
                PixelHeightTextBox.Text = vm.PixelHeight.ToString();
                if (vm.FileInfo.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    vm.FileInfo.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    vm.FileInfo.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    QualitySlider.IsEnabled = true;
                    var quality = ImageFunctionHelper.GetCompressionQuality(vm.FileInfo.FullName);
                    QualitySlider.Value = quality;
                }
                else
                {
                   QualitySlider.IsEnabled = false; 
                }
                ConversionComboBox.SelectedItem = NoConversion;
            };

            LinkChainButton.Click += (_, _) =>
            {
                if (_isKeepingAspectRatio)
                {
                    _isKeepingAspectRatio = false;
                    LinkChainImage.IsVisible = false;
                    UnlinkChainImage.IsVisible = true;
                }
                else
                {
                    _isKeepingAspectRatio = true;
                    LinkChainImage.IsVisible = true;
                    UnlinkChainImage.IsVisible = false;
                    
                    AdjustAspectRatio(PixelWidthTextBox);
                }
            };
        };

        Unloaded += delegate
        {
            _imageUpdateSubscription?.Dispose();
        };
    }

    private void AdjustAspectRatio(TextBox sender)
    {
        if (!_isKeepingAspectRatio)
        {
            return;
        }

        AspectRatioHelper.SetAspectRatioForTextBox(PixelWidthTextBox, PixelHeightTextBox, sender == PixelWidthTextBox,
            _aspectRatio, DataContext as MainViewModel);
    }

    private void SetIsQualitySliderEnabled()
    {
        if (DataContext is not MainViewModel vm) return;

        try
        {
            if (JpgItem.IsSelected || PngItem.IsSelected)
            {
                QualitySlider.IsEnabled = true;
                QualitySlider.Value = 75;
            }
            else if (vm.FileInfo.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     vm.FileInfo.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                     vm.FileInfo.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                QualitySlider.IsEnabled = true;
                var quality = ImageFunctionHelper.GetCompressionQuality(vm.FileInfo.FullName);
                QualitySlider.Value = quality;
            }
            else
            {
                QualitySlider.IsEnabled = false;
            }
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e);
#endif
        }
    }

    private async Task SaveImageOnEnter(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            await SaveImage(vm).ConfigureAwait(false);
        }
    }

    private async Task SaveImageAs(MainViewModel vm)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } provider)
        {
            return;
        }

        var options = new FilePickerSaveOptions
        {
            Title = $"{TranslationHelper.Translation.OpenFileDialog} - PicView",
            SuggestedFileName = vm.FileInfo.Name,
            SuggestedStartLocation =
                await desktop.MainWindow.StorageProvider.TryGetFolderFromPathAsync(vm.FileInfo.FullName),
        };
        var file = await provider.SaveFilePickerAsync(options);
        if (file is null)
        {
            return;
        }

        var destination = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? file.Path.AbsolutePath
            : file.Path.LocalPath;
        await DoSaveImage(vm, destination).ConfigureAwait(false);
    }

    private async Task SaveImage(MainViewModel vm)
    {
        await DoSaveImage(vm, vm.FileInfo.FullName).ConfigureAwait(false);
    }

    private async Task DoSaveImage(MainViewModel vm, string destination)
    {
        if (!uint.TryParse(PixelWidthTextBox.Text, out var width) ||
            !uint.TryParse(PixelHeightTextBox.Text, out var height))
        {
            return;
        }

        //Set loading and prevent user from interacting with UI
        ParentContainer.Opacity = .1;
        ParentContainer.IsHitTestVisible = false;
        SpinWaiter.IsVisible = true;

        var rotationAngle = 0; // TODO make a control for adjusting rotation

        var file = vm.FileInfo.FullName;
        var sameFile = file.Equals(destination, StringComparison.OrdinalIgnoreCase);
        var ext = vm.FileInfo.Extension;
        if (!NoConversion.IsSelected)
        {
            if (PngItem.IsSelected)
            {
                ext = ".png";
                destination = Path.ChangeExtension(destination, ".png");
            }
            else if (JpgItem.IsSelected)
            {
                ext = ".jpg";
                destination = Path.ChangeExtension(destination, ".jpg");
            }
            else if (WebpItem.IsSelected)
            {
                ext = ".webp";
                destination = Path.ChangeExtension(destination, ".webp");
            }
            else if (AvifItem.IsSelected)
            {
                ext = ".avif";
                destination = Path.ChangeExtension(destination, ".avif");
            }
            else if (HeicItem.IsSelected)
            {
                ext = ".heic";
                destination = Path.ChangeExtension(destination, ".heic");
            }
            else if (JxlItem.IsSelected)
            {
                ext = ".jxl";
                destination = Path.ChangeExtension(destination, ".jxl");
            }
        }

        uint? quality = null;
        if (QualitySlider.IsEnabled)
        {
            if (ext == ".jpg" || Path.GetExtension(destination) == ".jpg" || Path.GetExtension(destination) == ".jpeg")
            {
                quality = (uint)QualitySlider.Value;
            }
        }

        var success = await SaveImageFileHelper.SaveImageAsync(null,
            file,
            sameFile ? null : destination,
            width,
            height,
            quality,
            ext,
            rotationAngle,
            null,
            _isKeepingAspectRatio).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SpinWaiter.IsVisible = false;
            ParentContainer.IsHitTestVisible = true;
            ParentContainer.Opacity = 1;
        });
        if (!success)
        {
            await TooltipHelper.ShowTooltipMessageAsync(TranslationHelper.Translation.SavingFileFailed);
            return;
        }

        if (destination == file)
        {
            if (!NavigationHelper.CanNavigate(vm))
            {
                return;
            }

            if (vm.ImageIterator is not null)
            {
                await vm.ImageIterator.QuickReload().ConfigureAwait(false);
            }
        }
        else
        {
            if (Path.GetDirectoryName(file) == Path.GetDirectoryName(destination))
            {
                await NavigationHelper.LoadPicFromFile(destination, vm).ConfigureAwait(false);
            }
        }
    }

    ~SingleImageResizeView()
    {
        _imageUpdateSubscription?.Dispose();
    }
}
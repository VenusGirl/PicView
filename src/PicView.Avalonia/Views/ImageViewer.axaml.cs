using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PicView.Avalonia.ViewModels;
using PicView.Core.Config;
using PicView.Core.Navigation;
using System.Runtime.InteropServices;
using Avalonia.Controls.Primitives;
using PicView.Core.ImageTransformations;
using Point = Avalonia.Point;
using Avalonia.Svg.Skia;
using PicView.Avalonia.Helpers;
using PicView.Avalonia.Navigation;

namespace PicView.Avalonia.Views;

public partial class ImageViewer : UserControl
{
    private static ScaleTransform? _scaleTransform;
    private static TranslateTransform? _translateTransform;

    private static Point _start;
    private static Point _origin;

    private bool _captured;
    private bool _isZoomed;
    private double _zoomPercentage = 100; 

    public ImageViewer()
    {
        InitializeComponent();
        AddHandler(PointerWheelChangedEvent, PreviewOnPointerWheelChanged, RoutingStrategies.Tunnel);
        AddHandler(Gestures.PointerTouchPadGestureMagnifyEvent, TouchMagnifyEvent, RoutingStrategies.Bubble);

        Loaded += delegate
        {
            InitializeZoom();
            LostFocus += (s, e) =>
            {
                _captured = false;
            };
        };
    }

    public void SetImage(object image, ImageType imageType)
    {
        if (imageType is ImageType.Svg)
        {
            var svgSource = SvgSource.Load(image as string, null);
            if (Dispatcher.UIThread.CheckAccess())
            {
                MainImage.Source = new SvgImage { Source = svgSource };
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainImage.Source = new SvgImage { Source = svgSource };
                }, DispatcherPriority.Send);
            }

            return;
        }
        if (Dispatcher.UIThread.CheckAccess())
        {
            Set();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(Set);
        }

        return;

        void Set()
        {
            ImageHelper.SetImage(image, MainImage, imageType);
            Reset();
            if (SettingsHelper.Settings.Zoom.ScrollEnabled)
            {
                ImageScrollViewer.ScrollToHome();
            }
        }
    }

    private void TouchMagnifyEvent(object? sender, PointerDeltaEventArgs e)
    {
        ZoomTo(e.GetPosition(this), e.Delta.X > 0);
    }

    private async Task PreviewOnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        await Main_OnPointerWheelChanged(e);
    }
    
    private async Task Main_OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Use touch gestures for zooming on macOS
            return;
        }
        var ctrl = e.KeyModifiers == KeyModifiers.Control;
        var reverse = e.Delta.Y < 0;

        if (SettingsHelper.Settings.Zoom.CtrlZoom)
        {
            if (ctrl)
            {
                if (reverse)
                {
                    ZoomOut(e);
                }
                else
                {
                    ZoomIn(e);
                }
            }
            else
            {
                await ScrollOrNavigate();
            }
        }
        else
        {
            if (ctrl)
            {
                await ScrollOrNavigate();
            }
            else
            {
                if (reverse)
                {
                    ZoomOut(e);
                }
                else
                {
                    ZoomIn(e);
                }
            }
        }
        return;

        async Task ScrollOrNavigate()
        {
            if (!SettingsHelper.Settings.Zoom.ScrollEnabled || e.KeyModifiers == KeyModifiers.Shift)
            {
                await LoadNextPic();
            }
            else
            {
                if (ImageScrollViewer.VerticalScrollBarVisibility is ScrollBarVisibility.Visible or ScrollBarVisibility.Auto)
                {
                    if (reverse)
                    {
                        ImageScrollViewer.LineDown();
                    }
                    else
                    {
                        ImageScrollViewer.LineUp();
                    }
                }
                else
                {
                    await LoadNextPic();
                }
            }
        }

        async Task LoadNextPic()
        {
            var navigateTo = SettingsHelper.Settings.Zoom.HorizontalReverseScroll ? NavigateTo.Previous : NavigateTo.Next;
            if (reverse)
            {
                navigateTo = SettingsHelper.Settings.Zoom.HorizontalReverseScroll ? NavigateTo.Next : NavigateTo.Previous;
            }

            await mainViewModel.ImageIterator.LoadNextPic(navigateTo, mainViewModel).ConfigureAwait(false);
        }
    }

    #region Zoom

    private void InitializeZoom()
    {
        ImageZoomBorder.RenderTransform = new TransformGroup
        {
            Children =
            [
                new ScaleTransform(),
                new TranslateTransform()
            ]
        };
        _scaleTransform = (ScaleTransform)((TransformGroup)ImageZoomBorder.RenderTransform)
            .Children.First(tr => tr is ScaleTransform);

        _translateTransform = (TranslateTransform)((TransformGroup)ImageZoomBorder.RenderTransform)
            .Children.First(tr => tr is TranslateTransform);
        ImageZoomBorder.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
    }

    private void Capture(PointerEventArgs e)
    {
        _start = e.GetPosition(ImageZoomBorder);
        _origin = new Point(_translateTransform.X, _translateTransform.Y);
        _captured = true;
    }

    public void ZoomIn(PointerWheelEventArgs e)
    {
        ZoomTo(e.GetPosition(ImageScrollViewer), true);
    }

    public void ZoomOut(PointerWheelEventArgs e)
    {
        ZoomTo(e.GetPosition(ImageScrollViewer), false);
    }

    public void ZoomIn()
    {
        var point = new Point(Bounds.Width / 2, Bounds.Height / 2);
        ZoomTo(point, true);
    }

    public void ZoomOut()
    {
        var point = new Point(Bounds.Width / 2, Bounds.Height / 2);
        ZoomTo(point, false);
    }

    public void ZoomTo(Point point, bool isZoomIn)
    {
        var currentZoom = _scaleTransform.ScaleX;
        var zoomSpeed = SettingsHelper.Settings.Zoom.ZoomSpeed;

        switch (currentZoom)
        {
            // Increase speed based on the current zoom level
            case > 14 when isZoomIn:
                return;

            case > 4:
                zoomSpeed += 1.5;
                break;

            case > 3.2:
                zoomSpeed += 1;
                break;

            case > 1.6:
                zoomSpeed += 0.5;
                break;
        }

        if (!isZoomIn)
        {
            zoomSpeed = -zoomSpeed;
        }

        currentZoom += zoomSpeed;
        currentZoom = Math.Max(0.09, currentZoom); // Fix for zooming out too much
        if (SettingsHelper.Settings.Zoom.AvoidZoomingOut && currentZoom < 1.0)
        {
            ResetZoom(true);
        }
        else
        {
            ZoomTo(point, currentZoom, true);
        }
    }

    public void ZoomTo(Point point, double zoomValue, bool enableAnimations)
    {
        if (_scaleTransform == null || _translateTransform == null)
        {
            return;
        }

        if (enableAnimations)
        {
            _scaleTransform.Transitions ??=
            [
                new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromSeconds(.25) },
                new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromSeconds(.25) }
            ];
            _translateTransform.Transitions ??=
            [
                new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromSeconds(.25) },
                new DoubleTransition { Property = TranslateTransform.YProperty, Duration = TimeSpan.FromSeconds(.25) }
            ];
        }
        else
        {
            _scaleTransform.Transitions = null;
            _translateTransform.Transitions = null;
        }

        var absoluteX = point.X * _scaleTransform.ScaleX + _translateTransform.X;
        var absoluteY = point.Y * _scaleTransform.ScaleY + _translateTransform.Y;

        var newTranslateValueX = Math.Abs(zoomValue - 1) > .2 ? absoluteX - point.X * zoomValue : 0;
        var newTranslateValueY = Math.Abs(zoomValue - 1) > .2 ? absoluteY - point.Y * zoomValue : 0;
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _scaleTransform.ScaleX = zoomValue;
            _scaleTransform.ScaleY = zoomValue;
            _translateTransform.X = newTranslateValueX;
            _translateTransform.Y = newTranslateValueY;
        }, DispatcherPriority.Normal);
        _zoomPercentage = zoomValue * 100;
        _isZoomed = zoomValue != 0;
        if (_isZoomed)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }
            vm.ToolTipUIText = $"{Math.Floor(_zoomPercentage)}%";
        }
    }

    public void ResetZoom(bool enableAnimations)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (enableAnimations)
            {
                _scaleTransform.Transitions ??=
                [
                    new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromSeconds(.25) },
                    new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromSeconds(.25) }
                ];
                _translateTransform.Transitions ??=
                [
                    new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromSeconds(.25) },
                    new DoubleTransition { Property = TranslateTransform.YProperty, Duration = TimeSpan.FromSeconds(.25) }
                ];
            }
            else
            {
                _scaleTransform.Transitions = null;
                _translateTransform.Transitions = null;
            }

            _scaleTransform.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
        }, DispatcherPriority.Send);
        _zoomPercentage = 100;
        _isZoomed = false;
    }

    public void Reset()
    {
        if (_isZoomed)
        {
            ResetZoom(false);
        }
        if (Dispatcher.UIThread.CheckAccess())
        {
            ImageLayoutTransformControl.LayoutTransform = null;
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ImageLayoutTransformControl.LayoutTransform = null;
            });
        }

        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        vm.RotationAngle = 0;
        vm.ScaleX = 1;
    }
    
    /*public void PreparePanImage(object sender, PointerPressedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (!desktop.MainWindow.IsActive || !desktop.MainWindow.IsVisible || _scaleTransform == null)
        {
            return;
        }
        
        _start = e.GetPosition(MainImage);
        _origin = new Point(_translateTransform.X, _translateTransform.Y);
        _captured = true;
    }*/


    public void Pan(PointerEventArgs e, bool enableAnimations)
    {
        if (!_captured || _scaleTransform == null || !_isZoomed)
        {
            return;
        }

        var position = _start - e.GetPosition(this);
        
        var newXproperty = _origin.X - position.X;
        var newYproperty = _origin.Y - position.Y;

        Dispatcher.UIThread.Invoke(() =>
        {
            _translateTransform.X = newXproperty;
            _translateTransform.Y = newYproperty;
        });
        e.Handled = true;
        
        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        vm.ToolTipUIText = $"{_translateTransform.X}, {_translateTransform.Y}";
    }

    #endregion Zoom

    #region Rotation and Flip

    public void Rotate(bool clockWise, bool animate)
    {
        if (DataContext is not MainViewModel vm)
            return;
        if (MainImage.Source is null)
        {
            return;
        }
        if (RotationHelper.IsValidRotation(vm.RotationAngle))
        {
            var nextAngle = RotationHelper.Rotate(vm.RotationAngle, clockWise);
            vm.RotationAngle = nextAngle switch
            {
                360 => 0,
                -90 => 270,
                _ => nextAngle
            };
        }
        else
        {
            vm.RotationAngle = RotationHelper.NextRotationAngle(vm.RotationAngle, true);
        }

        var rotateTransform = new RotateTransform(vm.RotationAngle);

        if (animate)
        {
            rotateTransform.Transitions ??=
            [
                new DoubleTransition { Property = RotateTransform.AngleProperty, Duration = TimeSpan.FromSeconds(.5) },
                new DoubleTransition { Property = RotateTransform.CenterXProperty, Duration = TimeSpan.FromSeconds(.5) },
                new DoubleTransition { Property = RotateTransform.CenterYProperty, Duration = TimeSpan.FromSeconds(.5) }
            ];
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ImageLayoutTransformControl.LayoutTransform = rotateTransform;
        });
        WindowHelper.SetSize(vm);
    }

    public void Flip(bool animate)
    {
        if (DataContext is not MainViewModel vm)
            return;
        if (MainImage.Source is null)
        {
            return;
        }
        vm.ScaleX = vm.ScaleX == -1 ? 1 : -1;
        if (vm.ScaleX == 1)
        {
            vm.ScaleX = -1;
            vm.GetFlipped = vm.UnFlip;
        }
        else
        {
            vm.ScaleX = 1;
            vm.GetFlipped = vm.Flip;
        }
        var flipTransform = new ScaleTransform(vm.ScaleX, 1);
        if (animate)
        {
            flipTransform.Transitions ??=
            [
                new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromSeconds(.3) },
            ];
        }
        //_scaleTransform.ScaleX = vm.ScaleX;
        MainImage.RenderTransform = flipTransform;
    }

    #endregion Rotation and Flip

    #region Events

    private void ImageScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        e.Handled = true;
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ResetZoom(true);
        }
    }

    private void ImageZoomBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.ClickCount == 2)
        {
            ResetZoom(true);
        }
        else
        {
            Pressed(e);
        }
    }

    private void ImageZoomBorder_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        Pan(e, false);
    }

    private void Pressed(PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Capture(e);
    }

    private void ImageZoomBorder_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_captured)
        {
            return;
        }

        _captured = false;
    }

    #endregion Events
}
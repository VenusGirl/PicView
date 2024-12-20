﻿using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.Views.UC;
using PicView.Core.Config;

namespace PicView.Avalonia.CustomControls;

[TemplatePart("PART_ScrollViewer", typeof(AutoScrollViewer))]
public class GalleryListBox : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    private AutoScrollViewer? _autoScrollViewer;
    
    public GalleryListBox()
    {
        AddHandler(PointerPressedEvent, PreviewPointerPressedEvent, RoutingStrategies.Tunnel);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _autoScrollViewer = e.NameScope.Find<AutoScrollViewer>("PART_ScrollViewer");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Prevent control from hijacking keys
        e.Handled = false;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // Prevent control from hijacking keys
        e.Handled = false;
    }

    #region Functions

    public IEnumerable<Control?> GetVisibleItems()
    {
        return Items.Cast<Control?>().Where(IsControlVisible);
    }
    
    private bool IsControlVisible(Control? child)
    {
        if (child is null)
        {
            return false;
        }
        try
        {
            var parentBounds = new Rect(Bounds.Size);
            var visual = child.TransformToVisual(this);
            if (visual is null)
            {
                return false;
            }
            var childBounds = child.Bounds.TransformToAABB(visual.Value);
            return parentBounds.Intersects(childBounds);
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine($"IsControlVisible exception:\n{e.Message}");
#endif
            return false;
        }
    }

    public void ScrollToCenterOfItem(GalleryItem galleryItem)
    {
        if (GalleryFunctions.IsFullGalleryOpen)
        {
            ScrollIntoView(galleryItem);
            return;
        }
        var visibleItems = GetVisibleItems();
        
        var array = visibleItems as GalleryItem[] ?? visibleItems.ToArray();
        var visibleItemsCount = array.Length;
        if (visibleItemsCount == 0)
        {
            return;
        }
        
        var averageItemWidth = array.Sum(item => item.Bounds.Width);
        averageItemWidth /= visibleItemsCount;
        
        var selectedScrollTo = galleryItem.TranslatePoint(new Point(), ItemsPanelRoot);
        
        if (!selectedScrollTo.HasValue)
        {
            return;
        }

        // ReSharper disable once PossibleLossOfFraction
        var x = selectedScrollTo.Value.X - (visibleItemsCount + 1) / 2 * averageItemWidth + averageItemWidth / 2;
        
        _autoScrollViewer.Offset = new Vector(x, _autoScrollViewer.Offset.Y);
    }
    
    #endregion

    private void PreviewPointerPressedEvent(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Disable right click selection
        e.Handled = true;
    }
    
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        e.Handled = true;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS already has horizontal scrolling for touchpad
            return;
        }

        const int speed = 64;

        if (e.Delta.Y > 0)
        {
            if (SettingsHelper.Settings.Zoom.HorizontalReverseScroll)
            {
                _autoScrollViewer.Offset -= new Vector(speed, speed);
            }
            else
            {
                _autoScrollViewer.Offset -= new Vector(-speed, -speed);
            }
        }
        else
        {
            if (SettingsHelper.Settings.Zoom.HorizontalReverseScroll)
            {
                _autoScrollViewer.Offset -= new Vector(-speed, -speed);
            }
            else
            {
                _autoScrollViewer.Offset -= new Vector(speed, speed);
            }
        }
    }

    public void ScrollToEnd()
    {
        _autoScrollViewer.Offset = new Vector(double.PositiveInfinity, double.PositiveInfinity);
        _autoScrollViewer.ScrollToEnd();
    }
    
    public void ScrollToHome()
    {
        _autoScrollViewer.Offset = new Vector(double.NegativeInfinity, double.NegativeInfinity);
        _autoScrollViewer.ScrollToHome();
    }

    public void PageLeft()
    {
        _autoScrollViewer.PageLeft();
    }
    public void PageRight()
    {
        _autoScrollViewer.PageRight();
    }
}

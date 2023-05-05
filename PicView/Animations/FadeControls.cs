﻿using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PicView.ChangeImage;
using PicView.PicGallery;
using PicView.Properties;
using PicView.UILogic;
using PicView.UILogic.TransformImage;
using static PicView.UILogic.UC;
using Timer = System.Timers.Timer;

namespace PicView.Animations
{
    internal static class FadeControls
    {
        /// <summary>
        /// Timer used to hide interface and/or scrollbar
        /// </summary>
        internal static Timer? ActivityTimer { get; set; }

        /// <summary>
        /// Hides/shows interface elements with a fade animation
        /// </summary>
        /// <param name="show"></param>
        internal static void Fade(bool show)
        {
            if (Settings.Default.ShowInterface && Settings.Default.Fullscreen == false
                || GetClickArrowRight == null
                || GetClickArrowLeft == null
                || Getx2 == null
                || GetGalleryShortcut == null
                || Scroll.IsAutoScrolling
                || GalleryFunctions.IsHorizontalFullscreenOpen
                || GalleryFunctions.IsHorizontalOpen)
            {
                return;
            }

            ConfigureWindows.GetMainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                if (GetCropppingTool is {IsVisible: true})
                {
                    return;
                }

                if (Settings.Default.ScrollEnabled && ConfigureWindows.GetMainWindow?.Scroller?.ScrollableHeight > 0)
                {
                    ScrollbarFade(show);
                }

                // Don't run, if already being animated || prevent lag
                if (Getx2.Opacity is > 0 and < 1)
                {
                    return;
                }

                if (Scroll.IsAutoScrolling)
                {
                    GetClickArrowLeft.Opacity =
                    GetClickArrowRight.Opacity =
                    GetGalleryShortcut.Opacity =
                    Getx2.Opacity =
                    GetMinus.Opacity = 0;
                    return;
                }

                var timespan = TimeSpan.FromSeconds(show ? .5 : 1);

                var opacity = show ? 1 : 0;

                AnimationHelper.Fade(GetClickArrowLeft, opacity, timespan);
                AnimationHelper.Fade(GetClickArrowRight, opacity, timespan);
                AnimationHelper.Fade(GetGalleryShortcut, opacity, timespan);
                AnimationHelper.Fade(Getx2, opacity, timespan);
                AnimationHelper.Fade(GetMinus, opacity, timespan);
                AnimationHelper.Fade(GetRestorebutton, opacity, timespan);
            }));
        }

        /// <summary>
        /// Find scrollbar and start fade animation
        /// </summary>
        /// <param name="show"></param>
        internal static void ScrollbarFade(bool show)
        {
            var s = ConfigureWindows.GetMainWindow?.Scroller?.Template?.FindName("PART_VerticalScrollBar", ConfigureWindows.GetMainWindow?.Scroller) as ScrollBar;

            if (show)
            {
                AnimationHelper.Fade(s, 1, TimeSpan.FromSeconds(.7));
            }
            else
            {
                AnimationHelper.Fade(s, 0, TimeSpan.FromSeconds(1));
            }
        }
    }
}
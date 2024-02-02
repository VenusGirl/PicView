﻿using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using PicView.Core.Config;

namespace PicView.Avalonia.Helpers;

public static class WindowHelper
{
    public static void InitializeWindowSizeAndPosition(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow.Position = new PixelPoint((int)SettingsHelper.Settings.WindowProperties.Top, (int)SettingsHelper.Settings.WindowProperties.Left);
        desktop.MainWindow.Width = SettingsHelper.Settings.WindowProperties.Width;
        desktop.MainWindow.Height = SettingsHelper.Settings.WindowProperties.Height;
        
        _ = SettingsHelper.SaveSettingsAsync();
    }

    public static void UpdateWindowPosToSettings()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        SettingsHelper.Settings.WindowProperties.Top = desktop.MainWindow.Position.X;
        SettingsHelper.Settings.WindowProperties.Left = desktop.MainWindow.Position.Y;
        SettingsHelper.Settings.WindowProperties.Width = desktop.MainWindow.Width;
        SettingsHelper.Settings.WindowProperties.Height = desktop.MainWindow.Height;

        _ = SettingsHelper.SaveSettingsAsync();
    }
}
﻿using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PicView.Core.FileHandling;
using PicView.Core.ImageDecoding;
using PicView.Core.Localization;

namespace PicView.Avalonia.Services;

public class FileService
{
    public static async Task<IStorageFile?> OpenFile()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow?.StorageProvider is not { } provider)
                throw new NullReferenceException("Missing StorageProvider instance.");
            var options = new FilePickerOpenOptions
            {
                Title = $"{TranslationHelper.GetTranslation("OpenFileDialog")} - PicView",
                AllowMultiple = false,
                FileTypeFilter = new[] { AllFileType, FilePickerFileTypes.ImageAll, ArchiveFileType }
            };
            IReadOnlyList<IStorageFile> files;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                files  = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                });
            }
            else
            {
                files = await provider.OpenFilePickerAsync(options);
            }

            return files?.Count >= 1 ? files[0] : null;
        }
        catch (Exception e)
        {
            // TODO write exception service to display error messages
        }

        return null;
    }

    private static FilePickerFileType AllFileType { get; } = new(TranslationHelper.GetTranslation("SupportedFiles"))
    {
        Patterns = SupportedFiles.ConvertFilesToGlobFormat(),
        AppleUniformTypeIdentifiers = new[] { "public.image" },
        MimeTypes = new[] { "image/*" },
    };

    private static FilePickerFileType ArchiveFileType { get; } = new(TranslationHelper.GetTranslation("SupportedFiles"))
    {
        Patterns = SupportedFiles.ConvertArchivesToGlobFormat(),
        AppleUniformTypeIdentifiers = new[] { "public.archive" },
        MimeTypes = new[] { "archive/*" }
    };

    public async Task SaveFileAsync(string fileName)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } provider)
            throw new NullReferenceException("Missing StorageProvider instance.");
        
        var options = new FilePickerSaveOptions()
        {
            Title = $"{TranslationHelper.GetTranslation("OpenFileDialog")} - PicView",
            FileTypeChoices  = new[] { AllFileType, FilePickerFileTypes.ImageAll, ArchiveFileType },
            SuggestedFileName = fileName,
            SuggestedStartLocation = await desktop.MainWindow.StorageProvider.TryGetFolderFromPathAsync(fileName)
            
        };
        IStorageFile? file;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            file  = await provider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
            });
        }
        else
        {
            file = await provider.SaveFilePickerAsync(options);
        }

        if (file is not null)
        {
            var path = file.Path.AbsolutePath;
            await SaveImageFileHelper.SaveImageAsync(null, fileName, path, null, null, null, Path.GetExtension(path));
        }
        else
        {
            // TODO save images that are not files
        }
    }
}
﻿using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for LanguageSettings.xaml
/// </summary>
public partial class LanguageSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;


    public LanguageSettings()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        LoadAiStatus();

        LoadWindowsLanguages();

        if (DefaultSettings.UseTesseract)
        {
            TesseractLanguagesStackPanel.Visibility = Visibility.Visible;
            await LoadTesseractContent();
        }
        else
        {
            TesseractLanguagesStackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadAiStatus()
    {
        if (OSInterop.IsWindows10())
        {
            StatusTextBlock.Text = "Not supported";
            ReasonTextBlock.Text = "Windows AI is not supported on Windows 10.";
            return;
        }

        // Check if the app is packaged and if the AI feature is supported
        if (!AppUtilities.IsPackaged())
        {
            StatusTextBlock.Text = "Not supported";
            ReasonTextBlock.Text = "Windows AI is only supported in packaged apps.";
            StoreLink.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
            if (readyState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
            {
                StatusTextBlock.Text = "Not supported";
                ReasonTextBlock.Text = "Windows AI is not supported on this system.";
                return;
            }
            else
            {
                StatusTextBlock.Text = "Ready";
                ReasonTextBlock.Text = "Windows AI is supported on this system.";
                return;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Failed to ready";
            ReasonTextBlock.Text = $"An error occurred while checking Windows AI support: {ex.Message}";
            return;
        }
    }

    private void LoadWindowsLanguages()
    {
        WindowsLanguagesListView.Items.Clear();
        List<Language> possibleOCRLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();
        foreach (Language language in possibleOCRLanguages)
            WindowsLanguagesListView.Items.Add(language);
    }

    private async Task LoadTesseractContent()
    {
        TesseractLanguagesListView.Items.Clear();
        List<ILanguage> tesseractLanguages = await TesseractHelper.TesseractLanguages();
        foreach (TessLang tessLang in tesseractLanguages.Cast<TessLang>())
        {
            string fileName = $"{tessLang.LanguageTag}.traineddata".PadRight(26);
            TesseractLanguagesListView.Items.Add($"{fileName}\t{tessLang.CultureDisplayName}");
        }

        AllLanguagesComboBox.Items.Clear();
        foreach (string textName in TesseractGitHubFileDownloader.tesseractTrainedDataFileNames)
        {
            string tesseractTag = textName.Split('.').First();

            TessLang tessLang = new(tesseractTag);
            string paddedTextName = textName.PadRight(26);
            AllLanguagesComboBox.Items.Add($"{paddedTextName}\t{tessLang.CultureDisplayName}");
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AllLanguagesComboBox.Text))
            return;

        string? pickedLanguageFile = AllLanguagesComboBox.Text.Split('\t', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(pickedLanguageFile))
            return;

        string tesseractPath = Path.GetDirectoryName(DefaultSettings.TesseractPath) ?? "c:\\";
        string tesseractFilePath = $"{tesseractPath}\\tessdata\\{pickedLanguageFile}";
        string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

        TesseractGitHubFileDownloader fileDownloader = new();
        await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
        await CopyFileWithElevatedPermissions(tempFilePath, tesseractFilePath);
        await LoadTesseractContent();
        File.Delete(tempFilePath);
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {

    }

    public async Task CopyFileWithElevatedPermissions(string sourcePath, string destinationPath)
    {
        string arguments = $"/c copy \"{sourcePath}\" \"{destinationPath}\"";
        ProcessStartInfo startInfo = new()
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = "cmd.exe",
            Verb = "runas",
            Arguments = arguments,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        // cannot redirect when UseShellExecute is true
        // cannot trigger UAC when UseShellExecute is false 🤷
        //startInfo.RedirectStandardError = true;
        //startInfo.RedirectStandardOutput = true;


        try
        {
            Process? process = Process.Start(startInfo);
            // string errors = process?.StandardError.ReadToEnd();
            // string output = process?.StandardOutput.ReadToEnd();
            if (process is not null)
                await process.WaitForExitAsync();

            // if (!string.IsNullOrEmpty(errors))
            //     ErrorsAndOutputText.Text += Environment.NewLine + errors;
            // 
            // if (!string.IsNullOrEmpty(output))
            //     ErrorsAndOutputText.Text += Environment.NewLine + output;
        }
        catch (Exception ex)
        {
            // The user refused the elevation.
            // Handle this situation as you prefer.
            MessageBox.Show(ex.Message);
        }
    }

    private void OpenPathButton_Click(object sender, RoutedEventArgs e)
    {
        string tesseractPath = Path.GetDirectoryName(DefaultSettings.TesseractPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tesseractPath))
            return;

        string tesseractFilePath = $"{tesseractPath}\\tessdata\\";

        Process.Start("explorer.exe", tesseractFilePath);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}

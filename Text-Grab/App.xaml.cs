﻿using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.ApplicationModel;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public NotifyIcon? TextGrabIcon { get; set; }

        public int NumberOfRunningInstances { get; set; } = 0;

        void appStartup(object sender, StartupEventArgs e)
        {
            NumberOfRunningInstances = Process.GetProcessesByName("Text-Grab").Length;

            if (IsPackaged())
                attemptToMSIXStartup();
            else
                attemptToSetRegistryStartup();

            // Register COM server and activator type
            bool handledArgument = false;

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                string argsInvoked = toastArgs.Argument;
                // Need to dispatch to UI thread if performing UI operations
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (String.IsNullOrWhiteSpace(argsInvoked) == false)
                    {
                        EditTextWindow mtw = new EditTextWindow(argsInvoked);
                        mtw.Show();
                        handledArgument = true;
                    }
                }));
            };

            if (Settings.Default.RunInTheBackground == true
                && NumberOfRunningInstances < 2)
                NotifyIconUtilities.SetupNotifyIcon();

            Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;

            for (int i = 0; i != e.Args.Length && !handledArgument; ++i)
            {
                Debug.WriteLine($"ARG {i}:{e.Args[i]}");
                if (e.Args[i].Contains("ToastActivated"))
                {
                    Debug.WriteLine("Launched from toast");
                    handledArgument = true;
                }
                else if (e.Args[i] == "Settings")
                {
                    SettingsWindow sw = new SettingsWindow();
                    sw.Show();
                    handledArgument = true;
                }
                else if (e.Args[i] == "GrabFrame")
                {
                    GrabFrame gf = new GrabFrame();
                    gf.Show();
                    handledArgument = true;
                }
                else if (e.Args[i] == "Fullscreen")
                {
                    WindowUtilities.LaunchFullScreenGrab();
                    handledArgument = true;
                }
                else if (e.Args[i] == "EditText")
                {
                    EditTextWindow manipulateTextWindow = new EditTextWindow();
                    manipulateTextWindow.Show();
                    handledArgument = true;
                }
                else if (File.Exists(e.Args[i]))
                {
                    EditTextWindow manipulateTextWindow = new EditTextWindow();
                    manipulateTextWindow.OpenThisPath(e.Args[i]);
                    manipulateTextWindow.Show();
                    handledArgument = true;
                }
            }

            if (!handledArgument)
            {
                if (Settings.Default.FirstRun)
                {
                    FirstRunWindow frw = new FirstRunWindow();
                    frw.Show();

                    Settings.Default.FirstRun = false;
                    Settings.Default.Save();
                }
                else
                {
                    switch (Settings.Default.DefaultLaunch)
                    {
                        case "Fullscreen":
                            WindowUtilities.LaunchFullScreenGrab();
                            break;
                        case "GrabFrame":
                            GrabFrame gf = new GrabFrame();
                            gf.Show();
                            break;
                        case "EditText":
                            EditTextWindow manipulateTextWindow = new EditTextWindow();
                            manipulateTextWindow.Show();
                            break;
                        default:
                            EditTextWindow editTextWindow = new EditTextWindow();
                            editTextWindow.Show();
                            break;
                    }
                }
            }
        }

        private void attemptToSetRegistryStartup()
        {
            string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            string? BaseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key is not null
                && BaseDir is not null)
            {
                key.SetValue("Text-Grab", $"\"{BaseDir}\\Text-Grab.exe\"");
            }
        }

        internal static bool IsPackaged()
        {
            try
            {
                // If we have a package ID then we are running in a packaged context
                var dummy = Package.Current.Id;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void attemptToMSIXStartup()
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");
            Debug.WriteLine("Startup is " + startupTask.State.ToString());

            StartupTaskState newState = await startupTask.RequestEnableAsync();

            // switch (startupTask.State)
            // {
            //     case StartupTaskState.Disabled:
            //         // Task is disabled but can be enabled.
            //         // StartupChkBox.Checked = false;
            //         break;
            //     case StartupTaskState.DisabledByUser:
            //         // Task is disabled and user must enable it manually.
            //         // StartupChkBox.Checked = false;
            //         // StartupChkBox.Enabled = false;

            //         // StartupChkBox.Text += "\nDisabled in Task Manager";
            //         break;
            //     case StartupTaskState.Enabled:
            //         // StartupChkBox.Checked = true;
            //         break;
            // }
        }

        private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // unhandled exceptions thrown from UI thread
            Debug.WriteLine($"Unhandled exception: {e.Exception}");
            e.Handled = true;
            Current.Shutdown();
        }
    }
}

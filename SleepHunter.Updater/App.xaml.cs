﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SleepHunter.Updater
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            // Invalid number of arguments, exit
            // Usage: Updater.exe <zip file> <install path>
            if (e.Args.Length != 2)
            {
                Shutdown();
                return;
            }

            var updateFilePath = e.Args[0];
            var installationPath = e.Args[1];

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            mainWindow.RetryRequested += async (sender, _) =>
            {
                mainWindow.ResetState();

                var retrySuccess = await PerformUpdate(mainWindow, updateFilePath, installationPath);
                if (!retrySuccess)
                    mainWindow.ToggleRetryButton(true);
                else
                    Shutdown();
            };

            mainWindow.Show();

            var success = await PerformUpdate(mainWindow, updateFilePath, installationPath);
            if (!success)
                mainWindow.ToggleRetryButton(true);
            else
                Shutdown();
        }

        private async Task<bool> PerformUpdate(MainWindow mainWindow, string updateFilePath, string installationPath)
        {
            var executableFile = Path.Combine(installationPath, "SleepHunter.exe");

            // Check that the update file exists, show error if missing
            if (!File.Exists(updateFilePath))
            {
                mainWindow.SetStatusText("Unable to Update");
                mainWindow.SetErrorMessage("Missing update file.\nYou can try again within SleepHunter, or install it manually.");
                return false;
            }

            // Terminate any existing SleepHunter instances
            try
            {
                mainWindow.SetStatusText("Waiting for SleepHunter...");
                await Task.Delay(3000);
                TerminateAllAndWait("SleepHunter");
            }
            catch (Exception ex)
            {
                mainWindow.SetStatusText("Unable to Update");
                mainWindow.SetErrorMessage(ex.Message);
                return false;
            }

            // Try to update, and display an error if something goes wrong
            try
            {
                mainWindow.PerformAppUpdate(updateFilePath, installationPath);
            }
            catch (Exception ex)
            {
                mainWindow.SetStatusText("Unable to Update");
                mainWindow.SetErrorMessage(ex.Message);
                return false;
            }

            // Check that the executable exists, show error if missing
            if (!File.Exists(executableFile))
            {
                mainWindow.SetStatusText("Unable to Restart");
                mainWindow.SetErrorMessage("Missing SleepHunter executable in installation folder.\nYou may need to apply the update manually.");
                return false;
            }

            // Restart SleepHunter
            mainWindow.SetStatusText("Restarting SleepHunter...");
            try
            {
                Process.Start(executableFile);
                return true;
            }
            catch
            {
                mainWindow.SetStatusText("Unable to Restart");
                mainWindow.SetErrorMessage("Unable to restart SleepHunter automatically.\nYou can try launching it manually.");
                return false;
            }
        }

        private async void TerminateAllAndWait(string processName)
        {
            var matchingProcesses = Process.GetProcessesByName(processName);

            foreach (var process in matchingProcesses)
                process.Kill();

            while (matchingProcesses.Length > 0)
            {
                await Task.Delay(3000);
                matchingProcesses = Process.GetProcessesByName(processName);
            }
        }
    }
}

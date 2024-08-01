using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace Research_Arcade_Updater
{
    // Launcher State Enum

    enum LauncherState
    {
        idle,
        startingLauncher,
        closingLauncher,
        restartingLauncher,
        failed,
        checkingForUpdates,
        updatingLauncher,
        waitingOnInternet,
    }

    public partial class MainWindow : Window
    {
        // Send a key press
        [DllImport("User32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly string rootPath;
        private readonly string configPath;
        private readonly string launcherPath;

        private JObject config;

        private Process launcherProcess = null;

        private LauncherState _state;
        internal LauncherState State
        {
            get => _state;
            set
            {
                if (Application.Current != null && Application.Current.Dispatcher != null)
                    try {
                        Application.Current.Dispatcher.Invoke(() => {
                            if (_state == value) return;
                            _state = value;

                            switch (_state)
                            {
                                case LauncherState.idle:
                                    StatusText.Text = "Awaiting Instructions...";
                                    break;
                                case LauncherState.startingLauncher:
                                    StatusText.Text = "Starting Launcher...";
                                    break;
                                case LauncherState.closingLauncher:
                                    StatusText.Text = "Closing Launcher...";
                                    break;
                                case LauncherState.restartingLauncher:
                                    StatusText.Text = "Restarting Launcher...";
                                    break;
                                case LauncherState.failed:
                                    StatusText.Text = "Failed (Please contact IT for support)";
                                    break;
                                case LauncherState.checkingForUpdates:
                                    StatusText.Text = "Checking for updates...";
                                    break;
                                case LauncherState.updatingLauncher:
                                    StatusText.Text = "Updating Launcher...";
                                    break;
                                case LauncherState.waitingOnInternet:
                                    StatusText.Text = "Waiting for an internet connection...";
                                    break;
                                default:
                                    break;
                            }

                        });
                    } catch (TaskCanceledException) { }
            }
        }

        public MainWindow()
        {
            Closing += Window_Closing;

            InitializeComponent();

            // Setup Directories
            rootPath = Directory.GetCurrentDirectory();

            configPath = Path.Combine(rootPath, "config.json");
            launcherPath = Path.Combine(rootPath, "Launcher");

            // Create the Launcher directory if it does not exist
            if (!Directory.Exists(launcherPath))
                Directory.CreateDirectory(launcherPath);
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Check for an internet connection
            while (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                State = LauncherState.waitingOnInternet;

            // Initialize the update timer
            Task.Run(async () =>
            {
                CheckForUpdates();

                while (true)
                {
                    // Wait 1 hour before checking for updates again
                    await Task.Delay(60 * 60 * 1000);
                    CheckForUpdates();
                }
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Close the launcher
            CloseLauncher();
        }

        private void Launcher_Closing(object sender, CancelEventArgs e)
        {
            launcherProcess = null;
            State = LauncherState.restartingLauncher;

            // After 3 seconds start the launcher
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                StartLauncher();
            });
        }

        private void StartLauncher()
        {
            if (launcherProcess == null)
            {
                State = LauncherState.startingLauncher;

                launcherProcess = Process.Start(Path.Combine(launcherPath, "Research-Arcade-Launcher.exe"));

                // Check if the launcher process has quit
                Task.Run(async () =>
                {
                    while (!launcherProcess.HasExited)
                        await Task.Delay(1000);

                    Launcher_Closing(null, null);
                });

                // Bring the launcher to the front
                SetForegroundWindow(launcherProcess.MainWindowHandle);
            }
        }

        private void CloseLauncher()
        {
            if (launcherProcess != null)
            {
                State = LauncherState.closingLauncher;

                // Send the close key to the launcher
                keybd_event(69, 0, 0, 0);

                // Wait for the launcher to close
                launcherProcess.WaitForExit();
                launcherProcess = null;

                // Release the close key
                keybd_event(69, 0, 2, 0);
            }
        }

        private void CheckForUpdates()
        {
            State = LauncherState.checkingForUpdates;

            try
            {
                // Get the online version of the launcher
                WebClient webClient = new WebClient();
                
                // Load the config file
                if (!File.Exists(configPath))
                {
                    State = LauncherState.failed;
                    MessageBox.Show("Config file not found");
                    return;
                }

                config = JObject.Parse(File.ReadAllText(configPath));

                // Create the version file if it does not exist
                if (!File.Exists(Path.Combine(rootPath, config["VersionPath"].ToString())))
                    File.WriteAllText(Path.Combine(rootPath, config["VersionPath"].ToString()), "0.0.0");

                // Get the current version of the launcher
                Version currentVersion = new Version(File.ReadAllText(Path.Combine(rootPath, config["VersionPath"].ToString())));

                // Get the latest version of the launcher
                Version latestVersion = new Version(webClient.DownloadString(config["VersionURL"].ToString()));

                // Check if the launcher is up to date
                if (currentVersion.IsDifferentVersion(latestVersion))
                {
                    // Close the launcher
                    CloseLauncher();

                    // Update the launcher
                    UpdateLauncher();

                    // Update the version file
                    File.WriteAllText(Path.Combine(rootPath, config["VersionPath"].ToString()), latestVersion.ToString());

                    // Start the launcher
                    StartLauncher();

                    // After 3 seconds set the state to idle
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        State = LauncherState.idle;
                    });
                }
                else
                {
                    // Start the launcher
                    StartLauncher();

                    // After 3 seconds set the state to idle
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        State = LauncherState.idle;
                    });
                }
            } catch (Exception e)
            {
                State = LauncherState.failed;
                MessageBox.Show(e.Message);
            }
        }

        private void UpdateLauncher()
        {
            State = LauncherState.updatingLauncher;

            // Delete the old launcher files (except the Games folder)
            foreach (string file in Directory.GetFiles(launcherPath))
                if (Path.GetFileName(file) != "Games")
                    File.Delete(file);

            // Download the launcher
            WebClient webClient = new WebClient();
            webClient.DownloadFile(config["LauncherURL"].ToString(), Path.Combine(launcherPath, "Launcher.zip"));

            // Extract the launcher
            FastZip fastZip = new FastZip();
            fastZip.ExtractZip(Path.Combine(launcherPath, "Launcher.zip"), launcherPath, null);

            // Delete the zip file
            File.Delete(Path.Combine(launcherPath, "Launcher.zip"));
        }
    }

    struct Version
    {
        // Zero value for the Version struct
        internal static Version zero = new Version(0, 0, 0);

        public int major;
        public int minor;
        public int subMinor;

        internal Version(short _major, short _minor, short _subMinor)
        {
            // Initialize the version number
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }

        internal Version(string version)
        {
            string[] parts = version.Split('.');

            // Reset the version number if it is not in the correct format
            if (parts.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            // Parse the version number
            major = int.Parse(parts[0]);
            minor = int.Parse(parts[1]);
            subMinor = int.Parse(parts[2]);
        }

        internal bool IsDifferentVersion(Version _otherVersion)
        {
            // Compare each part of the version number
            if (major != _otherVersion.major)
                return true;
            else if (minor != _otherVersion.minor)
                return true;
            else if (subMinor != _otherVersion.subMinor)
                return true;
            else return false;
        }

        public override string ToString()
        {
            // Return the version number as a string
            return $"{major}.{minor}.{subMinor}";
        }
    }
}

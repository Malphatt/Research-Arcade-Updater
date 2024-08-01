using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Research_Arcade_Updater
{
    // Launcher State Enum

    enum LauncherState
    {
        startingLauncher,
        closingLauncher,
        failed,
        checkingForUpdates,
        updatingLauncher,
        waitingOnInternet,
    }

    public partial class MainWindow : Window
    {
        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly string rootPath;
        private readonly string configPath;
        private readonly string launcherPath;

        private Process launcherProcess = null;

        private LauncherState _state;
        internal LauncherState State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                _state = value;

                switch (_state)
                {
                    case LauncherState.startingLauncher:
                        StatusText.Text = "Starting Launcher...";
                        break;
                    case LauncherState.closingLauncher:
                        StatusText.Text = "Closing Launcher...";
                        break;
                    case LauncherState.failed:
                        StatusText.Text = "Failed (Please contact IT for support)";
                        break;
                    case LauncherState.checkingForUpdates:
                        StatusText.Text = "Checking for updates...";
                        break;
                    case LauncherState.waitingOnInternet:
                        StatusText.Text = "Waiting for an internet connection...";
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Setup Directories
            rootPath = Directory.GetCurrentDirectory();

            configPath = Path.Combine(rootPath, "config.json");
            launcherPath = Path.Combine(rootPath, "Launcher");

            // Create the Launcher directory if it does not exist
            if (!Directory.Exists(launcherPath))
                Directory.CreateDirectory(launcherPath);

            // Check for an internet connection
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                State = LauncherState.waitingOnInternet;
                return;
            }
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

        private void CheckForUpdates()
        {
            // Check for updates
            State = LauncherState.checkingForUpdates;

            // Get the current version of the launcher
            Version currentVersion = new Version("0.0.0");

            // Get the latest version of the launcher
            Version latestVersion = new Version("0.0.0");

            // Check if the launcher is up to date
            if (currentVersion.IsDifferentVersion(latestVersion))
            {
                // Close the launcher
                if (launcherProcess != null)
                {
                    State = LauncherState.closingLauncher;
                    launcherProcess.Kill();
                    launcherProcess.WaitForExit();

                    launcherProcess = null;
                }

                // Update the launcher
                State = LauncherState.updatingLauncher;
                UpdateLauncher();
            }
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

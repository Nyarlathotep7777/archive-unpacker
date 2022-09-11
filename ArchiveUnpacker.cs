using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;

namespace ArchiveUnpacker
{
    partial class PopUp : Form
    {
        private System.Windows.Forms.TextBox textBox1;
        public PopUp(string title, string message)
        {
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.Text = message;
            this.textBox1.AutoSize = true;
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox1.Enabled = false;
            this.textBox1.Multiline = false;
            this.textBox1.ReadOnly = true;
            //this.textBox1.Padding = new System.Windows.Forms.Padding(5);

            // 
            // Form1
            // 
            this.Controls.Add(this.textBox1);
            this.Text = title;
            this.ResumeLayout(false);
            this.PerformLayout();

            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.ControlBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.Padding = new System.Windows.Forms.Padding(5);

            this.PerformAutoScale();

            //this.MinimumSize = new System.Windows.Size(0, 0);

            this.StartPosition = FormStartPosition.CenterScreen;
        }
    }

    public class ArchiveUnpacker : GenericPlugin
    {
        IPlayniteAPI myApi;
        private static readonly ILogger logger = LogManager.GetLogger();

        private ArchiveUnpackerSettingsViewModel settings { get; set; }

        private void ExtractFile(string source, string destination)
        {
            //string zPath = @"C:\Program Files\7-Zip\7z.exe";
            string zPath = settings.Settings.OptionZPath;
            if (!File.Exists(zPath))
                return;

            // If the directory doesn't exist, create it.
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            // change the path and give yours 
            try
            {
                ProcessStartInfo pro = new ProcessStartInfo();
                pro.WindowStyle = ProcessWindowStyle.Hidden;
                pro.FileName = zPath;
                pro.Arguments = "x \"" + source + "\" -o\"" + destination + "\"";
                //MessageBox.Show(pro.Arguments, pro.FileName);

                Process x = Process.Start(pro);
                x.WaitForExit();
            }
            catch (System.Exception Ex)
            {
                //DO logic here 
            }
        }

        public static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size;
        }

        private void FreeCache()
        {
            string destPath = settings.Settings.OptionDestPath;

            // If the directory doesn't exist, exit
            if (!Directory.Exists(destPath))
                return;

            // Sort sub folders by last access time
            IEnumerable<string> folders = Directory.EnumerateDirectories(destPath).OrderBy(folder => Directory.GetLastWriteTime(folder));

            // Print folder list
            StreamWriter sourceFile = File.CreateText(destPath + @"orderedList.txt");
            foreach (string folder in folders)
            {
                long d = DirSize(new DirectoryInfo(destPath));
                if (d > (long)1024 * 1024 * 1024 * settings.Settings.OptionSize)
                {
                    sourceFile.WriteLine(d + " bigger than limit " + (long)1024 * 1024 * 1024 * settings.Settings.OptionSize);
                    DialogResult dr = DialogResult.OK;
                    if (settings.Settings.OptionConfirmDelete == true)
                    {
                        string files = "";
                        foreach (string file in Directory.EnumerateFiles(folder))
                        {
                            files += file + System.Environment.NewLine;
                        }

                        dr = System.Windows.Forms.MessageBox.Show(files, "Removing cache folder", MessageBoxButtons.OKCancel);
                    }
                    if (dr == DialogResult.OK)
                    {
                        Directory.Delete(folder, true);
                        sourceFile.WriteLine(folder + " removed");
                    }
                    else
                    {
                        sourceFile.WriteLine(folder + " kept");
                    }
                }
                else
                {
                    sourceFile.WriteLine(folder + " kept");
                }
            }
            sourceFile.WriteLine(DirSize(new DirectoryInfo(destPath)) + " bytes remaining in cache");
            sourceFile.Close();
        }

        public override Guid Id { get; } = Guid.Parse("a2e681de-0cbd-4ee1-9a6c-62a2e4816d9f");

        public ArchiveUnpacker(IPlayniteAPI api) : base(api)
        {
            settings = new ArchiveUnpackerSettingsViewModel(this);
            myApi = api;
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
            IGameDatabase database = myApi.Database;
            GameAction action = args.SourceAction;

            Guid guid = action.EmulatorId;
            string profileId = action.EmulatorProfileId;

            string emulatorName = database.Emulators.Get(guid).Name;
            string profileName = database.Emulators.Get(guid).GetProfile(profileId).Name;
            //MessageBox.Show(profileName, emulatorName);

            //EmulatorProfile emulatorProfile = database.Emulators.Get(guid).GetProfile(profileId);
            foreach (CustomEmulatorProfile customProfile in myApi.Database.Emulators.Get(guid).CustomProfiles)
            {
                //MessageBox.Show(customProfile.ImageExtensions[1], customProfile.Name);
                if (customProfile.Name == profileName)
                {
                    List<string> extensions = customProfile.ImageExtensions;
                    bool compressedFormats = false;
                    bool otherFormats = false;
                    foreach (string currentExtension in extensions)
                    {
                        if (currentExtension == "7z")
                            compressedFormats = true;
                        else if (currentExtension == "zip")
                            compressedFormats = true;
                        else
                            otherFormats = true;
                    }
                    if (compressedFormats && !otherFormats) // Decompression required
                    {
                        PopUp warning = new PopUp("Unpacking files", "Please wait for the process to finish");
                        // Show the owned form.
                        warning.Show();

                        //string destPath = @"C:\Playnite\";
                        string destPath = settings.Settings.OptionDestPath;

                        string source = args.SelectedRomFile.Replace(@"{InstallDir}\", args.Game.InstallDirectory);
                        //string destination = args.SelectedRomFile.Replace(".7z", "").Replace(".zip", "").Replace(@"{InstallDir}\", destPath) + "(" + args.Game.GameId + ")";
                        string destination = destPath + args.Game.GameId;
                        //MessageBox.Show(destination, source);

                        if (!Directory.Exists(destination))
                        {
                            ExtractFile(source, destination);
                            // Add original file reference for comparison
                            StreamWriter sourceFile = File.CreateText(destination + @"\source.txt");
                            sourceFile.WriteLine(source);
                            sourceFile.WriteLine(args.Game.GameId);
                            sourceFile.WriteLine(emulatorName);
                            sourceFile.WriteLine(profileName);
                            sourceFile.Close();
                        }
                        else
                        {
                            Directory.SetLastWriteTime(destination, DateTime.Now);
                        }

                        // Close the owned form.
                        warning.Close();
                    }
                }
            }

        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Add code to be executed when game is stopped.
            FreeCache();
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
        {
            return new ArchiveUnpackerSettingsView();
        }
    }
}
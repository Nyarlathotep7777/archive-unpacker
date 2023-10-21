using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
//using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Windows;
using static System.Net.WebRequestMethods;
using File = System.IO.File;
//using System.Windows.Forms;
//using System.Windows.Controls;

namespace ArchiveUnpacker
{
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
            {
                PlayniteApi.Dialogs.ShowErrorMessage("File not found", "Error during file extraction");
                return;
            }

            // If the directory doesn't exist, create it.
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            // Unpack file
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
                PlayniteApi.Dialogs.ShowErrorMessage(Ex.Message, "Error during file extraction");
            }
        }

        public static long DirSize(DirectoryInfo d)
        {
            // Calculate folder size
            try
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
            catch (System.Exception)
            {
                return 0;
            }
        }

        private void FreeCache()
        {
            string destPath = settings.Settings.OptionDestPath;

            // If the directory doesn't exist, exit
            if (!Directory.Exists(destPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage("Folder not found", "Error in cache folder");
                return;
            }

            // Sort sub folders by last access time
            IEnumerable<string> folders = Directory.EnumerateDirectories(destPath).OrderBy(folder => Directory.GetLastWriteTime(folder));

            // Print folder list
            try
            {
                StreamWriter sourceFile = null;
                if (settings.Settings.OptionSaveDetails == true)
                    sourceFile = File.CreateText(destPath + @"orderedList.txt");

                foreach (string folder in folders)
                {
                    long d = DirSize(new DirectoryInfo(destPath));
                    if (d > (long)1024 * 1024 * 1024 * settings.Settings.OptionSize)
                    {
                        if (settings.Settings.OptionSaveDetails == true)
                            sourceFile.WriteLine(d + " bigger than limit " + (long)1024 * 1024 * 1024 * settings.Settings.OptionSize);
                        MessageBoxResult mr = MessageBoxResult.OK;
                        if (settings.Settings.OptionConfirmDelete == true)
                        {
                            string files = "";
                            foreach (string file in Directory.EnumerateFiles(folder))
                            {
                                files += file + System.Environment.NewLine;
                            }

                            mr = PlayniteApi.Dialogs.ShowMessage(files, "Removing cache folder", MessageBoxButton.OKCancel);
                        }
                        if (mr == MessageBoxResult.OK)
                        {
                            Directory.Delete(folder, true);
                            if (settings.Settings.OptionSaveDetails == true)
                                sourceFile.WriteLine(folder + " removed");
                        }
                        else
                        {
                            if (settings.Settings.OptionSaveDetails == true)
                                sourceFile.WriteLine(folder + " kept");
                        }
                    }
                    else
                    {
                        if (settings.Settings.OptionSaveDetails == true)
                            sourceFile.WriteLine(folder + " kept");
                    }
                }
                if (settings.Settings.OptionSaveDetails == true)
                    sourceFile.WriteLine(DirSize(new DirectoryInfo(destPath)) + " bytes remaining in cache");
                if (settings.Settings.OptionSaveDetails == true)
                    sourceFile.Close();
            }
            catch (System.Exception Ex)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(Ex.Message, "Error in cache folder");
            }
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
            try
            {
                IGameDatabase database = myApi.Database;
                GameAction action = args.SourceAction;

                // If it cannot access data structures it should exit quietly
                if ((database == null) || (action == null))
                    return;

                Guid guid = action.EmulatorId;
                string profileId = action.EmulatorProfileId;

                string emulatorName = database.Emulators.Get(guid).Name;
                string profileName = database.Emulators.Get(guid).GetProfile(profileId).Name;
                //MessageBox.Show(profileName, emulatorName);

                // If it cannot access data structures it should exit quietly
                if (myApi.Database.Emulators.Get(guid).CustomProfiles == null)
                    return;

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
                            //string destPath = @"C:\Playnite\";
                            string destPath = settings.Settings.OptionDestPath;

                            string source = args.SelectedRomFile.Replace(@"{InstallDir}\", args.Game.InstallDirectory);
                            //string destination = args.SelectedRomFile.Replace(".7z", "").Replace(".zip", "").Replace(@"{InstallDir}\", destPath) + "(" + args.Game.GameId + ")";
                            string destination = destPath + args.Game.GameId;
                            //MessageBox.Show(destination, source);

                            if (!Directory.Exists(destination))
                            {
                                PlayniteApi.Dialogs.ActivateGlobalProgress((prg) =>
                                {
                                    prg.ProgressMaxValue = 1;
                                    prg.CurrentProgressValue = -1;
                                    prg.Text = "Unpacking files";
                                    try
                                    {
                                        ExtractFile(source, destination);
                                    }
                                    catch (Exception e)
                                    {
                                    }
                                }, new GlobalProgressOptions("") { IsIndeterminate = true });

                                if (settings.Settings.OptionSaveDetails == true)
                                {
                                    // Add original file reference for comparison
                                    StreamWriter sourceFile = File.CreateText(destination + @"\source.txt");
                                    sourceFile.WriteLine(source);
                                    sourceFile.WriteLine(args.Game.GameId);
                                    sourceFile.WriteLine(emulatorName);
                                    sourceFile.WriteLine(profileName);
                                    sourceFile.Close();
                                }
                            }
                            else
                            {
                                Directory.SetLastWriteTime(destination, DateTime.Now);
                            }
                        }
                    }
                }
            }
            catch (System.Exception Ex)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(Ex.Message, "Error starting game");
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
﻿using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArchiveUnpacker
{
    public class ArchiveUnpackerSettings : ObservableObject
    {
        private string optionZPath = @"C:\Program Files\7-Zip\7z.exe";
        private string optionDestPath = @"C:\Playnite\";
        private int optionSize = 5;
        private bool optionConfirmDelete = true;

        private bool optionThatWontBeSaved = false;

        public string OptionZPath { get => optionZPath; set => SetValue(ref optionZPath, value); }
        public string OptionDestPath { get => optionDestPath; set => SetValue(ref optionDestPath, value); }
        public int OptionSize { get => optionSize; set => SetValue(ref optionSize, value); }
        public bool OptionConfirmDelete { get => optionConfirmDelete; set => SetValue(ref optionConfirmDelete, value); }
        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        [DontSerialize]
        public bool OptionThatWontBeSaved { get => optionThatWontBeSaved; set => SetValue(ref optionThatWontBeSaved, value); }
    }

    public class ArchiveUnpackerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ArchiveUnpacker plugin;
        private ArchiveUnpackerSettings editingClone { get; set; }

        private ArchiveUnpackerSettings settings;
        public ArchiveUnpackerSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public ArchiveUnpackerSettingsViewModel(ArchiveUnpacker plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<ArchiveUnpackerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new ArchiveUnpackerSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}
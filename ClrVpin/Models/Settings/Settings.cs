﻿using System;
using System.IO;
using PropertyChanged;
using Utils;

namespace ClrVpin.Models.Settings
{
    [AddINotifyPropertyChangedInterface]
    public class Settings
    {
        public Settings()
        {
            // default settings - will be overwritten AFTER ctor by the deserialized settings if they exist
            TableFolder = @"C:\vp\tables\vpx";
            FrontendFolder = @"C:\vp\apps\PinballX";
            BackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClrVpin", "backup");
            TrainerWheels = true;

            Rebuilder = new RebuilderSettings();
        }

        public string TableFolder { get; set; }
        public string FrontendFolder { get; set; }
        public string BackupFolder { get; set; }
        public bool TrainerWheels { get; set; }

        public RebuilderSettings Rebuilder { get; set; }
    }

    [AddINotifyPropertyChangedInterface]
    public class RebuilderSettings
    {
        public RebuilderSettings()
        {
            SourceFolder = SpecialFolder.Downloads;
        }

        public string SourceFolder { get; set; }
        public string DestinationContentType { get; set; }
    }
}
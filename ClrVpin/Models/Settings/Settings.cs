﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PropertyChanged;
using Utils;

namespace ClrVpin.Models.Settings
{
    [AddINotifyPropertyChangedInterface]
    public class Settings
    {
        public Settings()
        {
            // default settings
            // - during json.net deserialization.. ctor is invoked BEFORE deserialized version overwrites the values, i.e. they will be overwritten where a stored setting exists
            Version = MinVersion;

            TableFolder = @"C:\vp\tables\vpx";
            FrontendFolder = @"C:\vp\apps\PinballX";
            BackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClrVpin", "backup");
            TrainerWheels = true;

            FrontendFolders = new List<ContentType>
            {
                new ContentType {Enum = ContentTypeEnum.Database, Tip = "Pinball X or Pinball Y database file", Extensions = "*.xml", IsDatabase = true},
                new ContentType {Enum = ContentTypeEnum.TableAudio, Tip = "Audio used when displaying a table", Extensions = "*.mp3, *.wav"},
                new ContentType {Enum = ContentTypeEnum.LaunchAudio, Tip = "Audio used when launching a table", Extensions = "*.mp3, *.wav"},
                new ContentType {Enum = ContentTypeEnum.TableVideos, Tip = "Video used when displaying a table", Extensions = "*.f4v, *.mp4, *.mkv"},
                new ContentType {Enum = ContentTypeEnum.BackglassVideos, Tip = "Video used when displaying a table's backglass", Extensions = "*.f4v, *.mp4, *.mkv"},
                new ContentType {Enum = ContentTypeEnum.WheelImages, Tip = "Image used when displaying a table", Extensions = "*.png, *.apng, *.jpg"}

                // todo; table folders
                //new ContentType_Obsolete {Enum = "Tables", Extensions = new[] {"*.png"}, GetXxxHits = g => g.WheelImageHits},
                //new ContentType_Obsolete {Enum = "Backglass", Extensions = new[] {"*.png"}, GetXxxHits = g => g.WheelImageHits},
                //new ContentType_Obsolete {Enum = "Point of View", Extensions = new[] {"*.png"}, GetXxxHits = g => g.WheelImageHits},
            };
            FrontendFolders.ForEach(x => x.Description = x.Enum.GetDescription());

            Rebuilder = new RebuilderSettings();
            Scanner = new ScannerSettings();
        }
        
        public int Version { get; set; }

        public string TableFolder { get; set; }
        public string FrontendFolder { get; set; }
        public string BackupFolder { get; set; }
        public bool TrainerWheels { get; set; }
        public List<ContentType> FrontendFolders { get; set; }

        public ScannerSettings Scanner { get; set; }
        public RebuilderSettings Rebuilder { get; set; }

        public static int MinVersion = 1;

        public ContentType GetDestinationContentType() => FrontendFolders.First(x => x.Description == Rebuilder.DestinationContentType);

        // all possible content types (except database) - to be used elsewhere to create check collections
        public ContentType[] GetContentTypes() => FrontendFolders.Where(x => !x.IsDatabase).ToArray();
    }
}
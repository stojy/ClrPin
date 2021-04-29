﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using PropertyChanged;
using Utils;

namespace ClrVpin.Models
{
    [AddINotifyPropertyChangedInterface]
    public class Config
    {
        // abstract the underlying settings designer class because..
        // - users are agnostic to the underlying Properties.Settings.Default get/set implementation
        // - simpler xaml binding to avoid need for either..
        //   - StaticResource reference; prone to errors if Default isn't referenced in Folder (i.e. else new class used)
        //     e.g. <properties:Settings x:Key="Settings"/>
        //          Text="{Binding Source={StaticResource Settings}, Folder=Default.FrontendFolder}"
        //   - Static reference; too long
        //     e.g. Text="{Binding Source={x:Static p:Settings.Default}, Folder=FrontendFolder}"
        //   - vs a simple regular data binding
        //     e.g. Text="{Binding FrontendFolder}"

        public Config()
        {
            CheckContentTypes = new ObservableStringCollection<string>(Properties.Settings.Default.CheckContentTypes).Observable;
            CheckHitTypes = new ObservableCollectionJson<HitType>(Properties.Settings.Default.CheckHitTypes, value => Properties.Settings.Default.CheckHitTypes = value).Observable;
            FixHitTypes = new ObservableCollectionJson<HitType>(Properties.Settings.Default.FixHitTypes, value => Properties.Settings.Default.FixHitTypes = value).Observable;

            // assign some default frontend folders if there are none
            if (string.IsNullOrEmpty(FrontendFoldersJson))
                Default();
        }

        private void Default()
        {
            var defaultFrontendFolders = new List<ContentType>
            {
                new ContentType {Type = Database, Extensions = "*.xml", IsDatabase = true},
                new ContentType {Type = TableAudio, Extensions = "*.mp3, *.wav"},
                new ContentType {Type = LaunchAudio, Extensions = "*.mp3, *.wav"},
                new ContentType {Type = TableVideos, Extensions = "*.f4v, *.mp4"},
                new ContentType {Type = BackglassVideos, Extensions = "*.f4v, *.mp4"},
                new ContentType {Type = WheelImages, Extensions = "*.png, *.jpg"}
            };
            FrontendFoldersJson = JsonSerializer.Serialize(defaultFrontendFolders);
        }

        private string FrontendFoldersJson
        {
            get => Properties.Settings.Default.FrontendFoldersJson;
            set => Properties.Settings.Default.FrontendFoldersJson = value;
        }

        public string FrontendFolder
        {
            get => Properties.Settings.Default.FrontendFolder;
            set => Properties.Settings.Default.FrontendFolder = value;
        }

        public string FrontendDatabaseFolder
        {
            get => Properties.Settings.Default.FrontendDatabaseFolder;
            set => Properties.Settings.Default.FrontendDatabaseFolder = value;
        }

        public string TableFolder
        {
            get => Properties.Settings.Default.TableFolder;
            set => Properties.Settings.Default.TableFolder = value;
        }

        public List<ContentType> GetFrontendFolders() => JsonSerializer.Deserialize<List<ContentType>>(FrontendFoldersJson);
        public void SetFrontendFolders(IEnumerable<ContentType> frontendFolders) => FrontendFoldersJson = JsonSerializer.Serialize(frontendFolders);

        public readonly ObservableCollection<string> CheckContentTypes;
        public readonly ObservableCollection<HitType> CheckHitTypes;
        public readonly ObservableCollection<HitType> FixHitTypes;

        public const string Database = "Database";
        public const string TableAudio = "Table Audio";
        public const string LaunchAudio = "Launch Audio";
        public const string TableVideos = "Table Videos";
        public const string BackglassVideos = "Backglass Videos";
        public const string WheelImages = "Wheel Images";
    }
}
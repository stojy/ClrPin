﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ClrVpin.Models;
using PropertyChanged;
using Utils;
using ActionCommand = Microsoft.Xaml.Behaviors.Core.ActionCommand;

namespace ClrVpin.Settings
{
    [AddINotifyPropertyChangedInterface]
    public class Settings
    {
        public Settings()
        {
            FrontendFolderCommand = new ActionCommand(() => FolderUtil.Get("Frontend Root", Config.FrontendFolder, folder => Config.FrontendFolder = folder));
            AutoAssignFoldersCommand = new ActionCommand(AutoAssignFolders);

            TablesFolderCommand = new ActionCommand(() => FolderUtil.Get("Table and B2S", Config.TableFolder, folder => Config.TableFolder = folder));

            var configFrontendFolders = Config.GetFrontendFolders();
            FrontendFolders = configFrontendFolders!.Select(folder => new ContentTypeModel(folder, () => Config.SetFrontendFolders(FrontendFolders))).ToList();

            // todo; table folders
            //new ContentType_Obsolete {Type = "Tables", Extensions = new[] {"*.png"}, GetXxxHits = g => g.WheelImageHits},
            //new ContentType_Obsolete {Type = "Backglass", Extensions = new[] {"*.png"}, GetXxxHits = g => g.WheelImageHits},
            //new ContentType_Obsolete {Type = "Point of View", Extensions = new[] {"*.png"}, GetXxxHits = g => g.WheelImageHits},
        }

        public ICommand TablesFolderCommand { get; }
        public ICommand FrontendFolderCommand { get; }
        public ICommand AutoAssignFoldersCommand { get; }

        public Config Config { get; } = Model.Config;

        public List<ContentTypeModel> FrontendFolders { get; init; }

        public void Show(Window parent)
        {
            var window = new Window
            {
                Owner = parent,
                Content = this,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ContentTemplate = parent.FindResource("SettingsTemplate") as DataTemplate
            };
            window.Show();
            parent.Hide();

            window.Closed += (_, _) =>
            {
                Properties.Settings.Default.Save();
                parent.Show();
            };
        }

        private void AutoAssignFolders()
        {
            // automatically assign folders based on the frontend root folder
            FrontendFolders.Where(x => !x.IsDatabase).ForEach(x => x.Folder = $@"{Config.FrontendFolder}\Media\Visual Pinball\{x.Type}");
            FrontendFolders.First(x => x.IsDatabase).Folder = $@"{Config.FrontendFolder}\Databases\Visual Pinball";

            Config.SetFrontendFolders(FrontendFolders);
        }
    }
}
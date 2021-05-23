﻿using System;
using ClrVpin.Models;
using Microsoft.Xaml.Behaviors.Core;
using PropertyChanged;

namespace ClrVpin.Settings
{
    [AddINotifyPropertyChangedInterface]
    public class ContentTypeModel : FolderTypeDetail
    {
        public ContentTypeModel(ContentType contentType, Action updateFolderDetail) 
        {
            ContentType = contentType;
            Folder = contentType.Folder;
            Description = contentType.Description;
            Extensions = string.Join(", ", contentType.Extensions);

            TextChangedCommand = new ActionCommand(() =>
            {
                // for storage
                contentType.Folder = Folder;
                contentType.Extensions = Extensions;
                updateFolderDetail();
            });
            
            FolderExplorerCommand = new ActionCommand(() => FolderUtil.Get(Description, Folder, folder =>
            {
                // for display
                Folder = folder;

                // for storage
                contentType.Folder = folder;
                updateFolderDetail();
            }));
        }

        public ContentType ContentType { get; set; }
    }
}
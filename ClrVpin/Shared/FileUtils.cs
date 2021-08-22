﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteSizeLib;
using ClrVpin.Logging;
using ClrVpin.Models;
using Utils;

namespace ClrVpin.Shared
{
    public static class FileUtils
    {
        static FileUtils()
        {
            _settings = Model.Settings;
        }

        public static string ActiveBackupFolder { get; private set; }

        public static string SetActiveBackupFolder(string rootBackupFolder)
        {
            _rootBackupFolder = rootBackupFolder;
            return ActiveBackupFolder = $"{rootBackupFolder}\\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        }

        public static void Delete(string file, HitTypeEnum hitTypeEnum, string contentType, Action<string> backupAction = null)
        {
            Backup(file, "deleted", backupAction);
            Delete(file);
        }

        public static void DeleteIgnored(string sourceFile, string destinationFile, HitTypeEnum hitTypeEnum, string contentType, Action<string> backupAction = null)
        {
            Backup(sourceFile, "deleted.ignored", backupAction);
            DeleteIgnored(sourceFile, destinationFile);
        }

        public static IEnumerable<FileDetail> DeleteAllExcept(IEnumerable<Hit> hits, Hit hit, ICollection<HitTypeEnum> supportedHitTypes)
        {
            var deleted = new List<FileDetail>();

            // delete all 'real' files except the specified hit
            hits.Except(hit).Where(x => x.Size.HasValue).ForEach(h => deleted.Add(Delete(h, supportedHitTypes)));

            return deleted;
        }

        public static void Merge(string sourceFile, string destinationFile, HitTypeEnum hitTypeEnum, string contentType, bool deleteSource, bool preserveDateModified, IEnumerable<string> kindredExtensions,
            Action<string> backupAction)
        {
            // merge the specific file
            Merge(sourceFile, destinationFile, hitTypeEnum, contentType, deleteSource, preserveDateModified, backupAction);

            // merge any kindred files
            ExecuteForKindred(kindredExtensions, sourceFile, destinationFile, (source, destination) => Merge(source, destination, hitTypeEnum, contentType, deleteSource, preserveDateModified));
        }

        public static FileDetail Rename(Hit hit, Game game, ICollection<HitTypeEnum> supportedHitTypes, IEnumerable<string> kindredExtensions)
        {
            var renamed = false;

            if (supportedHitTypes.Contains(hit.Type))
            {
                renamed = true;

                // determine the correct name - different for media vs pinball
                var correctName = game.GetContentName(_settings.GetContentType(hit.ContentTypeEnum).Category);

                var extension = Path.GetExtension(hit.Path);
                var path = Path.GetDirectoryName(hit.Path);
                var newFile = Path.Combine(path!, $"{correctName}{extension}");

                // rename specific file
                Rename(hit.Path, newFile, hit.Type, hit.ContentType, backupFile => hit.Path = backupFile);

                // rename any kindred files
                ExecuteForKindred(kindredExtensions, hit.Path, newFile, (source, destination) => Rename(source, destination, hit.Type, hit.ContentType));
            }

            return new FileDetail(hit.ContentTypeEnum, hit.Type, renamed ? FixFileTypeEnum.Renamed : null, hit.Path, hit.Size ?? 0);
        }

        public static void DeleteActiveBackupFolderIfEmpty()
        {
            // delete empty backup folders - i.e. if there are no files (empty sub-directories are allowed)
            if (Directory.Exists(ActiveBackupFolder))
            {
                var files = Directory.EnumerateFiles(ActiveBackupFolder, "*", SearchOption.AllDirectories);
                if (!files.Any())
                {
                    Logger.Info($"Deleting empty backup folder: '{ActiveBackupFolder}'");
                    Directory.Delete(ActiveBackupFolder, true);
                }
            }

            // if directory doesn't exist (e.g. deleted as per above OR never existed), then assign the active folder back to the root folder, i.e. a valid folder that exists
            if (!Directory.Exists(ActiveBackupFolder))
                ActiveBackupFolder = _rootBackupFolder;
        }

        public static string GetFileInfoStatistics(string file)
        {
            FileInfo fileInfo = null;
            if (File.Exists(file))
                fileInfo = new FileInfo(file);

            return fileInfo != null ? $"{ByteSize.FromBytes(fileInfo.Length).ToString("0.#"),-8} {fileInfo.LastWriteTime:dd/MM/yy HH:mm:ss} - {file}" : $"{"(n/a: new file)",-26} - {file}";
        }

        private static void Rename(string sourceFile, string newFile, HitTypeEnum hitTypeEnum, string contentType, Action<string> backupAction = null)
        {
            //Logger.Info($"Renaming file{GetTrainerWheelsDisclosure()}.. type: {hitTypeEnum.GetDescription()}, content: {contentType}, original: {sourceFile}, new: {newFile}");

            Backup(sourceFile, "renamed", backupAction);
            Rename(sourceFile, newFile);
        }

        private static void Merge(string sourceFile, string destinationFile, HitTypeEnum hitTypeEnum, string contentType, bool deleteSource, bool preserveDateModified, Action<string> backupAction = null)
        {
            // backup the existing file (if any) before overwriting
            Backup(destinationFile, "deleted");

            // backup the source file before merging it
            Backup(sourceFile, "merged", backupAction);

            // copy the source file into the 'merged' destination folder
            Copy(sourceFile, destinationFile);

            // delete the source file if required - no need to backup as this is already done in the "merged" folder
            if (deleteSource)
                Delete(sourceFile);

            // optionally reset date modified if preservation isn't selected
            // - by default windows behaviour when copying file.. last access & creation timestamps are DateTime.Now, but last modified is unchanged!
            if (!preserveDateModified)
                File.SetLastWriteTime(destinationFile, DateTime.Now);
        }

        private static void ExecuteForKindred(IEnumerable<string> kindredExtensions, string sourceFile, string destinationFile, Action<string, string> action)
        {
            // merge any kindred files
            var kindredFiles = GetKindredFiles(new FileInfo(sourceFile), kindredExtensions);
            var destinationFolder = Path.GetDirectoryName(destinationFile);

            kindredFiles.ForEach(file =>
            {
                // use source file name (minus extension) instead of kindred file name to ensure the case is correct!
                var fileName = $"{Path.GetFileNameWithoutExtension(destinationFile)}{Path.GetExtension(file)}";

                var destinationFileName = Path.Combine(destinationFolder!, fileName);
                action(file, destinationFileName);
            });
        }

        private static IEnumerable<string> GetKindredFiles(FileInfo fileInfo, IEnumerable<string> kindredExtensionsList)
        {
            var kindredExtensions = kindredExtensionsList.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.TrimStart('*').ToLower());
            var allFiles = Directory.EnumerateFiles(fileInfo.DirectoryName!, $"{Path.GetFileNameWithoutExtension(fileInfo.Name)}.*").Select(x => x.ToLower()).ToList();

            var kindredFiles = allFiles.Where(file => kindredExtensions.Any(file.EndsWith)).ToList();
            return kindredFiles;
        }

        private static void Backup(string file, string subFolder, Action<string> backupAction = null)
        {
            if (!_settings.TrainerWheels && File.Exists(file))
            {
                // backup file (aka copy) to the specified sub folder
                // - no logging since the backup is intended to be transparent
                var backupFile = CreateBackupFileName(file, subFolder);
                File.Copy(file, backupFile, true);

                backupAction?.Invoke(backupFile);
            }
        }

        private static string CreateBackupFileName(string file, string subFolder = "")
        {
            var contentFolder = Path.GetDirectoryName(file)!.Split("\\").Last();
            var folder = Path.Combine(ActiveBackupFolder, subFolder, contentFolder);
            var destFileName = Path.Combine(folder, Path.GetFileName(file));

            // store backup file in the same folder structure as the source file
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return destFileName;
        }

        private static FileDetail Delete(Hit hit, ICollection<HitTypeEnum> supportedHitTypes)
        {
            var deleted = false;

            // only delete file if configured to do so
            if (supportedHitTypes.Contains(hit.Type))
            {
                deleted = true;
                Delete(hit.Path, hit.Type, hit.ContentType, newFile => hit.Path = newFile);
            }

            return new FileDetail(hit.ContentTypeEnum, hit.Type, deleted ? FixFileTypeEnum.Deleted : null, hit.Path, hit.Size ?? 0);
        }

        private static void Delete(string sourceFile)
        {
            Logger.Debug($"- deleting{GetTrainerWheelsDisclosure()}..\n  src: {GetFileInfoStatistics(sourceFile)}");

            if (!_settings.TrainerWheels)
                File.Delete(sourceFile);
        }

        // same as delete, but also logging the destination file info (for comparison)
        private static void DeleteIgnored(string sourceFile, string destinationFile)
        {
            Logger.Debug($"- deleting ignored{GetTrainerWheelsDisclosure()}..\n  src: {GetFileInfoStatistics(sourceFile)}\n  dst: {GetFileInfoStatistics(destinationFile)}");

            if (!_settings.TrainerWheels)
                File.Delete(sourceFile);
        }

        private static void Rename(string sourceFile, string destinationFile)
        {
            Logger.Debug($"- renaming{GetTrainerWheelsDisclosure()}..\n  src: {GetFileInfoStatistics(sourceFile)}\n  dst: {GetFileInfoStatistics(destinationFile)}");

            if (!_settings.TrainerWheels)
                File.Move(sourceFile, destinationFile, true);
        }

        private static void Copy(string sourceFile, string destinationFile)
        {
            Logger.Debug($"- copying{GetTrainerWheelsDisclosure()}..\n  src: {GetFileInfoStatistics(sourceFile)}\n  dst: {GetFileInfoStatistics(destinationFile)}");

            if (!_settings.TrainerWheels)
                File.Copy(sourceFile, destinationFile, true);
        }


        private static string GetTrainerWheelsDisclosure() => _settings.TrainerWheels ? " (ignored: trainer wheels)" : "";

        private static string _rootBackupFolder;
        private static readonly Models.Settings.Settings _settings;
    }
}
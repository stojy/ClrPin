﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClrVpin.Logging;
using ClrVpin.Models;
using ClrVpin.Scanner;
using Utils;

namespace ClrVpin.Tables
{
    public static class TableUtils
    {
        public static List<Game> GetDatabases()
        {
            var databaseDetail = Model.Config.GetFrontendFolders().First(x => x.IsDatabase);

            // scan through all the databases in the folder
            var files = Directory.EnumerateFiles(databaseDetail.Folder, databaseDetail.Extensions);

            var games = new List<Game>();

            files.ForEach(file =>
            {
                var doc = XDocument.Load(file);
                if (doc.Root == null)
                    throw new Exception("Failed to load database");

                var menu = doc.Root.Deserialize<Menu>();
                var number = 1;
                menu.Games.ForEach(g =>
                {
                    g.Number = number++;
                    g.Ipdb = g.IpdbId ?? g.IpdbNr;
                    g.IpdbUrl = string.IsNullOrEmpty(g.Ipdb) ? "" : $"https://www.ipdb.org/machine.cgi?id={g.Ipdb}";
                    g.NavigateToIpdbCommand = new ActionCommand(() => NavigateToIpdb(g.IpdbUrl));
                });

                games.AddRange(menu.Games);
            });

            return games;
        }

        private static void NavigateToIpdb(string url) => Process.Start(new ProcessStartInfo(url) {UseShellExecute = true});

        public static List<FixFileDetail> Check(List<Game> games)
        {
            var otherFiles = new List<FixFileDetail>();

            // for the configured content types only.. check the installed content files against those specified in the database
            var checkContentTypes = Model.Config.GetFrontendFolders()
                .Where(x => !x.IsDatabase)
                .Where(type => Model.Config.SelectedCheckContentTypes.Contains(type.Description));

            foreach (var contentType in checkContentTypes)
            {
                var mediaFiles = GetMedia(contentType);
                var unknownMedia = AddMediaToGames(games, mediaFiles, contentType.Enum, game => game.Content.ContentHitsCollection.First(contentHits => contentHits.Type == contentType.Enum));
                otherFiles.AddRange(unknownMedia);

                if (Model.Config.SelectedCheckHitTypes.Contains(HitTypeEnum.Unsupported))
                {
                    var unsupportedFiles = GetUnsupportedMedia(contentType);
                    otherFiles.AddRange(unsupportedFiles);
                }

                // todo; scan non-media content, e.g. tables and b2s
            }

            CheckMissing(games);

            return otherFiles;
        }

        public static async Task<List<FixFileDetail>> FixAsync(List<Game> games, List<FixFileDetail> otherFileDetails, string backupFolder)
        {
            var fixedFileDetails = await Task.Run(() => Fix(games, otherFileDetails, backupFolder));
            return fixedFileDetails;
        }

        private static List<FixFileDetail> Fix(List<Game> games, List<FixFileDetail> otherFileDetails, string backupFolder)
        {
            _activeBackupFolder = $"{backupFolder}\\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            var fixedFileDetails = new List<FixFileDetail>();

            // fix files associated with games
            games.ForEach(game =>
            {
                game.Content.ContentHitsCollection.ForEach(contentHitCollection =>
                {
                    if (TryGet(contentHitCollection.Hits, out var hit, HitTypeEnum.Valid))
                    {
                        // valid hit exists.. so delete everything else
                        fixedFileDetails.AddRange(DeleteAllExcept(contentHitCollection.Hits, hit));
                    }
                    else if (TryGet(contentHitCollection.Hits, out hit, HitTypeEnum.WrongCase, HitTypeEnum.TableName, HitTypeEnum.Fuzzy))
                    {
                        // for all 3 hit types.. rename file and delete other entries
                        fixedFileDetails.Add(Rename(hit, game));
                        fixedFileDetails.AddRange(DeleteAllExcept(contentHitCollection.Hits, hit));
                    }

                    // other hit types are n/a
                    // - duplicate extension - already taken care as a valid hit will exist
                    // - unknown - not associated with a game.. handled elsewhere
                    // - missing - can't be fixed.. requires file to be downloaded
                });
            });

            // delete files NOT associated with games, i.e. unknown files
            otherFileDetails.ForEach(x =>
            {
                if (x.HitType == HitTypeEnum.Unknown && Model.Config.SelectedFixHitTypes.Contains(HitTypeEnum.Unknown) ||
                    x.HitType == HitTypeEnum.Unsupported && Model.Config.SelectedFixHitTypes.Contains(HitTypeEnum.Unsupported))
                {
                    x.Deleted = true;
                    Delete(x.Path, x.HitType, null);
                }
            });

            // delete empty backup folders - i.e. if there are no files (empty sub-directories are allowed)
            if (Directory.Exists(_activeBackupFolder))
            {
                var files = Directory.EnumerateFiles(_activeBackupFolder, "*", SearchOption.AllDirectories);
                if (!files.Any())
                {
                    Logger.Info($"Deleting empty backup folder: '{_activeBackupFolder}'");
                    Directory.Delete(_activeBackupFolder, true);
                }
            }

            return fixedFileDetails;
        }

        private static bool TryGet(IEnumerable<Hit> hits, out Hit hit, params HitTypeEnum[] hitTypes)
        {
            // return the first entry found
            hit = hits.FirstOrDefault(h => hitTypes.Contains(h.Type));
            return hit != null;
        }

        private static IEnumerable<FixFileDetail> DeleteAllExcept(IEnumerable<Hit> hits, Hit hit)
        {
            var deleted = new List<FixFileDetail>();

            // delete all 'real' files except the specified hit
            hits.Except(hit).Where(x => x.Size.HasValue).ForEach(h => deleted.Add(Delete(h)));

            return deleted;
        }

        private static FixFileDetail Delete(Hit hit)
        {
            var deleted = false;

            // only delete file if configured to do so
            if (Model.Config.SelectedFixHitTypes.Contains(hit.Type))
            {
                deleted = true;
                Delete(hit.Path, hit.Type, hit.ContentType);
            }

            return new FixFileDetail(hit.ContentTypeEnum, hit.Type, deleted, false, hit.Path, hit.Size ?? 0);
        }

        private static void Delete(string file, HitTypeEnum hitType, string contentType)
        {
            var backupFileName = CreateBackupFileName(file);

            var prefix = Model.Config.TrainerWheels ? "Skipped (trainer wheels are on) " : "";
            Logger.Warn($"{prefix}Deleting file.. type: {hitType.GetDescription()}, content: {contentType ?? "n/a"}, file: {file}, backup: {backupFileName}");

            if (!Model.Config.TrainerWheels)
                File.Move(file, backupFileName, true);
        }

        private static FixFileDetail Rename(Hit hit, Game game)
        {
            var renamed = false;

            if (Model.Config.SelectedFixHitTypes.Contains(hit.Type))
            {
                renamed = true;

                var extension = Path.GetExtension(hit.Path);
                var path = Path.GetDirectoryName(hit.Path);
                var newFile = Path.Combine(path!, $"{game.Description}{extension}");

                var backupFileName = CreateBackupFileName(hit.Path);
                var prefix = Model.Config.TrainerWheels ? "Skipped (trainer wheels are on) " : "";
                Logger.Info($"{prefix}Renaming file.. type: {hit.Type.GetDescription()}, content: {hit.ContentType}, original: {hit.Path}, new: {newFile}, backup: {backupFileName}");

                if (!Model.Config.TrainerWheels)
                {
                    File.Copy(hit.Path!, backupFileName, true);
                    File.Move(hit.Path!, newFile, true);
                }
            }

            return new FixFileDetail(hit.ContentTypeEnum, hit.Type, false, renamed, hit.Path, hit.Size ?? 0);
        }

        private static string CreateBackupFileName(string file)
        {
            var baseFolder = Path.GetDirectoryName(file)!.Split("\\").Last();
            var folder = Path.Combine(_activeBackupFolder, baseFolder);
            var destFileName = Path.Combine(folder, Path.GetFileName(file));
            
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            
            return destFileName;
        }

        private static void CheckMissing(List<Game> games)
        {
            games.ForEach(game =>
            {
                // add missing content
                game.Content.ContentHitsCollection.ForEach(contentHitCollection =>
                {
                    if (!contentHitCollection.Hits.Any(hit => hit.Type == HitTypeEnum.Valid || hit.Type == HitTypeEnum.WrongCase))
                        contentHitCollection.Add(HitTypeEnum.Missing, game.Description);
                });
            });
        }

        private static IEnumerable<FixFileDetail> AddMediaToGames(IReadOnlyCollection<Game> games, IEnumerable<string> mediaFiles, ContentTypeEnum contentTypeEnum,
            Func<Game, ContentHits> getContentHits)
        {
            var unknownMediaFiles = new List<FixFileDetail>();

            // for each file, associate it with a game or if one can't be found, then mark it as unknown
            foreach (var mediaFile in mediaFiles)
            {
                Game matchedGame;

                // check for hit..
                // - skip hit types that aren't configured
                // - only 1 hit per file.. but a game can have multiple hits.. with a maximum of 1 valid hit
                // - todo; fuzzy match.. e.g. partial matches, etc.
                if ((matchedGame = games.FirstOrDefault(game => game.Description == Path.GetFileNameWithoutExtension(mediaFile))) != null)
                {
                    // if a match already exists, then assume this match is a duplicate name with wrong extension
                    // - file extension order is important as it determines the priority of the preferred extension
                    var contentHits = getContentHits(matchedGame);
                    contentHits.Add(contentHits.Hits.Any(hit => hit.Type == HitTypeEnum.Valid) ? HitTypeEnum.DuplicateExtension : HitTypeEnum.Valid, mediaFile);
                }
                else if ((matchedGame =
                    games.FirstOrDefault(game => string.Equals(game.Description, Path.GetFileNameWithoutExtension(mediaFile), StringComparison.CurrentCultureIgnoreCase))) != null)
                {
                    getContentHits(matchedGame).Add(HitTypeEnum.WrongCase, mediaFile);
                }
                else if ((matchedGame = games.FirstOrDefault(game => game.TableFile == Path.GetFileNameWithoutExtension(mediaFile))) != null)
                {
                    getContentHits(matchedGame).Add(HitTypeEnum.TableName, mediaFile);
                }
                else if ((matchedGame = games.FirstOrDefault(game =>
                    game.TableFile.StartsWith(Path.GetFileNameWithoutExtension(mediaFile)) || Path.GetFileNameWithoutExtension(mediaFile).StartsWith(game.TableFile) ||
                    game.Description.StartsWith(Path.GetFileNameWithoutExtension(mediaFile)) || Path.GetFileNameWithoutExtension(mediaFile).StartsWith(game.Description))
                    ) != null)
                {
                    // todo; add more 'fuzzy' checks
                    getContentHits(matchedGame).Add(HitTypeEnum.Fuzzy, mediaFile);
                }
                else
                {
                    unknownMediaFiles.Add(new FixFileDetail(contentTypeEnum, HitTypeEnum.Unknown, false, false, mediaFile, new FileInfo(mediaFile).Length));
                }
            }

            return unknownMediaFiles;
        }

        private static IEnumerable<string> GetMedia(ContentType contentType)
        {
            var files = contentType.ExtensionsList.Select(ext => Directory.EnumerateFiles(contentType.Folder, ext));

            return files.SelectMany(x => x).ToList();
        }

        private static IEnumerable<FixFileDetail> GetUnsupportedMedia(ContentType contentType)
        {
            // return all files that don't match the supported extensions
            var supportedExtensions = contentType.ExtensionsList.Select(x => x.TrimStart('*').ToLower());

            var allFiles = Directory.EnumerateFiles(contentType.Folder).Select(x => x.ToLower());
            
            var unsupportedFiles = allFiles.Where(file => !supportedExtensions.Any(file.EndsWith));

            var unsupportedFixFiles = unsupportedFiles.Select(file => new FixFileDetail(contentType.Enum, HitTypeEnum.Unsupported, false, false, file, new FileInfo(file).Length));

            return unsupportedFixFiles.ToList();
        }

        private static string _activeBackupFolder;
    }
}
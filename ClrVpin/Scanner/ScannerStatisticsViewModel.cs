﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ClrVpin.Controls;
using ClrVpin.Models.Settings;
using ClrVpin.Models.Shared;
using ClrVpin.Models.Shared.Database;
using ClrVpin.Shared;

namespace ClrVpin.Scanner
{
    public class ScannerStatisticsViewModel : StatisticsViewModel
    {
        public ScannerStatisticsViewModel(ObservableCollection<GameDetail> games, TimeSpan elapsedTime, ICollection<FileDetail> gameFiles, ICollection<FileDetail> unmatchedFiles)
            : base(games, elapsedTime, gameFiles, unmatchedFiles)
        {
            // hit type stats for all supported types only
            // - including the extra 'under the hood' types.. valid, unknown, unsupported
            SupportedHitTypes = StaticSettings.AllHitTypes.ToList();

            SupportedContentTypes = Settings.GetFixableContentTypes().Where(x => Settings.Scanner.SelectedCheckContentTypes.Contains(x.Description)).ToList();

            SelectedCheckContentTypes = Settings.Scanner.SelectedCheckContentTypes;

            // rebuilder doesn't support check and fix separately
            SelectedCheckHitTypes = Settings.Scanner.SelectedCheckHitTypes.ToList();
            SelectedFixHitTypes = Settings.Scanner.SelectedFixHitTypes.ToList();

            // unlike rebuilder, the total count represents the number of GameDetails
            TotalCount = Games.Count;
        }

        public void Show(Window parentWindow, double left, double top)
        {
            Window = new MaterialWindowEx
            {
                Owner = parentWindow,
                Title = "Scanner Statistics",
                Left = left,
                Top = top,
                Width = 770,
                Height = Model.ScreenWorkArea.Height - WindowMargin - WindowMargin,
                Content = this,
                Resources = parentWindow.Resources,
                ContentTemplate = parentWindow.FindResource("ScannerStatisticsTemplate") as DataTemplate
            };
            Window.Show();

            CreateStatistics();
        }

        protected override string CreateTotalStatistics()
        {
            var validHits = Games.SelectMany(x => x.Content.ContentHitsCollection).SelectMany(x => x.Hits).Where(x => x.Type == HitTypeEnum.CorrectName).ToList();

            var eligibleHits = Games.Count * Settings.Scanner.SelectedCheckContentTypes.Count;

            // all files
            var allFilesCount = validHits.Count + GameFiles.Count;
            var allFilesSize = validHits.Sum(x => x.Size) + GameFiles.Sum(x => x.Size) ?? 0;

            // renamed
            // - must be configured as a fix hit type
            // - unknown is n/a apply for renamable, i.e. since we don't know what game/table to rename it to
            var fixFilesRenamed = GameFiles.Where(x => x.Renamed).ToList();
            var fixFilesRenamedSize = fixFilesRenamed.Sum(x => x.Size);

            // removed (deleted)
            // - must be configured as a fix hit type
            var fixFilesDeleted = GameFiles.Where(x => x.Deleted).ToList();
            var fixFilesDeletedSize = fixFilesDeleted.Sum(x => x.Size);
            var fixFilesDeletedUnknown = fixFilesDeleted.Where(x => x.HitType == HitTypeEnum.Unknown).ToList();
            var fixFilesDeletedUnknownSize = fixFilesDeletedUnknown.Sum(x => x.Size);

            // ignored (removable and renamable)
            // - includes renamable AND removable files
            // - unknown..
            //   - n/a apply for renamable, i.e. since we don't know what game/table to rename it to
            //   - applicable for removable
            var fixFilesIgnored = GameFiles.Where(x => x.Ignored).ToList();
            var fixFilesIgnoredSize = fixFilesIgnored.Sum(x => x.Size);
            var fixFilesIgnoredUnknown = fixFilesIgnored.Where(x => x.HitType == HitTypeEnum.Unknown).ToList();
            var fixFilesIgnoredUnknownSize = fixFilesIgnoredUnknown.Sum(x => x.Size);
            var eligibleHitsPercentage = eligibleHits == 0 ? "n/a" : $"{(decimal)validHits.Count / eligibleHits:P2}";

            return "\n-----------------------------------------------\n" +
                   "\nTotals" +
                   $"\n{"- Available Tables",StatisticsKeyWidth}{Games.Count}" +
                   $"\n{"- Possible Content",StatisticsKeyWidth}{Games.Count * Settings.GetFixableContentTypes().Length}" +
                   $"\n{"- Checked Content",StatisticsKeyWidth}{eligibleHits}" +
                   $"\n\n{"All Files",StatisticsKeyWidth}{CreateFileStatistic(allFilesCount, allFilesSize)}" +
                   $"\n\n{"CorrectName Files",StatisticsKeyWidth}{CreateFileStatistic(validHits.Count, validHits.Sum(x => x.Size ?? 0))}" +
                   $"\n{"- Collection",StatisticsKeyWidth}{validHits.Count}/{eligibleHits} ({eligibleHitsPercentage})" +
                   $"\n\n{"Fixed/Fixable Files",StatisticsKeyWidth}{CreateFileStatistic(GameFiles.Count, GameFiles.Sum(x => x.Size))}" +
                   $"\n{"- renamed",StatisticsKeyWidth}{CreateFileStatistic(fixFilesRenamed.Count, fixFilesRenamedSize)}" +
                   $"\n{"- removed",StatisticsKeyWidth}{CreateFileStatistic(fixFilesDeleted.Count, fixFilesDeletedSize)}" +
                   $"\n{"  (criteria: unknown)",StatisticsKeyWidth}{CreateFileStatistic(fixFilesDeletedUnknown.Count, fixFilesDeletedUnknownSize)}" +
                   $"\n{"- renamable and removable",StatisticsKeyWidth}{CreateFileStatistic(fixFilesIgnored.Count, fixFilesIgnoredSize)}" +
                   $"\n{"  (criteria: unknown)",StatisticsKeyWidth}{CreateFileStatistic(fixFilesIgnoredUnknown.Count, fixFilesIgnoredUnknownSize)}" +
                   $"\n\n{"Time Taken",StatisticsKeyWidth}{ElapsedTime.TotalSeconds:f2}s";
        }

        private const double WindowMargin = 0;
    }
}
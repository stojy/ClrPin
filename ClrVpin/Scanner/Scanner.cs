﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClrVpin.Models;
using PropertyChanged;
using Utils;

namespace ClrVpin.Scanner
{
    [AddINotifyPropertyChangedInterface]
    public class Scanner
    {
        public Scanner(MainWindow parentWindow)
        {
            _parentWindow = parentWindow;

            StartCommand = new ActionCommand(Start);
            ConfigureCheckContentTypesCommand = new ActionCommand<string>(ConfigureCheckContentTypes);

            CheckHitTypesView = new ListCollectionView(CreateCheckHitTypes().ToList());
            _fixHitTypes = CreateFixHitTypes();
            FixHitTypesView = new ListCollectionView(_fixHitTypes.ToList());
        }

        public ListCollectionView CheckHitTypesView { get; set; }
        public ListCollectionView FixHitTypesView { get; set; }

        public ActionCommand<string> ConfigureCheckContentTypesCommand { get; set; }
        public ObservableCollection<Game> Games { get; set; }
        public ICommand StartCommand { get; set; }

        public void Show()
        {
            _scannerWindow = new Window
            {
                Owner = _parentWindow,
                Title = "Scanner",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = this,
                ContentTemplate = _parentWindow.FindResource("ScannerTemplate") as DataTemplate
            };

            _scannerWindow.Show();
            _parentWindow.Hide();
            _scannerWindow.Closed += (_, _) => _parentWindow.Show();
        }

        private void ShowResults(List<Tuple<string, long>> unknownFiles)
        {
            var scannerResults = new ScannerResults(_scannerWindow, Games);
            scannerResults.Show();

            var scannerStatistics = new ScannerStatistics(Games, _scanStopWatch, unknownFiles);
            scannerStatistics.Show(_scannerWindow, scannerResults.Window);

            var explorerWindow = new ScannerExplorer(Games);
            explorerWindow.Show(_scannerWindow, scannerResults.Window);

            _scannerWindow.Hide();
            scannerResults.Window.Closed += (_, _) =>
            {
                scannerStatistics.Close();
                explorerWindow.Close();
                _scannerWindow.Show();
            };
        }

        private IEnumerable<FeatureType> CreateCheckHitTypes()
        {
            // show all hit types
            var contentTypes = Hit.Types.Select(hitType =>
            {
                var featureType = new FeatureType
                {
                    Description = hitType.GetDescription(),
                    IsSupported = true,
                    IsActive = true
                };

                featureType.SelectedCommand = new ActionCommand(() =>
                {
                    Config.CheckHitTypes.Toggle(hitType);

                    // toggle the fix hit type checked & enabled
                    var fixHitType = _fixHitTypes.First(x => x.Description == featureType.Description);
                    fixHitType.IsSupported = featureType.IsActive;
                    if (!featureType.IsActive)
                        fixHitType.IsActive = false;
                });

                return featureType;
            });

            return contentTypes.ToList();
        }

        private static IEnumerable<FeatureType> CreateFixHitTypes()
        {
            // show all hit types, but allow them to be enabled and selected indirectly via the check hit type
            var contentTypes = Hit.Types.Select(hitType => new FeatureType
            {
                Description = hitType.GetDescription(),
                IsSupported = true,
                IsActive = true,
                SelectedCommand = new ActionCommand(() => Config.FixHitTypes.Toggle(hitType))
            });

            return contentTypes.ToList();
        }

        private static void ConfigureCheckContentTypes(string contentType) => Config.CheckContentTypes.Toggle(contentType);

        private void Start()
        {
            // todo; show progress bar

            _scanStopWatch = Stopwatch.StartNew();

            var games = ScannerUtils.GetDatabase();

            // todo; retrieve 'missing games' from spreadsheet

            var unknownFiles = ScannerUtils.Check(games);

            ScannerUtils.Fix(games);

            Games = new ObservableCollection<Game>(games);

            _scanStopWatch.Stop();

            ShowResults(unknownFiles);
        }

        private readonly IEnumerable<FeatureType> _fixHitTypes;
        private readonly MainWindow _parentWindow;
        private Window _scannerWindow;
        private Stopwatch _scanStopWatch;
    }
}
﻿using System.Collections.ObjectModel;
using PropertyChanged;

namespace ClrVpin.Models.Settings
{
    [AddINotifyPropertyChangedInterface]
    public class ScannerSettings
    {
        public ObservableCollection<string> SelectedCheckContentTypes { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<HitTypeEnum> SelectedCheckHitTypes { get; set; } = new ObservableCollection<HitTypeEnum>();
        public ObservableCollection<HitTypeEnum> SelectedFixHitTypes { get; set; } = new ObservableCollection<HitTypeEnum>();
    }
}
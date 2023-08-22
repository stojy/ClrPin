﻿using System;
using System.Collections.ObjectModel;
using ClrVpin.Models.Shared.Enums;
using PropertyChanged;

namespace ClrVpin.Models.Settings;

[AddINotifyPropertyChangedInterface]
[Serializable]
public class CommonFilterSettings
{
    public bool IsDynamicFiltering { get; set; }

    public string SelectedTableFilter { get; set; }
    public string SelectedManufacturerFilter { get; set; }

    public ObservableCollection<TechnologyTypeOptionEnum> SelectedTechnologyTypeOptions { get; set; } = new();
    
    public string SelectedYearBeginFilter { get; set; }
    public string SelectedYearEndFilter { get; set; }

    public DateTime? SelectedUpdatedAtDateBegin { get; set; }
    public DateTime? SelectedUpdatedAtDateEnd { get; set; }

    public ObservableCollection<YesNoNullableBooleanOptionEnum> SelectedManufacturedOptions { get; set; } = new();
}
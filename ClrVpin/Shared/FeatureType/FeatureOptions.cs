﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using ClrVpin.Controls;
using ClrVpin.Models.Feeder;
using Utils;
using Utils.Extensions;

namespace ClrVpin.Shared.FeatureType;

public static class FeatureOptions
{
    public static ListCollectionView<FeatureType> CreateFeatureOptionsSelectionView<T>(IEnumerable<EnumOption<T>> enumOptions, T highlightedOption, 
        Expression<Func<T>> selection, Action changedAction) where T : Enum
    {
        var memberAccessor = new Accessor<T>(selection);

        // create options with a single selection, e.g. style.. radio button, choice chip, etc
        var featureTypes = enumOptions.Select(option =>
        {
            var featureType = new FeatureType(Convert.ToInt32(option.Enum))
            {
                Tag = typeof(T).Name,
                Description = option.Description,
                Tip = option.Tip,
                IsSupported = true,
                IsHighlighted = option.Enum.IsEqual(highlightedOption),
                IsActive = option.Enum.IsEqual(memberAccessor.Get()),
                SelectedCommand = new ActionCommand(() =>
                {
                    memberAccessor.Set(option.Enum);
                    changedAction.Invoke();
                })
            };

            return featureType;
        }).ToList();

        return new ListCollectionView<FeatureType>(featureTypes);
    }

    public static ListCollectionView<FeatureType> CreateFeatureOptionsSelectionsView<T>(IEnumerable<EnumOption<T>> enumOptions, ObservableCollection<string> selections,
        Action changedAction, bool includeSelectAll = true) where T : Enum
    {
        // create options with a multiple selection support, e.g. style.. checkbox button, filter chip, etc
        var featureTypes = enumOptions.Select(option =>
        {
            var featureType = new FeatureType(Convert.ToInt32(option.Enum))
            {
                Description = option.Description,
                Tip = option.Tip,
                IsSupported = true,
                IsActive = selections.Contains(option.Description),
                //IsHighlighted = hitType.IsHighlighted,
                //IsHelpSupported = hitType.HelpUrl != null,
                //HelpAction = new ActionCommand(() => Process.Start(new ProcessStartInfo(hitType.HelpUrl) { UseShellExecute = true }))
                SelectedCommand = new ActionCommand(() =>
                {
                    selections.Toggle(option.Description);
                    changedAction();
                })
            };

            return featureType;
        }).ToList();

        if (includeSelectAll)
            featureTypes.Add(CreateSelectAll(featureTypes));

        return new ListCollectionView<FeatureType>(featureTypes);
    }

    public static FeatureType CreateSelectAll(List<FeatureType> featureTypes)
    {
        // a generic select/clear all feature type
        var selectAll = new FeatureType(-1)
        {
            Description = "Select/Clear All",
            Tip = "Select or clear all criteria/options",
            IsSupported = true,
            IsActive = featureTypes.All(x => x.IsActive),
            IsSpecial = true
        };

        selectAll.SelectedCommand = new ActionCommand(() =>
        {
            // select/clear every sibling feature type
            featureTypes.ForEach(featureType =>
            {
                // don't set state if it's not supported
                if (!featureType.IsSupported)
                    return;

                // update is active state before invoking command
                // - required in this order because this is how it would normally be seen if the underlying feature was changed via the UI
                var wasActive = featureType.IsActive;
                featureType.IsActive = selectAll.IsActive;

                // invoke action by only toggling on/off if not already in the on/off state
                // - to ensure the underlying model is updated
                if (selectAll.IsActive && !wasActive || !selectAll.IsActive && wasActive)
                    featureType.SelectedCommand.Execute(null);
            });
        });

        return selectAll;
    }
}
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using ClrVpin.Models.Shared.Game;

namespace ClrVpin.Models.Shared
{
    public class Content
    {
        // 1 or more content hits (e.g. launch audio, wheel, etc), each of which can contain multiple media file hits (e.g. wrong case, valid, etc)
        // - only the selected content types are added to the collection
        public List<ContentHits> ContentHitsCollection { get; } = new List<ContentHits>();

        // flattened collection of all media file hits (including valid) across all content types (that checking is enabled)
        public ObservableCollection<Hit> Hits { get; private set; }
        public ListCollectionView HitsView { get; set; }

        // true if game contains any hits types that are not valid
        public bool IsSmelly { get; set; }

        public static string GetName(GameDetail gameDetail, ContentTypeCategoryEnum category) =>
            // determine the correct name - different for media vs pinball
            category == ContentTypeCategoryEnum.Media ? gameDetail.Game.Description : gameDetail.Game.Name;

        public void Init(IEnumerable<ContentType> contentTypes)
        {
            // create content hits collection for the specified contentTypes, e.g. the selected contentTypes
            ContentHitsCollection.AddRange(contentTypes.Select(contentType => new ContentHits(contentType)));
        }

        public void Update(Func<IEnumerable<int>> getActiveContentFeatureTypes, Func<IEnumerable<int>> getActiveHitContentTypes)
        {
            // standard properties to avoid cost of recalculating getters during every request (e.g. wpf bindings)
            IsSmelly = ContentHitsCollection.Any(contentHits => contentHits.IsSmelly);

            Hits = new ObservableCollection<Hit>(ContentHitsCollection.SelectMany(contentHits => contentHits.Hits.ToList()));
            HitsView = new ListCollectionView(Hits)
            {
                // update HitsView based on the updated filtering content type and/or hit type
                // - the getFilteredXxx return their respective enum as an integer via FeatureType.Id
                Filter = hitObject => getActiveContentFeatureTypes().Contains((int)((Hit)hitObject).ContentTypeEnum) &&
                                      getActiveHitContentTypes().Contains((int)((Hit)hitObject).Type)
            };
        }
    }
}
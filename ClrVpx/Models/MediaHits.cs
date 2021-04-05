﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClrVpx.Models
{
    public class MediaHits
    {
        public MediaHits(string mediaType)
        {
            Type = mediaType;
        }

        public string Type { get; }
        public ObservableCollection<Hit> Hits { get; set; } = new ObservableCollection<Hit>();

        public bool IsMissing => Hits.Any(hit => hit.Type == HitType.Missing);
        public bool IsSmelly => SmellyHits.Any();
        public IEnumerable<Hit> SmellyHits => Hits.Where(hit => hit.Type != HitType.Valid);

        public void Add(HitType type, string path)
        {
            Hits.Add(new Hit(Type, path, type));
        }
    }
}
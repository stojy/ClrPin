﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClrVpin.Models
{
    public class ContentHits
    {
        public ContentHits(ContentType contentType)
        {
            _contentType = contentType;
        }

        public ContentTypeEnum Type => _contentType.Enum;
        public ObservableCollection<Hit> Hits { get; set; } = new ObservableCollection<Hit>();

        // todo; remove (expensive) expression getters
        public bool IsSmelly => Hits.Any(hit => hit.Type != HitTypeEnum.Valid);

        public void Add(HitTypeEnum hitType, string path)
        {
            // for missing content.. the path is the description, i.e. desirable file name without an extension
            if (hitType == HitTypeEnum.Missing)
            {
                // display format: <file>.<ext1> (or .<ext2>, .<ext3>)
                var extensions = _contentType.Extensions.Split(",").Select(x => x.Trim().TrimStart('*')).ToList();
                path = @$"{_contentType.Folder}\{path}{extensions.First()}";

                var otherExtensions = extensions.Skip(1).ToList();
                if (otherExtensions.Any())
                    path += $" (or {string.Join(", ", otherExtensions)})";
            }

            // only add hit type for valid hits OR if it has been configured to be checked
            if (hitType == HitTypeEnum.Valid || Model.Config.SelectedCheckHitTypes.Contains(hitType))
                Hits.Add(new Hit(_contentType.Enum, path, hitType));
        }

        private readonly ContentType _contentType;
    }
}
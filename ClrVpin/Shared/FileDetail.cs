﻿using ClrVpin.Models;

namespace ClrVpin.Shared
{
    public enum FixFileTypeEnum
    {
        Deleted,
        Renamed,
        Merged
    }

    public class FileDetail
    {
        public FileDetail(ContentTypeEnum contentTypeEnum, HitTypeEnum hitType, FixFileTypeEnum? fixFileType, string path, long size)
        {
            Path = path;
            Size = size;

            ContentType = contentTypeEnum;
            HitType = hitType;
            Deleted = fixFileType == FixFileTypeEnum.Deleted;
            Renamed = fixFileType == FixFileTypeEnum.Renamed;
            Merged = fixFileType == FixFileTypeEnum.Merged;
        }

        public ContentTypeEnum ContentType { get; }
        public HitTypeEnum HitType { get; }
        public bool Deleted { get; set; }
        public bool Renamed { get; }
        public bool Merged { get; set; }
        public bool Ignored => !Deleted && !Renamed && !Merged;

        public string Path { get; }
        public long Size { get; }
    }
}
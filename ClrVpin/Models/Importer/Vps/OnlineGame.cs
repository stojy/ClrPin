﻿using System.Collections.Generic;

namespace ClrVpin.Models.Importer.Vps;

// ReSharper disable ClassNeverInstantiated.Global - required for collections as r# doesn't realize this is a json deserialized object

// todo; move this to a 'Derived' object.. similar to Game
public class OnlineGame : OnlineGameBase
{
    // view model properties
    public Dictionary<string, FileCollection> AllFiles { get; set; }
    public IEnumerable<File> AllFilesList { get; set; }
    public List<ImageFile> ImageFiles { get; set; }

    public UrlSelection ImageUrlSelection { get; set; }
    public string YearString { get; set; }
    public bool IsTableDownloadAvailable { get; set; }

    public bool IsOriginal { get; set; }

    public string IpdbId { get; set; } = string.Empty;
    public string Description { get; set; }

    // reference to the highest fuzzy ranked DB match
    public GameHit Hit { get; set; }
}
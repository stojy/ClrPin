﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClrVpin.Importer.Vps;

// ReSharper disable ClassNeverInstantiated.Global - required for collections as r# doesn't realize this is a json deserialized object
// ReSharper disable UnusedMember.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
public class Game
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Manufacturer { get; set; }
    public int Year { get; set; }
    public string Type { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastCreatedAt { get; set; }

    public string IpdbUrl { get; set; }
    public bool Broken { get; set; }
    public string[] Designers { get; set; }

    [JsonPropertyName("theme")]
    public string[] Themes { get; set; }

    public int? Players { get; set; }
    public string[] Features { get; set; }
    public string Mpu { get; set; }
    public string ImgUrl { get; set; }

    public TableFiles TableFiles { get; set; } = new TableFiles();
    public ImageFile[] B2SFiles { get; set; } = Array.Empty<ImageFile>();
    public File[] WheelArtFiles { get; set; } = Array.Empty<File>();
    public File[] RomFiles { get; set; } = Array.Empty<File>();

    public File[] MediaPackFiles { get; set; } = Array.Empty<File>();
    public File[] AltColorFiles { get; set; } = Array.Empty<File>();
    public File[] SoundFiles { get; set; } = Array.Empty<File>();
    public File[] TopperFiles { get; set; } = Array.Empty<File>();
    public File[] PupPackFiles { get; set; } = Array.Empty<File>();
    public File[] PovFiles { get; set; } = Array.Empty<File>();
    public File[] AltSoundFiles { get; set; } = Array.Empty<File>();
    public File[] RuleFiles { get; set; } = Array.Empty<File>();

    // view model properties
    public int Index { get; set; }
    public override string ToString() => $"{Name} ({Manufacturer} {Year}), Tables={TableFiles.Count}, B2Ss={B2SFiles.Length}, Wheels={WheelArtFiles.Length}";
}

public class TableFiles : List<TableFile> { }

public class UrlDetail
{
    public bool Broken { get; set; }
    public string Url { get; set; }
}

public class File
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string[] Authors { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    public UrlDetail[] Urls { get; set; }
}

public class ImageFile : File
{
    public string ImgUrl { get; set; }
    public string[] Features { get; set; }
}

public class TableFile : ImageFile
{
    [JsonPropertyName("theme")]
    public string[] Themes { get; set; }

    public string TableFormat { get; set; }
    public string Comment { get; set; }
}
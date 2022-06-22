﻿using System.Collections.Generic;

namespace ClrVpin.Importer;

public class ImporterMatchStatistics
{
    public ImporterMatchStatistics()
    {
        _statistics = new Dictionary<string, int>
        {
            // create dictionary items upfront to ensure the preferred display ordering (for statistics)
            { MatchedTotal, 0 },
            { MatchedManufactured, 0 },
            { MatchedOriginal, 0 },
            { UnmatchedOnlineTotal, 0 },
            { UnmatchedOnlineManufactured, 0 },
            { UnmatchedOnlineOriginal, 0 },
            { UnmatchedLocalTotal, 0 },
            { UnmatchedLocalManufactured, 0 },
            { UnmatchedLocalOriginal, 0 }
        };
    }

    public void Increment(string key) => _statistics[key]++;
    public void Decrement(string key) => _statistics[key]--;

    public Dictionary<string, int> ToDictionary() => _statistics;

    // exists in local and online DB
    public const string MatchedTotal = nameof(MatchedTotal);
    public const string MatchedManufactured = nameof(MatchedManufactured);
    public const string MatchedOriginal = nameof(MatchedOriginal);

    // exists only in online DB
    public const string UnmatchedOnlineTotal = nameof(UnmatchedOnlineTotal);
    public const string UnmatchedOnlineManufactured = nameof(UnmatchedOnlineManufactured);
    public const string UnmatchedOnlineOriginal = nameof(UnmatchedOnlineOriginal);

    // exists only in local DB
    public const string UnmatchedLocalTotal = nameof(UnmatchedLocalTotal);
    public const string UnmatchedLocalManufactured = nameof(UnmatchedLocalManufactured);
    public const string UnmatchedLocalOriginal = nameof(UnmatchedLocalOriginal);

    private readonly Dictionary<string, int> _statistics;
}
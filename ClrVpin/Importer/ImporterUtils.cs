﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClrVpin.Converters;
using ClrVpin.Logging;
using ClrVpin.Models.Importer.Vps;
using ClrVpin.Models.Shared.Game;
using ClrVpin.Shared.Fuzzy;
using Utils.Extensions;

namespace ClrVpin.Importer
{
    public static class ImporterUtils
    {
        static ImporterUtils()
        {
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new UnixToNullableDateTimeConverter { IsFormatInSeconds = false } }
            };

            // used with Regex.Replace will capture multiple matches at once.. same word or other other words
            // - refer Fuzzy.cs
            _trimAuthorsRegex = new Regex($@"(?<=^|[^a-z^A-Z])({Fuzzy.Authors.StringJoin("|")})(?=$|[^a-zA-Z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _feedFixStatistics = new Dictionary<string, int>();
        }

        public static async Task<ImporterMatchStatistics> MatchOnlineToLocalAsync(List<GameDetail> games, List<OnlineGame> onlineGames, Action<string, float?> updateProgress)
        {
            return await Task.Run(() => MatchOnlineToLocal(games, onlineGames, updateProgress));
        }

        public static async Task MatchLocalToOnlineAsync(List<GameDetail> games, List<OnlineGame> onlineGames, ImporterMatchStatistics matchStatistics, Action<string, float?> updateProgress)
        {
            await Task.Run(() => MatchLocalToOnline(games, onlineGames, matchStatistics, updateProgress));
        }

        public static async Task<List<OnlineGame>> GetOnlineDatabase()
        {
            // create dictionary items upfront to ensure the preferred display ordering (for statistics)
            _feedFixStatistics.Clear();
            _feedFixStatistics.Add(FixTableNameWhitespace, 0);
            _feedFixStatistics.Add(FixTableManufacturerWhitespace, 0);
            _feedFixStatistics.Add(FixManufacturedContainsAuthor, 0);
            _feedFixStatistics.Add(FixTableWrongManufacturer, 0);
            _feedFixStatistics.Add(FixTableWrongName, 0);
            _feedFixStatistics.Add(FixTableMissingImage, 0);
            _feedFixStatistics.Add(FixTableCreatedTime, 0);
            _feedFixStatistics.Add(FixTableUpdatedTimeTooLow, 0);
            _feedFixStatistics.Add(FixTableUpdatedTimeTooHigh, 0);
            _feedFixStatistics.Add(FixFileUpdateTimeOrdering, 0);
            _feedFixStatistics.Add(FixFileUpdatedTime, 0);
            _feedFixStatistics.Add(FixInvalidUrl, 0);
            _feedFixStatistics.Add(FixWrongUrl, 0);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60),
                MaxResponseContentBufferSize = 10 * 1024 * 1024 // 10MB
            };

            var onlineGames = (await httpClient.GetFromJsonAsync<OnlineGame[]>(VisualPinballSpreadsheetDatabaseUrl, _jsonSerializerOptions))!.ToList();

            Logger.Info($"Online database tables:  count={onlineGames.Count} (manufactured={onlineGames.Count(onlineGame => !onlineGame.IsOriginal)}, original={onlineGames.Count(onlineGame => onlineGame.IsOriginal)})");
            return onlineGames;
        }

        public static Dictionary<string, int> FixOnlineDatabase(List<OnlineGame> onlineGames)
        {
            // perform pre-merge fixes, i.e. fixes that do NOT require any duplicate game collections to be merged
            // - some of this information mus be done BEFORE the rest of the game fixing because the duplicate entries must be correctly removed BEFORE the various collections are created
            onlineGames.ForEach(FixPreMerge);

            // merge duplicate entries
            FixDuplicateGames(onlineGames);

            // fix game ordering
            // - alphanumerical
            // - after pre-merge and merged so that the table names are correct and unique
            var orderedDames = onlineGames.OrderBy(game => game.Name).ToArray();
            onlineGames.Clear();
            onlineGames.AddRange(orderedDames);

            // perform post-merge fixes, i.e. fixes that DO require duplicate game collections to be merged
            onlineGames.ForEach((game, index) =>
            {
                game.Index = index + 1;

                // group files into collections so they can be treated generically
                game.AllFiles = new Dictionary<string, FileCollection>
                {
                    { nameof(game.TableFiles), new FileCollection(game.TableFiles) },
                    { nameof(game.B2SFiles), new FileCollection(game.B2SFiles) },
                    { nameof(game.RuleFiles), new FileCollection(game.RuleFiles) },
                    { nameof(game.AltColorFiles), new FileCollection(game.AltColorFiles) },
                    { nameof(game.AltSoundFiles), new FileCollection(game.AltSoundFiles) },
                    { nameof(game.MediaPackFiles), new FileCollection(game.MediaPackFiles) },
                    { nameof(game.PovFiles), new FileCollection(game.PovFiles) },
                    { nameof(game.PupPackFiles), new FileCollection(game.PupPackFiles) },
                    { nameof(game.RomFiles), new FileCollection(game.RomFiles) },
                    { nameof(game.SoundFiles), new FileCollection(game.SoundFiles) },
                    { nameof(game.TopperFiles), new FileCollection(game.TopperFiles) },
                    { nameof(game.WheelArtFiles), new FileCollection(game.WheelArtFiles) }
                };
                game.AllFilesList = game.AllFiles.Select(kv => kv.Value).SelectMany(x => x);
                game.ImageFiles = game.TableFiles.Concat(game.B2SFiles).ToList();

                FixPostMerge(game);

                // copy the dictionary files (potentially re-arranged, filtered, etc) back to the lists to ensure they are in sync
                game.TableFiles = game.AllFiles[nameof(game.TableFiles)].Cast<TableFile>().ToList();
                game.B2SFiles = game.B2SFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.WheelArtFiles = game.WheelArtFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.RomFiles = game.RomFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.MediaPackFiles = game.MediaPackFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.AltColorFiles = game.AltColorFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.SoundFiles = game.SoundFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.TopperFiles = game.TopperFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.PupPackFiles = game.PupPackFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.PovFiles = game.PovFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.AltSoundFiles = game.AltSoundFiles.OrderByDescending(x => x.UpdatedAt).ToList();
                game.RuleFiles = game.RuleFiles.OrderByDescending(x => x.UpdatedAt).ToList();
            });

            return _feedFixStatistics;
        }

        public static OnlineGame GetUniqueGame(List<OnlineGame> onlineGames)
        {
            // create cleansed list of names ordered in ascending size
            var cleansed = onlineGames.Select(x =>
            {
                var cleanName = x.Name.Trim();

                return new
                {
                    name = x.Name,
                    cleanName
                };
            }).OrderBy(x => x.name.Length);

            // unique game is the first in the list item in the cleansed list 
            var uniqueGame = onlineGames.First(onlineGame => onlineGame.Name == cleansed.First().name);

            return uniqueGame;
        }

        private static ImporterMatchStatistics MatchOnlineToLocal(IList<GameDetail> games, ICollection<OnlineGame> onlineGames, Action<string, float?> updateProgress)
        {
            var matchStatistics = new ImporterMatchStatistics();

            onlineGames.ForEach((onlineGame, i) =>
            {
                updateProgress(onlineGame.Name, (i + 1f) / onlineGames.Count);

                // unlike rebuilder matching, only fuzzy is used

                // unlike scanner/rebuilder we already have the manufacturer and year breakdowns, so we can skip the parsing step and assign them directly instead
                // - use GetNameDetails for consistency and assign some properties, but then override with the known values (from the feed) directly
                var fullName = $"{onlineGame.Name} ({onlineGame.Manufacturer} {onlineGame.Year})";
                var fuzzyNameDetails = Fuzzy.GetNameDetails(fullName, false);
                fuzzyNameDetails.ActualName = fullName; // overriden in order to maintain the correct capitalization
                fuzzyNameDetails.Manufacturer = onlineGame.Manufacturer;
                fuzzyNameDetails.Year = onlineGame.Year;

                var (matchedGame, score, isMatch) = games.Match(fuzzyNameDetails, false);
                if (isMatch)
                {
                    var existingMatchOnlineGame = onlineGames.FirstOrDefault(online => online.Hit?.GameDetail == matchedGame);
                    if (existingMatchOnlineGame != null)
                    {
                        var replaceExistingMatch = existingMatchOnlineGame.Hit.Score < score;

                        var isOriginal = existingMatchOnlineGame.IsOriginal || matchedGame.Derived.IsOriginal || fuzzyNameDetails.IsOriginal;
                        var existingFullName = $"{existingMatchOnlineGame.Name} ({existingMatchOnlineGame.Manufacturer} {existingMatchOnlineGame.Year})";
                        
                        var fuzzyLog = $"duplicate fuzzy match: replaceExisting={replaceExistingMatch}, isOriginal={isOriginal}\n" +
                                       $"- db record:                      {Fuzzy.LogGameDetail(matchedGame.Game.Name, matchedGame.Game.Description, matchedGame.Game.Manufacturer, matchedGame.Game.Year)}\n" +
                                       $"- existing feed match: score={$"{existingMatchOnlineGame.Hit.Score},",-4} {Fuzzy.LogGameDetail(existingFullName, null, existingMatchOnlineGame.Manufacturer, existingMatchOnlineGame.YearString)}\n" +
                                       $"- new feed match:      score={$"{score},",-4} {Fuzzy.LogGameDetail(fuzzyNameDetails.ActualName, null, fuzzyNameDetails.Manufacturer, fuzzyNameDetails.Year?.ToString())}";
                        
                        if (!(isOriginal && Model.Settings.SkipLoggingForOriginalTables))
                            Logger.Info(fuzzyLog, true);

                        // if the new match has a greater score..
                        // - Yes = remove the previous hit for the SAME game since it must be wrong
                        //        e.g. onlineGame=Apache initially matches against localGame=Apache! because localDB does not have a 'Apache' game
                        //        .. but subsequently matches higher to onlineGame=Apache! as expected given this is the better (aka correct) match
                        // - No = ignore the match completely since a better match was already found
                        //        e.g. onlineGame=Apache and onlineGame=Apache! should NOT both match to the SAME localGame
                        if (replaceExistingMatch)
                        {
                            // remove match and adjust statistics
                            RemoveMatch(existingMatchOnlineGame);
                            DecrementMatchedStatistics(matchStatistics, existingMatchOnlineGame);
                            IncrementUnmatchedStatistics(matchStatistics, existingMatchOnlineGame);
                            
                            // add new match
                            AddMatch(onlineGame, matchedGame, score);
                        }
                        else
                        {
                            // ignore match - adjust statistics as if there was no match detected.. i.e. a lesser match to the SAME local DB game == effectively not a match at all
                            IncrementUnmatchedStatistics(matchStatistics, onlineGame);
                        }
                    }
                    else
                    {
                        AddMatch(onlineGame, matchedGame, score);
                        IncrementMatchedStatistics(matchStatistics, onlineGame);
                    }
                }
                else
                {
                    IncrementUnmatchedStatistics(matchStatistics, onlineGame);
                }
            });

            return matchStatistics;
        }

        private static void IncrementMatchedStatistics(ImporterMatchStatistics matchStatistics, OnlineGame onlineGame)
        {
            matchStatistics.Increment(ImporterMatchStatistics.MatchedTotal);
            matchStatistics.Increment(onlineGame.IsOriginal ? ImporterMatchStatistics.MatchedOriginal : ImporterMatchStatistics.MatchedManufactured);
        }

        private static void DecrementMatchedStatistics(ImporterMatchStatistics matchStatistics, OnlineGame onlineGame)
        {
            matchStatistics.Decrement(ImporterMatchStatistics.MatchedTotal);
            matchStatistics.Decrement(onlineGame.IsOriginal ? ImporterMatchStatistics.MatchedOriginal : ImporterMatchStatistics.MatchedManufactured);
        }

        private static void IncrementUnmatchedStatistics(ImporterMatchStatistics matchStatistics, OnlineGame onlineGame)
        {
            matchStatistics.Increment(ImporterMatchStatistics.UnmatchedOnlineTotal);
            matchStatistics.Increment(onlineGame.IsOriginal ? ImporterMatchStatistics.UnmatchedOnlineOriginal : ImporterMatchStatistics.UnmatchedOnlineManufactured);
        }

        private static void RemoveMatch(OnlineGame onlineGame)
        {
            onlineGame.Hit = null;
        }

        private static void AddMatch(OnlineGame onlineGame, GameDetail matchedGame, int? score)
        {
            onlineGame.Hit = new GameHit
            {
                GameDetail = matchedGame,
                Score = score
            };
        }

        private static void MatchLocalToOnline(IEnumerable<GameDetail> gameDetails, IEnumerable<OnlineGame> onlineGames, ImporterMatchStatistics matchStatistics, Action<string, float?> updateProgress)
        {
            var unmatchedGameDetails = gameDetails.Except(onlineGames.Where(onlineGame => onlineGame.Hit != null).Select(onlineGame => onlineGame.Hit.GameDetail)).ToList();

            // deliberately NOT performing a 'reverse' fuzzy lookup to avoid scenario where x1 online game could have multiple local files
            // - e.g. online only has 1 AC/DC entry (which is a known issue).. whereas there are multiple local files each representing the unique IPDBs (which is correct)
            unmatchedGameDetails.ForEach(localGameDetail =>
            {
                updateProgress(localGameDetail.Game.Name, null);

                Logger.Info($"Unmatched local table: '{localGameDetail.Game.Name}'");
                
                matchStatistics.Increment(ImporterMatchStatistics.UnmatchedLocalTotal);
                matchStatistics.Increment(localGameDetail.Derived.IsOriginal ? ImporterMatchStatistics.UnmatchedLocalOriginal : ImporterMatchStatistics.UnmatchedLocalManufactured);
            });
        }

        // fixes that do NOT require the collections to be initialized (which must occur after de-duplicating, aka merging)
        private static void FixPreMerge(OnlineGame onlineGame)
        {
            // fix named games
            FixNamedGames(onlineGame);

            // fix game name - remove whitespace
            if (onlineGame.Name != onlineGame.Name.Trim())
            {
                LogFixed(onlineGame, FixTableNameWhitespace);
                onlineGame.Name = onlineGame.Name.Trim();
            }

            // fix manufacturer - remove whitespace
            if (onlineGame.Manufacturer != onlineGame.Manufacturer.Trim())
            {
                LogFixed(onlineGame, FixTableManufacturerWhitespace, $"manufacturer='{onlineGame.Manufacturer}'");
                onlineGame.Manufacturer = onlineGame.Manufacturer.Trim();
            }

            // remove author of the game for manufactured tables
            // - e.g. JP's Captain Fantastic (Bally 1976)
            if (!GameDerived.CheckIsOriginal(onlineGame.Manufacturer, onlineGame.Name) && _trimAuthorsRegex.IsMatch(onlineGame.Name))
            {
                var cleanName = _trimAuthorsRegex.Replace(onlineGame.Name, "").Trim();
                LogFixed(onlineGame, FixManufacturedContainsAuthor, $"correct='{cleanName}, manufacturer='{onlineGame.Manufacturer}'");
                onlineGame.Name = cleanName;
            }

            // fix invalid IPDB Url
            // - e.g. "Not Available" frequently used for original tables
            if (!(Uri.TryCreate(onlineGame.IpdbUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            {
                LogFixed(onlineGame, FixInvalidIpdbUrl, $"url={onlineGame.IpdbUrl}");
                onlineGame.IpdbUrl = null;
            }

            // fix (technically upgrade) url to use https instead of http
            if (Uri.TryCreate(onlineGame.IpdbUrl, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttp)
            {
                var uriBuilder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1};
                onlineGame.IpdbUrl = uriBuilder.Uri.AbsoluteUri;
            }

            // fix wrong IPDB url
            // - original tables shouldn't reference a manufactured table.. but sometimes happens as a reference to the inspiration table
            if (onlineGame.IsOriginal && onlineGame.IpdbUrl != null) 
                FixGameWrongIpdbUrl(onlineGame, null);
        }

        private static void FixNamedGames(OnlineGame onlineGame)
        {
            // non-generic fixes for specifically named games
            // - this is very smelly, but treating these as 'exceptional' (and hopefully few!) scenarios, similar to GameDerived.CheckIsOriginal
            // - todo; report/fix the underlying VPS feed and then remove this code
            switch (onlineGame.Description)
            {
                case "Austin Powers (Stern 2001)":
                    FixGameWrongIpdbUrl(onlineGame, "https://www.ipdb.org/machine.cgi?id=4504");
                    break;
                case "JP's Dale Jr. Nascar (Original 2020)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=5292", "Stern", 2007);
                    break;
                case "JP'S Nascar Race (Original 2005)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=5093", "Stern");
                    break;
                case "JP's Grand Prix (Original 2005)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=5120", "Stern");
                    break;
                case "JP's Lord Of The Rings (Original 2003)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=4858", "Stern");
                    break;
                case "JP's Motor Show (Original 1989)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=3631", "Mr. Game");
                    break;
                case "JP's Spider-Man (Original 2011)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=5237", "Stern", 2007);
                    break;
                case "Siggi's Spider-Man Classic (Stern 2016)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=6328", "Stern", 2016, "Spider-Man (Vault Edition)");
                    break;
                case "JP's Street Fighter 2 (Original 1993)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=2403", "Gottlieb");
                    break;
                case "JP's Terminator 2 (Original 2020)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=2524", "Williams", 1991, "Terminator 2 Judgment Day");
                    break;
                case "JP's Transformers (Original 2011)":
                    FixGame(onlineGame, "https://www.ipdb.org/machine.cgi?id=5709", "Stern");
                    break;
                case "Phychedelic (Gottlieb 1970)":
                    FixGameWrongName(onlineGame, "Psychedelic");
                    break;
                case "Martian Queen (LTD ) (LTD 0)":
                    FixGameWrongName(onlineGame, "Martian Queen");
                    FixGameWrongManufacturer(onlineGame, "LTD do Brasil Diverses Eletrnicas Ltda", 1981);
                    break;
            }
        }

        private static void FixGameWrongIpdbUrl(OnlineGameBase onlineGame, string ipdbUrl)
        {
            LogFixed(onlineGame, FixWrongIpdbUrl, $"old url={onlineGame.IpdbUrl}, new url={ipdbUrl}");
            onlineGame.IpdbUrl = ipdbUrl;
        }
        
        private static void FixGameWrongName(OnlineGameBase onlineGame, string name)
        {
            LogFixed(onlineGame, FixTableWrongName, $"new name={name}");
            onlineGame.Name = name;
        }

        private static void FixGameWrongManufacturer(OnlineGameBase onlineGame, string manufacturer, int? year = null)
        {
            LogFixed(onlineGame, FixTableWrongManufacturer, $"old manufacturer={onlineGame.Manufacturer}, new manufacturer={manufacturer}");
            onlineGame.Manufacturer = manufacturer;
            onlineGame.Year = year ?? onlineGame.Year;
        }

        private static void FixGame(OnlineGameBase onlineGame, string ipdbUrl, string manufacturer, int? year = null, string name = null)
        {
            // assign correct IPDB url and manufacturer
            // - if the game already exists, then it will be picked up later as a duplicate
            FixGameWrongIpdbUrl(onlineGame, ipdbUrl);

            FixGameWrongManufacturer(onlineGame, manufacturer, year);

            if (name != null) 
                FixGameWrongName(onlineGame, name);
        }

        private static void FixDuplicateGames(ICollection<OnlineGame> onlineGames)
        {
            // duplicate games are determined by whether entries are have duplicate IPDB url references
            // - only works for manufactured tables of course
            // - e.g. Star Trek and JP's Star Trek share the same IPDB url
            var duplicateGames = onlineGames.Where(game => !game.IpdbUrl.IsEmpty()).GroupBy(game => game.IpdbUrl).Where(x => x.Count() > 1).ToList();

            duplicateGames.ForEach(grouping =>
            {
                // assign the unique and duplicate(s)
                var game = GetUniqueGame(grouping.ToList());
                var duplicates = grouping.Except(game).ToList();

                LogFixed(game, FixDuplicateGame, $"duplicate table(s)={duplicates.Select(x => x.Description).StringJoin()}");

                Logger.Warn($"Merging duplicate tables detected in the online feed, IPDB url: {grouping.Key}\n" +
                            $"- unique:    {game}\n" +
                            $"- duplicate: {duplicates.Select(x => x.Description).StringJoin()}");

                // process the duplicates
                duplicates.ForEach(duplicate =>
                {
                    // merge games collections
                    game.TableFiles.AddRange(duplicate.TableFiles);
                    game.B2SFiles.AddRange(duplicate.B2SFiles);
                    game.WheelArtFiles.AddRange(duplicate.WheelArtFiles);
                    game.RomFiles.AddRange(duplicate.RomFiles);
                    game.MediaPackFiles.AddRange(duplicate.MediaPackFiles);
                    game.AltColorFiles.AddRange(duplicate.AltColorFiles);
                    game.SoundFiles.AddRange(duplicate.SoundFiles);
                    game.TopperFiles.AddRange(duplicate.TopperFiles);
                    game.PupPackFiles.AddRange(duplicate.PupPackFiles);
                    game.AltSoundFiles.AddRange(duplicate.AltSoundFiles);
                    game.RuleFiles.AddRange(duplicate.RuleFiles);

                    // remove duplicate
                    onlineGames.Remove(duplicate);
                });
            });
        }

        private static void FixPostMerge(OnlineGame onlineGame)
        {
            // fix image url - assign to the first available image url.. B2S then table
            if (onlineGame.ImgUrl == null)
            {
                var imageUrl = onlineGame.B2SFiles.FirstOrDefault(x => x.ImgUrl != null)?.ImgUrl ?? onlineGame.TableFiles.FirstOrDefault(x => x.ImgUrl != null)?.ImgUrl;
                if (imageUrl != null)
                {
                    LogFixed(onlineGame, FixTableMissingImage, $"url='{imageUrl}'");
                    onlineGame.ImgUrl = imageUrl;
                }
            }

            // fix updated timestamp - must not be lower than the created timestamp
            onlineGame.AllFiles.ForEach(kv =>
            {
                kv.Value.Where(f => f.UpdatedAt < f.CreatedAt).ForEach(f =>
                {
                    LogFixedTimestamp(onlineGame, FixFileUpdatedTime, "updatedAt", f.UpdatedAt, "   createdAt", f.CreatedAt);
                    f.UpdatedAt = f.CreatedAt;
                });
            });

            // fix game created timestamp - must not be less than any file timestamps
            var maxCreatedAt = onlineGame.AllFilesList.Max(x => x.CreatedAt);
            if (onlineGame.LastCreatedAt < maxCreatedAt)
            {
                LogFixedTimestamp(onlineGame, FixTableCreatedTime, "createdAt", onlineGame.LastCreatedAt, nameof(maxCreatedAt), maxCreatedAt);
                onlineGame.LastCreatedAt = maxCreatedAt;
            }

            // fix game updated timestamp - must not be less than the max file timestamp
            var maxUpdatedAt = onlineGame.AllFilesList.Max(x => x.UpdatedAt);
            if (onlineGame.UpdatedAt < maxUpdatedAt)
            {
                LogFixedTimestamp(onlineGame, FixTableUpdatedTimeTooLow, "updatedAt", onlineGame.UpdatedAt, nameof(maxUpdatedAt), maxUpdatedAt);
                onlineGame.UpdatedAt = maxUpdatedAt;
            }
            else if (onlineGame.UpdatedAt > maxUpdatedAt)
            {
                LogFixedTimestamp(onlineGame, FixTableUpdatedTimeTooHigh, "updatedAt", onlineGame.UpdatedAt, nameof(maxUpdatedAt), maxUpdatedAt, true);
                onlineGame.UpdatedAt = maxUpdatedAt;
            }

            // fix file ordering - ensure a game's most recent files are shown first
            onlineGame.AllFiles.ForEach(kv =>
            {
                var orderByDescending = kv.Value.OrderByDescending(x => x.UpdatedAt).ToArray();
                if (!kv.Value.SequenceEqual(orderByDescending))
                {
                    LogFixed(onlineGame, FixFileUpdateTimeOrdering, $"type={kv.Key}");
                    kv.Value.Clear();
                    kv.Value.AddRange(orderByDescending);
                }
            });

            // fix urls
            onlineGame.AllFiles.ForEach(kv =>
            {
                kv.Value.ForEach(f =>
                    f.Urls.ForEach(urlDetail =>
                    {
                        // fix urls - mark any invalid urls, e.g. Abra Ca Dabra ROM url is a string warning "copyright notices"
                        if (!urlDetail.Broken && !(Uri.TryCreate(urlDetail.Url, UriKind.Absolute, out var generatedUrl) && (generatedUrl.Scheme == Uri.UriSchemeHttp || generatedUrl.Scheme == Uri.UriSchemeHttps)))
                        {
                            LogFixed(onlineGame, FixInvalidUrl, $"type={kv.Key} url={urlDetail.Url}");
                            urlDetail.Broken = true;
                        }

                        // fix vpuniverse urls - path
                        if (urlDetail.Url?.Contains("//vpuniverse.com/forums") == true)
                        {
                            LogFixed(onlineGame, FixWrongUrl, $"type={kv.Key} url={urlDetail.Url}");
                            urlDetail.Url = urlDetail.Url.Replace("//vpuniverse.com/forums", "//vpuniverse.com");
                        }
                    })
                );
            });
        }

        private static void LogFixedTimestamp(OnlineGameBase onlineGame, string type, string gameTimeName, DateTime? gameTime, string maxFileTimeName, DateTime? maxFileTime, bool greaterThan = false)
        {
            LogFixed(onlineGame, type, $"game.{gameTimeName} '{gameTime:dd/MM/yy HH:mm:ss}' {(greaterThan ? ">" : "<")} {maxFileTimeName} '{maxFileTime:dd/MM/yy HH:mm:ss}'");
        }

        private static void LogFixed(OnlineGameBase onlineGame, string type, string details = null)
        {
            AddFixStatistic(type);

            var name = $"'{onlineGame.Name[..Math.Min(onlineGame.Name.Length, 35)].Trim()}'";
            Logger.Warn($"Fixed {type,-35} name={name,-35} {details}", true);
        }

        private static void AddFixStatistic(string key)
        {
            _feedFixStatistics.TryAdd(key, 0);
            _feedFixStatistics[key]++;
        }

        // refer https://github.com/Fraesh/vps-db, https://virtual-pinball-spreadsheet.web.app/
        private const string VisualPinballSpreadsheetDatabaseUrl = "https://raw.githubusercontent.com/Fraesh/vps-db/master/vpsdb.json";

        private const string FixTableNameWhitespace = "Table Name Whitespace";
        private const string FixTableMissingImage = "Table Missing Image Url";
        private const string FixTableManufacturerWhitespace = "Table Manufacturer Whitespace";
        private const string FixManufacturedContainsAuthor = "Manufacturered Contains Author";
        private const string FixTableWrongManufacturer = "Table Wrong Manufacturer";
        private const string FixTableWrongName = "Table Wrong Name";
        private const string FixTableCreatedTime = "Table Created Time";
        private const string FixTableUpdatedTimeTooLow = "Table Updated Time Too Low";
        private const string FixTableUpdatedTimeTooHigh = "Table Updated Time Too High";
        private const string FixFileUpdateTimeOrdering = "File Update Time Ordering";
        private const string FixFileUpdatedTime = "File Updated Time";
        private const string FixInvalidUrl = "Invalid Url";
        private const string FixWrongUrl = "Wrong Url";
        private const string FixInvalidIpdbUrl = "Invalid IPDB Url";
        private const string FixWrongIpdbUrl = "Wrong IPDB Url";
        private const string FixDuplicateGame = "Duplicate Table";

        private static readonly JsonSerializerOptions _jsonSerializerOptions;
        private static readonly Dictionary<string, int> _feedFixStatistics;
        private static readonly Regex _trimAuthorsRegex;
    }
}
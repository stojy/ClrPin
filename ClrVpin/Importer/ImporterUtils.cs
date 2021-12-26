﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ClrVpin.Importer.Vps;
using Utils;

namespace ClrVpin.Importer
{
    internal static class ImporterUtils
    {
        static ImporterUtils()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60),
                MaxResponseContentBufferSize = 10 * 1024 * 1024
            };

            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new UnixToNullableDateTimeConverter {IsFormatInSeconds = false} }
            };
        }

        public static async Task<Game[]> GetOnlineDatabase()
        {
            var games =  await _httpClient.GetFromJsonAsync<Game[]>(VisualPinballSpreadsheetDatabaseUrl, _jsonSerializerOptions);

            // add VM properties.. todo; move elsewhere
            games.ForEach(game =>
            {
                // patch the data.. assign a top level image url if one doesn't already exist
                game.ImgUrl ??= game.B2SFiles.FirstOrDefault(x => x.ImgUrl != null)?.ImgUrl ?? game.TableFiles.FirstOrDefault(x => x.ImgUrl != null)?.ImgUrl;
            });

            return games;
        }

        // refer https://github.com/Fraesh/vps-db, https://virtual-pinball-spreadsheet.web.app/
        private const string VisualPinballSpreadsheetDatabaseUrl = "https://raw.githubusercontent.com/Fraesh/vps-db/master/vpsdb.json";

        private static readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions _jsonSerializerOptions;
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Linq;

namespace plexport
{
    class Library {
        public string Title;
        public string Type;
        public string Key;
    }

    class LibraryItem {
        [CsvSerializer.ColumnOrder(1)]
        public string Title;
        [CsvSerializer.ColumnOrder(2)]
        public int Year;
        [CsvSerializer.ColumnOrder(3)]
        public string Resolution;
        [CsvSerializer.ColumnOrder(4)]
        public decimal AspectRatio;
        [CsvSerializer.ColumnOrder(5)]
        public int RecordedItems;
        [CsvSerializer.ColumnOrder(6)]
        public int NonRecordedItems;
    }

    class PlexExport
    {
        private static readonly HttpClient client = new HttpClient();
        public string PlexToken = "";
        public bool UseHTTPS = false;
        public string PlexServer = "127.0.0.1";
        public string PlexServerPort = "32400";

        public PlexExport() {            
            client.DefaultRequestHeaders
                .Accept
                .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }
        
        private async Task<JObject> GetPlexResponse(string key) {
            var path = String.Format("{0}:{1}/{2}?X-Plex-Token={3}",PlexServer, PlexServerPort, key, PlexToken);
            path = "http" + (UseHTTPS ? "s" : "") + "://" + path.Replace("//", "/");
            var stringTask = client.GetStringAsync(path);
            var msg = await stringTask;
            return JObject.Parse(msg);
        }

        public async Task<List<Library>> GetLibraries() {
            List<Library> libraries = new List<Library>();
            var response = await GetPlexResponse("library/sections");
            var directory = response["MediaContainer"]["Directory"];

            foreach(var item in directory) {
                Library library = new Library();
                library.Type = item["type"].ToString();
                library.Title = item["title"].ToString();
                library.Key = item["key"].ToString();

                libraries.Add(library);
            }

            return libraries;
        }

        public async Task<List<LibraryItem>> GetLibraryItems(Library library) {
            List<LibraryItem> libraryItems = new List<LibraryItem>();
            var response = await GetPlexResponse("library/sections/" + library.Key + "/all");
            var metadata = response["MediaContainer"]["Metadata"];

            foreach(var item in metadata) {

                if (library.Type == "show") {
                    List<LibraryItem> showSeasons = await GetShowSeasons(item["key"].ToString());
                    libraryItems.AddRange(showSeasons);
                }
                else if (library.Type == "movie") {
                    foreach(var media in item["Media"]) {
                        LibraryItem libraryItem = new LibraryItem();
                        libraryItem.Title = item["title"].ToString();
                        libraryItem.Year = Convert.ToInt32(item["year"]);
                        
                        if (media["videoResolution"] == null 
                            || media["aspectRatio"] == null) {
                            continue;
                        }
                        
                        foreach(var part in media["Part"])
                        {
                            if (part["file"] != null && part["file"].ToString().Contains("recordings")) {                            
                                libraryItem.RecordedItems = 1;
                                break;
                            }
                        }

                        libraryItem.NonRecordedItems = libraryItem.RecordedItems > 0 ? 0 : 1;                            
                        libraryItem.Resolution = media["videoResolution"].ToString();
                        libraryItem.AspectRatio = Convert.ToDecimal(media["aspectRatio"]);
                        if (libraryItem.Resolution == "sd") { libraryItem.Resolution = "480"; }
                        libraryItems.Add(libraryItem);
                    }
                }
            }

            return libraryItems;
        }

        private async Task<List<LibraryItem>> GetShowSeasons(string key) {
            List<LibraryItem> showSeasons = new List<LibraryItem>();
            var response = await GetPlexResponse(key);
            var metadata = response["MediaContainer"]["Metadata"];

            foreach(var season in metadata) {
                showSeasons.Add(await GetShowSeason(season["key"].ToString()));
            }

            return showSeasons;
        }

        private async Task<LibraryItem> GetShowSeason(string key) {
            LibraryItem showSeason = new LibraryItem();
            var response = await GetPlexResponse(key);
            var metadata = response["MediaContainer"]["Metadata"];
            showSeason.Title = response["MediaContainer"]["title1"].ToString() + " (" + response["MediaContainer"]["title2"].ToString() + ")";

            foreach(var episode in metadata) {
                if (showSeason.Year == 0 && episode["year"] != null) {
                    Int32.TryParse(episode["year"].ToString(), out showSeason.Year);
                }

                bool isRecorded = false;
                foreach(var media in episode["Media"]) {
                    if (showSeason.Resolution == null && media["videoResolution"] != null) { 
                        showSeason.Resolution = media["videoResolution"].ToString();
                        if (showSeason.Resolution == "sd") { showSeason.Resolution = "480"; }
                    }
                    if (showSeason.AspectRatio == 0 && media["aspectRatio"] != null) { 
                        showSeason.AspectRatio = Convert.ToDecimal(media["aspectRatio"]); 
                    }
                    
                    if (isRecorded) continue;
                    foreach(var part in media["Part"]) {
                        if (part["file"] != null && part["file"].ToString().Contains("recordings")) { 
                            isRecorded = true;
                            break;
                        } 
                    }
                }
                
                if (isRecorded) {
                    showSeason.RecordedItems++;
                }
                else {
                    showSeason.NonRecordedItems++;
                }
            }

            return showSeason;
        }
    }

    //https://stackoverflow.com/a/3569285/30838
    public static class CsvSerializer {
        /// <summary>
        /// Serialize objects to Comma Separated Value (CSV) format [1].
        /// 
        /// Rather than try to serialize arbitrarily complex types with this
        /// function, it is better, given type A, to specify a new type, A'.
        /// Have the constructor of A' accept an object of type A, then assign
        /// the relevant values to appropriately named fields or properties on
        /// the A' object.
        /// 
        /// [1] http://tools.ietf.org/html/rfc4180
        /// </summary>
        public static void Serialize<T>(TextWriter output, IEnumerable<T> objects) {
            var fields =
                from mi in typeof (T).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                where new [] { MemberTypes.Field, MemberTypes.Property }.Contains(mi.MemberType)
                let orderAttr = (ColumnOrderAttribute) Attribute.GetCustomAttribute(mi, typeof (ColumnOrderAttribute))
                orderby orderAttr == null ? int.MaxValue : orderAttr.Order, mi.Name
                select mi;
            output.WriteLine(QuoteRecord(fields.Select(f => f.Name)));
            foreach (var record in objects) {
                output.WriteLine(QuoteRecord(FormatObject(fields, record)));
            }
        }

        static IEnumerable<string> FormatObject<T>(IEnumerable<MemberInfo> fields, T record) {
            foreach (var field in fields) {
                if (field is FieldInfo) {
                    var fi = (FieldInfo) field;
                    yield return Convert.ToString(fi.GetValue(record));
                } else if (field is PropertyInfo) {
                    var pi = (PropertyInfo) field;
                    yield return Convert.ToString(pi.GetValue(record, null));
                } else {
                    throw new Exception("Unhandled case.");
                }
            }
        }

        const string CsvSeparator = ",";

        static string QuoteRecord(IEnumerable<string> record) {
            return String.Join(CsvSeparator, record.Select(field => QuoteField(field)).ToArray());
        }

        static string QuoteField(string field) {
            if (String.IsNullOrEmpty(field)) {
                return "\"\"";
            } else if (field.Contains(CsvSeparator) || field.Contains("\"") || field.Contains("\r") || field.Contains("\n")) {
                return String.Format("\"{0}\"", field.Replace("\"", "\"\""));
            } else {
                return field;
            }
        }

        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
        public class ColumnOrderAttribute : Attribute {
            public int Order { get; private set; }
            public ColumnOrderAttribute(int order) { Order = order; }
        }
    }
}

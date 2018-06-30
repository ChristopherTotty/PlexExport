using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace plexport
{
    class Program
    {
        static void Main(string[] args)
        {
            PlexExport pe = new PlexExport();

            Task<List<Library>> libraryTask = pe.GetLibraries();
            libraryTask.Wait();
            List<Library> libraries = libraryTask.Result;

            Library selectedLibrary = PromptForLibrarySelection(libraries);

            Task<List<LibraryItem>> itemTask = pe.GetLibraryItems(selectedLibrary);
            itemTask.Wait();
            List<LibraryItem> items = itemTask.Result;

            string exportFile = selectedLibrary.Title + " Library Export (" + DateTime.Now.ToString("yyyy-MM-dd") + ").csv";
            using (TextWriter writer = File.CreateText(exportFile)) {
                CsvSerializer.Serialize(writer, items);
            }
            Console.WriteLine("Successfully Exported: " + exportFile);
            Console.ReadLine();
        }

        static Library PromptForLibrarySelection(List<Library> libraries) {
            Console.WriteLine("Select a Library:");
            for (var i = 0; i < libraries.Count; i++) {
                var library = libraries[i];
                Console.WriteLine((i + 1).ToString() + " - " + library.Title);
            }

            int libraryIndex;
            if(Int32.TryParse(Console.ReadLine(), out libraryIndex)) {
                if(libraryIndex <= libraries.Count && libraryIndex > 0) {
                    return libraries[libraryIndex - 1];
                }
            }
            
            return PromptForLibrarySelection(libraries);
        }
    }
}

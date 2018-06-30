# PlexExport

A small command line utility written in C# to export a CSV list of items from a Plex Media Server library.

I wrote this so that I could easily export a list of movies and tv shows to use as a reference to minimize duplicates while out shopping for new material.

Check options for Plex Server, Port, and Token before running.

Currently supports exporting Title/Season, Year, Resolution, and Aspect Ratio for Movies and TV Shows.

Additionally, exports recorded vs non-recorded info (based on source being in a directory containing "recordings").

---
## Future Enhancements:
1. Include console switches for automated runs
2. Explore a better method of determining source for DVR flag
3. Optional authentication with Plex.tv using user/pass to retreive token
4. Error handling
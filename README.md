# Lumidex

Organize your astrophotography library.

Lumidex scans folders with your image files, reads the headers, and builds a database.
You original data is never altered.

## Features
- Windows and MacOS support
  - Debian-based linux distros should also work but no dpkg is available
- .FITS and .XISF files
- Multiple libraries support scanning files in multiple locations
- Search -> Search your library to find the images you're looking for
- Aliases -> Assign multiple names to object names from the headers
- Tags -> Group your images with tags
- Quick Scan -> only scan files added since the last scan
- Statistics -> See your total integration time and breakdown by filter
- Advanced Filters -> Search the database by most header fields
- Image Export -> Copy images to a destination folder
- Astrobin Export -> Export acquisition details in Astrobin CSV format
- Duplicate Detection -> See a list of duplicate images so you can track them down
- Edit Data -> Edit the database contents for images so you have consistent data (does not alter files)

## Get Lumidex

Check out the [releases](https://github.com/alexhelms/lumidex/releases) to download Lumidex.

## Screenshots

### Search
![Search](/assets/lumidex_search.png?raw=true "Search")

### Aliases
![Alias](/assets/lumidex_aliases.png?raw=true "Aliases")

### Tags
![Tags](/assets/lumidex_tags.png?raw=true "Tags")

### Library
![Library](/assets/lumidex_library.png?raw=true "Library")

### Astrobin Export
![Astrobin Export](/assets/lumidex_astrobin_export.png?raw=true "Astrobin Export")

### File Export
![File Export](/assets/lumidex_file_export.png?raw=true "File Export")

## High CPU Usage During Scan in Windows

There can be very high CPU usage from Windows Security (MsMpEng.exe) when scanning.
The nature of Lumidex, scanning and hashing files, looks a lot like malware and triggers false positives. 
This high CPU usage can dramatically slow down scanning.

You can add `Lumidex.Desktop.exe` as a Process exclusion in Windows Security to mitigate the high CPU usage,
but this is certainly an added security risk -- only you can decide if it is worth it.

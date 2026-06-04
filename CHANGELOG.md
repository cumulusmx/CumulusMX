# Changelog

All notable changes to this project will be documented in this file.

Additional notes are available on the [forum release thread](https://cumulus.hosiene.co.uk/viewtopic.php?t=24075)

This file is formatted as [markdown](https://www.markdownguide.org/), any decent editor should display it correctly formatted.<br>
Alternatively view it [online on GitHub](https://github.com/cumulusmx/CumulusMX/blob/b5001/CHANGELOG.md)

---
---

## [6.0.0 \[b6000\]][60] - 2026-10-01

### New

- Now supports the concept of primary and secondary stations. This replaces the previous Main and Extra Sensors. The changes are:
	- You can now map any sensor type from either the primary or secondary station to be used for your weather data
	- Where multiple sensors are supported (eg extra temp/hum) you can now pick which sensor from each station is to be used
	- If the secondary station supports historic catch-up then catch-up will be performed across both stations. Note this may slow the catch-up process slightly
	- The sensor mapping between primary and secondary is provided on a new settings screen: ***Settings | Sensor Maps***

### Changed

- The station changes have required an extensive rewrite of much of the station code

### Fixed

- Nothing

### Package Updates

- SixLabors.ImageSharp [REMOVED] - New versions require keys to compile, even for free licences
- SkiaSharp [NEW] - Replaces SixLabors

---

## [5.1.4 \[b5011\]][55] - 2026-06-26

### New

- Add web tag for the Cumulus dashboard/web socket port - `<#CumulusPort>`
- Adds a new station option for Cumulus calculates WBGT<br>
	You can use this if say you have a BGT value from your extra sensors, but want to use the outdoor temperature/humidity from your main station

### Changed

- The dashboard web socket connections are now protocol independent
- Dashboard BGT/WBGT values moved from the Extra Sensors page to the Now page

### Fixed

- The new web tag `<#EcowittRssi>` that currently supports the Ecowitt HTTP API station
- Error decoding Windy error responses
- Fix inconsistent error responses from wl.com causing an exception
- Fix reversion error in FTP Watchdog where the test file was being created in the root FTP folder

---

## [5.1.3 \[b5010\]][54] - 2026-05-26

Version 5.1.3 also addresses some issues identified in the withdrawn 5.1.2 release

- Davis stations not starting correctly if "Use data logger" is disabled, or WeatherLink.com credentials missing


### New

- Adds a new web tag `<#EcowittRssi>` that currently supports the Ecowitt HTTP API station. It lists the RSSI value for each sensor - if available<br>
	Like the `<#EcowittReception>` web tag it supports a `format=xxx` parameter which affects the output, valid values are "text" or "json". The default is a text string

### Changed

- Softer start to Cumulus calculates average wind speed when number of samples < 10

### Fixed

- Ecowitt HTTP API station reception stats, WH40 was being reported as WH25
- Fix BGT/WBGT today/yesterday web tags
- Non-present WBGT values being written to the dayfile as -999
- Race condition in RealTimeFtpWatchDog and a System.ObjectDisposed Exception after the first disconnect/reconnect
- Fixes Davis VP2/Vue stations not starting and hanging MX if either: Use data logger = disabled, or Today.ini missing (e.g. first clean run of MX)
- JSON station set soil moisture units per sensor as received units

### Package Updates

- Microsoft.Win32.SystemEvents
- NLog
- NLog.Extensions.Logging
- SQLitePCLRaw.bundle_e_sqlite3
- System.CodeDom
- System.IO.Ports
- System.ServiceProcess.ServiceController

---

## [5.1.1 \[b5007\]][53] - 2026-06-07

### New

- Nothing

### Changed

- Nothing

### Fixed

- Spurious "y_temp" after Humidex values in charts tooltips
- Spelling errors of "minimum" in three historic temperature data series
- Updating MySQL when editing monthly log file entries
- Main station not starting correctly when using Ecowitt Cloud station for Extra Sensors
- Add missing rollover processing for BGT/WBGT high values
- Add BGT/WBGT to all time record detection
- Add new web tags for BGT/WBGT all-time record set<br>
`<#HighBgtRecordSet> <#HighWbgtRecordSet>`



## [5.1.0 \[b5006\]][52] - 2026-05-07

### Important Notes

- **MySQL Users:** Because the dayfile has some extra fields, you must update your MySQL table for this release. Use the Update Table feature in Cumulus MySQL Settings
- **PHP Upload:** There is an important update to the `upload.php` script


### New

- High records added for BGT and WBGT values
	- Updated dashboard records displays
	- Updated records editors
	- Add to Locale Strings and Display Options
	- Data file editors amended
	- Day file now has four extra fields to store these values and their times
	- MySQL update dayfile table for the new BGT and WBGT high values and times columns
	```
	HighBgt decimal(5,1)
	THighBgt varchar(5)
	HighWbgt decimal (5,1)
	THighWbgt varchar(5)
	```
	- File header files updated
	- Dashboard and default web site Historic charts updated
	- New web tags<br>
	`<#BgtTH> <#TBgtTH>`
	`<#BgtYH> <#TBgtYH>`
	`<#BgtH> <#TBgtH>`
	`<#MonthBgtH> <#MonthBgtHT> <#MonthBgtHD>`
	`<#YearBgtH> <#YearBgtHT>`
	`<#ByMonthBgtH> <#ByMonthBgtHT>`
	<br>
	`<#WbgtTH> <#TWbgtTH>`
	`<#WbgtYH> <#TWbgtYH>`
	`<#WbgtH> <#TWbgtH>`
	`<#MonthWbgtH> <#MonthWbgtHT> <#MonthWbgtHD>`
	`<#YearWbgtH> <#YearWbgtHT>`
	`<#ByMonthWbgtH> <#ByMonthWbgtHT>`
- Adds support for Soil Electrical Conductivity to Ecowitt Local HTTP API station, Ecowitt.net cloud station, and Ecowitt HTTP Station
	- Also supported on the following Extra Sensor stations: Ecowitt Cloud and Ecowitt HTTP Station
	- Sixteen new web tags
	`<#SoilEC[1-16]>`
	- Values 1-16 appended to the extra log file
	- Extra log file header updated
	- Add to Locale Strings and Display Options
	- New graph data file - `soilecdata.json`
	- Added to Dashboard Recent, Select-a-Period and Recent Select-a-Chart charts
	- Added to default web site recent charts
- Adds support for the WS3900/WN1800 console built-in CO₂ readings to the Ecowitt HTTP Station

### Changed

- The path setting for the MXdiags folder has been moved to *Program Setting > Path Options*, and stored in the Cumulus.ini file so it will persist across upgrades
- New version of CreateMissing (v3.1.0) to add BGT/WBGT support
- Updated versions of `ImportCumulusFile.php` and `ImportCumulusFile.py`
- Additions to the Interval Data Viewer
	- Extra Temperature 11-16
	- Extra Humidity 11-16
	- Extra Dew Point 11-16
	- Air Quality PM10 values
	- Laser Distance 1-4
	- Soil EC 1-16
	- Current Snowfall in 24 hours value
- FTP logging. To match the main MXdiags log files, the latest Realtime and Interval logs are now always called `ftp-realtime.log` and `ftp-interval.log`. Rolled over logs will have a date/time appended

### Fixed

- Issue with `upload.php` that allowed incrementally appended JSON files to grow without trimming old data
- Fix the standard web file websitedata.json being created in the root folder with a filename prefix of "web". Missed in v5.0.1
- Forecast issues introduced in v5.0.1:
	- Davis station no longer storing the station forecast when primary source is Cumulus or forecast.txt
	- Cumulus forecast and forecast number not being stored if the forecast source was other than Cumulus
	- Forecast string could be uninitialised if the associated web tags were call at start-up and before MX had processed the first forecast event
- Snow hour not saving in Station Settings
- JSON Station MQTT connection reconfigured
	- The on start-up the station now attempts to make the server connection indefinitely
	- Refactored the reconnection on connection loss for better execution and logging
- JSON Station add missing handling of "airquality" PM 10 values
- JSON Station fix error in BGT temperature if no "temperature" object in message
- Fix default web site Humidity Trends charts error
- Fix Simulator crash during write of monthly log file in initial day reset on catch-up
- Missing Air Quality block from the dashboard Extra Sensors page
- Extra Sensors using Ecowitt Cloud Station was not updating Solar & UV-I values
- Fixes multiple web cam URL support
	- Adds the `camera` parameter to the `<#webcam>` and `<#webcamurl>` web tags, if omitted it defaults to "1" the first defined camera
	- Eg `<#webcamurl camera=2>`
- Davis Cloud station error on decoding VP2/Vue originated current data, it also adds the Davis forecast decoding to this model
- Enabling a Purple Air AQ sensor now automatically enables the extra sensor to use AQ feature
- Fix monthly log editor MySQL updates
- Fix exception handling Davis v2 API error responses
- Davis WLL stations now check the subscription level before fetching health data
- Locally created `snow24hdata.json` files were misnamed snow24data.json

### Package Updates

- FluentFTP
- MailKit
- Microsoft.Win32.SystemEvents
- NLog
- System.CodeDom
- System.IO.Ports
- System.Serviceprocess.ServiceController

---

## [5.0.1 \[b5002\]][51] - 2026-04-03

### New

- Adds support for supply external forecast text via a file `forecast.txt` in the root folder
- Adds a new web tag `<#IsDST>` which indicates if the current date/time is in daylight saving time (=1) or not (=0)

### Changed

- Some third party uploads switched from HTTP to HTTPS - AWEKAS, PWS, WindGuru
- The charts in the dashboard and default web site now display tooltip numbers in the station locale format
- Improvements to Chart accessibility on the Dashboard

### Fixed

- Davis WLL Soil/Leaf transmitter exception when receive status = null
- Davis weatherlink.com API fix null wind direction in current data
- Fix numerous issues with the 9am daily rollover when using the 'use 10am in DST' option
- Limit Windy station type field to 100 characters for v2 API
- Fix Windy uploads for locales that do not use colon time separators
- Fix `<#RecentPressure>` web tag - not using UnixTime
- Fix various web tags (eg `<#YearTempAvg>`) erroring when no dayfile entries exist to the period requested
- Chart.js charts not plotting if the Cumulus is running under the Invariant Culture locale - now defaults to en-US for the charts
- Local copies of chart data and realtimegauges.txt files not saving in correct location - going into application root folder with a prefix of "web" to the filename
- Fix web tag `<#ByMonthWindH>` which was using the gust decimals setting

---

## [5.0.0 \[b5001\]][50] - 2026-03-25

### Important Notes

- First build using Visual Studio 2026, and transitioning to .NET 10.0
- The initial log conversion may take some time depending on the host computer. It is recommended to perform the initial run in a console so you can see the progress and any errors
- **Required changes to Ambient Extra Sensor Stations:** If you use an Ambient station as an Extra Sensors station, then after upgrading to this release you MUST check which sensors are enabled in the extra station configuration
- **Required updated for MySQL users:** If you use the standard MySQL uploads, then there are two additional columns in the Monthly table. Please run the table updater in the MySQL settings when you first run this release
- **Required update for existing Windy.com upload users:** This version of MX switches to using the new Windy v2 API, this API requires you to enter the full Station ID in the Windy Settings
- **Check required settings for Extra Sensor Users:** Check if Solar and UV are enabled in the Extra Sensor settings. A bug in previous versions of Cumulus MX meant the main station values were always used even if these options were enabled.
	The bug has been fixed, and if you do not want to use Solar/UV from the extra station, you MUST now disable these options


***IMPORTANT: This release requires .NET 10.0 to run, and WILL alter your log file structures***

### New

- The path for the MXdiags files can now be specified in the CumulusMX.runtimeconfig.json file
- The paths for the data, backup, and reports folders can now be defined in Program Settings
- Custom Rollover MySQL commands now have the option to control being run during catch-up or not
- Adds LASER depth to the Dashboard Recent Charts, Recent Select-a-Chart, and Select-a-Period
- Adds LASER depth to the default web site Trends and Select-a-graph charts
- New .NET 10 versions of ExportToMySQL and CreateMissing (v3) compatible with MX v5.0 log file formats
- New script `/MXutils/windows/CreateFirewallRules.ps1` for creating the required Windows firewall rules
- Add an exponential backoff to failed Email sends (up to 5.6 hours)
- New web tags `<#snowunit>`, `<#CapacitorV>`
- Adds a new Data Logs editor for the Recent Data from the SQLite database
- New option in Extra Sensor Settings under Laser Sensor Options to reset the current snow depth value being used for snowfall accumulation to the current laser depth value. This is used when there has been a large spurious change in the laser depth measurement for any reason. This does not affect the current snow depth measurement
- New Option in Extra Sensor Settings under Laser Sensor Options to specify if a laser is being used as a snow sensor
- Added ImportCumulusFile PHP script to `/MXutils` folder
- New Python script to upload monthly log files and the day file to MySQL - `/MXutils/ImportCumulusFile.py`
- Adds logging of debug snow data via the Program Settings > Logging Options - Logs to `/MXdiags/debug_snowLog[sensornumber].txt`
- Ecowitt HTTP Custom Server auto-configuration for main and extra stations now tries the HTTP Local API to access the station in addition to the TCP API
- Add support for BGT and WBGT to Ecowitt HTTP Local API, HTTP (Ecowitt), and the JSON stations
	- New web tags `<#BlackGlobeTemp>` and `<#WetBulbGlobeTemp>`
	- Two new fields added to the monthly log files and the monthly MySQL table to support these new measurements
	- MySQL fields
	```
	BlackGlobeTemp decimal(4,1)
	WetBulbGlobeTemp decimal(4,1)
	```
- Add support for Ecowitt WH52 EC Soil Moisture Sensors to the Ecowitt HTTP Local API and HTTP (Ecowitt) stations - soil moisture and temperature readings only for now
- Fix ecowitt.net historic data download of PM measurements
- New snow depth filtering mechanism implemented. This is a three-stage filter...
	- **Stage 1** applies a median filter to the raw values - you can specify the length of time in minutes for the median values. This is good for filtering out sudden spikes.
	- **Stage 2** applies a clip to the output of the median filter. The clip limits the step size of the increase/decrease of the output of stage 1 to the value you specify
	- **Stage 3** applies an Exponential Moving Average filter to the output of stage 2. This is essentially time based smoothing
	- The default values are:

		| Laser Units        | mm    | cm    | inch  |
		|:-------------------|:-----:|:-----:|:-----:|
		| median (mins)      ||         10          ||
		| clip (laser units) |  1.0  |  0.1  | 0.04  |
		| EMA time (mins)    ||        12.0         ||

	- Note you can effectively disable any stage by setting: median=1, or clip=10, or EMA=0.1
	- Increasing the filtering also delays the value being updated. The approximate delay is median/1.5 + EMA time/1.5. The defaults will give a 10-12 minute lag
	- You can edit the new smoothing filter values in the Calibration Settings screen
	- Suggested starting Minimum Increments for the new filter: 2-5 mm, 0.2-0.5 cm, 0.08-0.2 inches
- Adds Snowfall 24h charts to the Dashboard and default web site
- New web tags which show the current laser derived snow depth in snow depth units
    `<#LaserSnowLatest[1-4]>`
- Adds the ability to connect a third-party sunshine recorder to Davis WLL Stations
	- Supports connecting a sunshine recorder such as the Instromet to the Rain input of an ISS type transmitter
	- The sunshine recorder must send a pulse for every 1/100th hour of sunshine recorded
- Adds support for Ecowitt WH52 Soil Moistire sensors to HTTP API and Ecowitt.Net stations
- WeatherFlow Tempest stations can now filter live data based on device serial number. You can leave the serial number blank if you only have a single device
- Add LaserDepth1 and LaserSnowLatest1 to `websitedataT.json` along with a last modified date for the file


### Changed

- **Monthly log files have changed format**, the first two values of each record are different to resolve DST transition ambiguities
	- Old format records start: Date,Time,
	- New format records start: Date_Time,Unix_Timestamp,
	- All the data fields retain the same offsets as before
	- The log files will automatically be converted on the first run of v5.0
	- The original files will be backed up to `/backup/ConvertBackup`
	- The Date_Time field is now purely for human readability, Cumulus MX now uses the Unix Timestamp internally
- The main monthly log files now log the final values for Rainfall Today and ET Today in the first record of the following (rain/calendar) day
- The extra monthly log files now log the final snowfall in 24h value in the first record of the following (snow) day
- The dashboard has been converted from using Highcharts to ChartJS, and will now work fully offline
- The default web site has been fully converted from Highcharts to ChartJS, removing the dependency on obtaining a Highcharts licence
- Removes the dependency on ServiceStack.Text for JSON handling, now using the built-in System.Text.Json
- Swaps SQLitePCLRaw.bundle_green for newer SQLitePCLRaw.bundle_e_sqlite3
- Debug and data logging are now fully independent
- Internal change to the date/time storage in the recent data SQLite database (cumulusmx.db)
	- DateTimes are now stored as Unix timestamps to resolve DST transition ambiguities
- Debug Beta builds no longer save the debug & data logging enabled state into the Cumulus.ini file
- FTP/FTPS/SFTP connection management changed to avoid Operating System DNS caching in .NET 10
- The CO₂ Graph data file will now contain null values for missing entries like the other files
- The realtime FTP watchdog file has been renamed from "_cumulusmx_watchdog.txt" to "cumulusmx_watchdog.txt"
- The dashboard now allows user defined pages to be hosted under the `/interface/custom` folder
- Remove retries from WOW uploads
- Changes to MySQL buffer processing (after catch-up or server/network outage). The updates are now committed every 50 statements and are not removed from the queue unless the commit is successful
- Web tags are now case insensitive, as are tag parameter keys. Simple parameter values are also case insensitive. Parameter values for date formats etc are obviously still case sensitive.
- Switches Windy.com uploads to their new v2 API, this now allows upload of solar radiation values
	- You can now use the station password instead of an API key to authenticate
	- **Existing users must add their Station ID to the settings, this is a requirement of the new API**
- The snow 24 hour accumulation is now reset to zero *after* the "snow hour" processing is complete and the extra log file written.
	This means that the true final daily total will be available in the first record of the following snow day the same as the daily rainfall total in the monthly log file
- Now sets the HTTP Referer to the upload site when using PHP Upload, and to https://cumulus.hosiene.co.uk/ for all other HTTP queries
- The Extra Sensors dashboard page is more responsive to screen size and now only shows tables for sensors that are enabled in Display Options
- Davis WLL now catches missed recent gusts in the broadcast data

### Fixed

- Ecowitt HTTP API station using a fixed 5 minute interval for Degree Days during catch-up rather than the log file interval
- Interval uploads now have a locking mechanism like realtime uploads. This should prevent 1-minute intervals accumulating a backlog of failing uploads if the destination server is unavailable
- Changed the handling of Ecowitt SD card log files during catch-up to avoid duplicates over the DST period being dropped
- Prevent multiple copies of the FTP watchdog being started by Internet Settings
- Fix double entry of AirLink Outdoor in Extra Sensor settings
- Sun rise/set dawn/dusk calculations for DST changeover days - note locations near the arctic circle may still show the times in the wrong DST state
- Fix to `websitedataT.json` correcting the 'snowDepth' and 'snow24h' entries to 'snowdepth' and 'snow24hr' and adding 'snowunit'
- The EOD graph data files FTP(S) and SFTP uploads were being flagged as complete whether or not they were successful (PHP was OK)
- Correct wind speed array loading from recent data on start-up to use uncalibrated values
- Correct Windguru to use calibrated wind speeds
- Some fixes and updates to the Query Day File page
- Add retry to daily file backups when a file is in use
- Fix Ecowitt Cloud API Laser conversions of measurements in cm and feet to user units
- Fix Davis WLL station getting in a day reset loop when no historic API details and last run was prior to last rollover
- Fix exception when Ecowitt camera URL fetch hits the rate limit
- Final fix for Tempest station opening the UDP port in shared mode
- Fixes for Davis VP1/2 serial BARREAD and SETTIME commands
- On laser depth baseline changes, realign last snowfall depth to the new value
- Fix snow24h being reset to null at the snow hour if there is a valid laser depth, now set to zero
- Fix a number of issues with using the Ecowitt Cloud Station for extra sensors
- Errors handling null data from PurpleAir sensors
- Fix processing Ecowitt SD card log file when only one record is in scope
- Compass points not being saved in Locale Strings
- Errors saving/loading waxing/waning crescent moon in Locale Strings
- Fix Automated Weather Diary entries not being created for some stations during catch-up - Davis VP2, Davis WLL, Ecowitt Stations
- Fix web tags using year and month parameters to take account of meteo dates and first day of the year/month and add consistent handling
- If Bluesky uploads are rate limited, do not attempt to retry the upload
- PHP uploads failing after first upload in certain configurations
- Fix bug in exception logging if some exception data is null
- More adjustments to real-time FTP error detection and reconnection
- Fix Ecowitt HTTP API and Cloud station types not calculating derived temperature values when an extra T/H sensor is mapped to be primary
- Fix IsRaining alarm being immediately cleared after each trigger when using the Ecowitt "Use Piezo IsRaining" setting
- Fix a major logic error when applying extra sensor data to the main station - affects most stations
- Fix MySQL error handling to prevent buffering of statements with syntax errors
- Fix extra sensor data input via the JSON Station MQTT topic (was using main station config values)
- Add a short delay between fetching Ecowitt SD card files to try and mitigate the zero length/oddly formated files being sent
- Fix JSON Extra Sensor Station CO₂ and Lightning values only being applied when run as the main station
- Fix AQ PM10 Average visibility settings being reset on CMX restart
- Now handles AirLink null data values - represented as 0 values for now
- The HTTP Station (Ecowitt, Ambient, Wund) not repopulating the recent data database on startup

### Package Updates

- DnsClient [NEW]
- FluentFTP
- HidSharp
- MailKit
- Microsoft.Win32.SystemEvents
- MQTTnet
- MySqlConnector
- NLog
- NLog.Extensions.Logging
- ServiceStack.Text [REMOVED]
- SixLabors.ImageSharp
- SSH.NET
- SQLitePCLRaw.bundle_green [REMOVED]
- SQLitePCLRaw.bundle_e_sqlite3 [NEW]
- System.CodeDom
- System.IO.Ports
- System.ServiceProcess.ServiceController

---

[50]: https://github.com/cumulusmx/CumulusMX/releases/tag/b5001
[51]: https://github.com/cumulusmx/CumulusMX/releases/tag/b5002
[52]: https://github.com/cumulusmx/CumulusMX/releases/tag/b5006
[53]: https://github.com/cumulusmx/CumulusMX/releases/tag/b5007
[54]: https://github.com/cumulusmx/CumulusMX/releases/tag/b5010
[55]: https://github.com/cumulusmx/CumulusMX/releases/tag/b5011

[60]: https://github.com/cumulusmx/CumulusMX/releases/tag/b6000
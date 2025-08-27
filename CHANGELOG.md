# Changelog

All notable changes to this project will be documented in this file.

Additional notes are available on the [forum release thread](https://cumulus.hosiene.co.uk/viewtopic.php?t=17887)

This file is formatted as [markdown](https://www.markdownguide.org/), any decent editor should display it correctly formatted.
Alternatively view it [online on GitHub](https://github.com/cumulusmx/CumulusMX/blob/main/CHANGELOG.md)

---
---

## [4.6.0 \[b4113\]][23] - 2025-08-27

### New

- Adds support the Ecowitt WH45/46 CO₂ sensor values on the HTTP API
- Adds support for the Ecowitt WN20 battery status monitoring
- Adds RSSI value (if available) to the Ecowitt sensor list logging
- A new Alarm for Cumulus MX general errors, triggered every time something is written to the Recent Errors log, and cleared when the log is cleared
- Adds Soil Moisture upload to Met Office WOW for stations that report the moisture level as a percentage
- Snow values added to the websitedataT.json file
- Cumulus MX now handles Windows shutdown/restart and console window closure gracefully
- **EARLY DAYS** support for Cumulus MX general localisation of the Dashboard interface. The implementation details may change depending on feedback or tuning etc.
	- AI2 is excluded for now
	- The HTML and scripts strings are in `/locales/dashboard/`
		- There is one strings file per language
	- The settings strings are in `/locales/dashboard/json/{language}`
		- There is a folder per language, and a language strings files for each settings file
	- Change the display language in Program Settings -> Culture Overrides -> Display Language. Only those languages with translation files will be selectable
- New option to force the am/pm time designators to lower case. See Program Settings -> Culture Overrides

### Changed

- Added Davis Cloud API UUID option to the Configuration Wizard
- Add a retry to downloading Ecowitt SD card files on error or if returned file is empty
- The FTP log files have changed naming convention (to work better with the new version of NLog)
	- The latest file (if more than one), will be one with the highest value for NN for today: `ftp-<logtype>_YYYY-MM-DD_NN.log`
	- You will need to manually delete any old log files that use the old naming convention of:
		`ftp-realtime-N.log` or `ftp-interval-N.log`
- CreateMissing updated to v2.1.0 to fix the daily rainfall calculation on rain counter reset at rollover, and add support for evapotranspiration calculation
- Improvements in the Cumulus MX shutdown process - it should now be much faster
- The JSON and Tempest stations now use the same Sea Level Pressure calculation as all other stations
- All dashboard and default web site graph data files now use true UTC time stamps
	- Previously they were "pseudo-UTC" to force the graphs to display the station times rather the viewers time zone
	- Highcharts version used by the dashboard and default web site updated to v12.3.0 to support this
	- The charts will now render correctly at DST changes
	- **IMPORTANT**: You must upload the latest versions of the default web site pages and scripts files to support this change
	- If you have your own versions of Highcharts scripts, then the relevant change is from:
		```json
		{
			time: {
				useUTC: true
			}
		}
		```
		To (substituting your stations time zone):
		```json
		{
			time: {
				useUTC: false,
				timezone: 'Europe/London'
			}
		}
		```

		*Strictly, the 'useUTC: false' is not required as that setting has now been deprecated*
	- You must also use a version of Highcharts later than v11.2.0 - Cumulus MX now uses v12.3.0
- All logging to log files is now asynchronous. This means a change to log file naming scheme for the main MX diags
	- The latest log file will always have just the current date as the main filename: eg. 20250726.log
		- When The day changes a new log file will be created, this too will just have the current date as the filename
	- Rollover:
		- Rolled over files will have the time appended: eg. 20250726-115412.log
		- Rollover occurs when log files that have exceeded the rollover size (2MB)
		- Rollover also occurs when you start Cumulus MX and there is an existing dated log file for today
		- The timestamp on rollover files is the time of the last entry in the file, not the first
	- The maximum log file size has been decreased to 12MB, but the number of archives retained increased to 20
	- You may notice the logged event times only increment every 16ms, this is an efficiency thing!
- Logging of JSON responses from the Ecowitt Local HTTP API now compacts the output by removing line feeds and tabs from the text
- Improvements to the dashboard Select-a-Period graphing
	- This now uses the meteorological day you have defined, and the pm2.5/ pm10 values are now pulled from the log files rather than the time restricted recent data
- Adds a default User-Agent header to all HTTP requests of "CumulusMX/4.6.0.4107" - or whatever the current version/build is
- The dashboard charts now honour the time format setting in Settings -> Program Settings -> Culture Over-rides
	- The new default for the charts is to display in the web browsers TZ settings format
- The system uptime is now obtained differently, this allows the removal of the System.Diagnostics.PerformanceCounter package

### Fixed

- Bug in Ecowitt.API ApplyHistoricData: AQI = Nullable object must have a value
- AirLink log files being one comma short if only an indoor AirLink is in use
- Unlike the other log files, the monthly log file name generator did not have the check to remove "dots" from the yyyyMM date part of the filename
- Ecowitt HTTP API station not triggering the firmware alarm when new firmware available
- Handling of cached MySQL statements that are in error because of bad syntax or reference errors
- An error in the Ecowitt SD card log file handling that removed the corresponding Allsensors log file from the processing list if the primary file did not contain any dates in the required range
- Ecowitt historic catch-up from SD card was not setting the DataDateTime variable
- Ecowitt Cloud station decoding of CO₂ 24-hour PM values
- Ecowitt Cloud station 24-hour CO₂ PM values are now "kludged" from the 24-hour AQI values supplied by Ecowitt

### Package Updates

- BouncyCastle.Cryptography
- FluentFTP
- MailKit
- Microsoft.Win32.SystemEvents
- NLog
- NLog.Extensions.Logging
- SixLabors.ImageSharp
- System.CodeDom
- System.Diagnostics.PerformanceCounter [REMOVED]
- System.IO.Ports
- System.ServiceProcess.ServiceController



## [4.5.2 \[b4105\]][22] - 2025-06-21

### New

- Editing rainfall values in the dayfile now automatically updates the yesterday, week, month, year totals without restarting Cumulus MX

### Changed

- FTP watchdog now creates its temp file using the path specified in the Internet Settings

### Fixed

- Fix Davis AirLink badly formed URL when requesting health data for standalone sensors
- Fix Davis VP2 Extra T/H handling
- Fix and improvements to Ecowitt SD card file handling with timestamps
- Error starting v4.5.1 on Windows systems below version 10 - "Unable to find an entry point named 'PowerRegisterSuspendResumeNotification' in DLL 'Powrprof.dll'!"
- Fix Wunderground upload error - TimeSpan overflowed because the duration is too long



## [4.5.1 \[b4104\]][21] - 2025-06-14

### New

- Adds some additional detection of resuming from a computer suspension (the clock jumping forward by more than 10 minutes), and aborts the program ready for a restart and catch-up
	- This should only be activated on Linux/MacOS systems, Windows has its own suspend detection

### Changed

- FTP watchdog default interval changed from 1 minute to 5 minutes

### Fixed

- Fix for a number of related issues
	- day rollover sometimes not occurring
	- real-time operations sometimes stopping
	- third party uploads sometimes stopping
- Fixed standby/resume not working when Windows Modern Standby is in use
- Realtime SFTP still not working in v4.5.0 b4103
- Fix for the FTP watchdog not reconnecting on link failure
- Ecowitt HTTP API Station use SD card missing from the Wizard
- Fix incorrect trend and last hour values at rollover for stations using 9am rollover
- Reinstate the page number option on the data file viewer/editors
- Davis WLL Soil Temp/Moist & Leaf Wetness channels not being read from Cumulus.ini correctly
- Fix for incorrect values being returned for the `<#MonthAvgTotalChillHrs>` web tag


## [4.5.0 \[b4103\]][20] - 2025-05-31

### New
- By popular demand, implements web tags:
	`<#ExtraTemp[11-16]>`, `<#ExtraHum[11-16]>`, `<#ExtraDP[11-16]>`

### Fixed

- Davis Cloud Station (VP2/Vue) decoding of soil moisture/temp and leaf wetness
- Fix Ecowitt HTTP API station erroneously  mapping an extra humidity sensor to the indoor value
- Add missing 24h AQI to CO₂ sensor values from the Ecowitt HTTP API station and Ecowitt Cloud Station
- Air Quality PM10 sensor names not being saved
- Some basic checking of the PurpleAir response before trying to process it
- Sun rise/set daylength calculation errors now caught and the program continues on error
	- Fixes to the calculations for locations above the Arctic circles
- Realtime SFTP not working in v4.5.0 b4102


## [4.5.0 \[b4102\]][19] - 2025-05-26

### New

- Realtime FTP (and FTPS) handling changed
	- MX now runs a watchdog process that regularly checks the FTP connection is functional
- All-new Alternative Interface for the dashboard
- Cumulus now supports the back filling of historic on the first run of new installations
	- If Cumulus detects that it is a new installation, a *Backfill Date* field will be shown in the main station settings section
	- When Cumulus is restarted after setting the station etc, the first run will attempt to backfill the data from the date specified
	- "First run" is defined as:
		- No today.ini file present
		- No Cumulus.ini file present, or Cumulus.ini is present but the [station] type=-1
- New web tag `<#SnowAccumSeason>` - this tag queries the Weather Diary for the current snow season total snow fall
	- The tag takes an optional parameter `y=YYYY` which you can use to specify the snow season starting year for which you want the total
- The Weather Diary now has tick boxes for: Thunder, Hail, Fog, Gales
	- New web tags to fetch these values:
		`<#DiaryThunder>`, `<#DiaryHail>`, `<#DiaryFog>`, `<#DiaryGales>`
	- The tags return "true", "false", or "-" if no diary entry exists. This null value can be overridden with the usual `nv=` parameter
	- All web tags take an optional parameter of the date to be queried `date=YYYY-MM-DD`
- New monthly average web tags
	- These are the average values for a month across all your Cumulus MX history
	- The average excludes any partial month at the start of the history, and the current month, but assumes the data between is complete
	- Defaults to the current month, specify a month using the `mon=NN` tag parameter
	`<#MonthAvgTemp>`
	`<#MonthAvgTempHigh>`
	`<#MonthAvgTempLow>`
	`<#MonthAvgTotalRainfall>`
	`<#MonthAvgTotalWindRun>`
	`<#MonthAvgTotalSunHours>`
	`<#MonthAvgTotalET>`
	`<#MonthAvgTotalChillHrs>`
- Ecowitt stations can now map the values from an Extra Temp/Humidity sensor to the indoor T/H values (previously only outdoor T/H mapping was implemented)
- Locale Strings value for web tag elapsed time, applies to `<#SystemUpTime>` and `<#ProgramUpTime>` web tags
- Locale Strings value for web tag times, applies to 60+ web tags
	- By default, they output the time as 24-hour HH:mm, you can now override this and use 12-hour format as the default
- New Program *Options > General Options* setting to disable the use of WebSockets in the dashboard and use HTTP polling instead
	- Previously this required manual editing of the Dashboard, Now, and Gauges page scripts
- Ecowitt Cloud Station type now reports individual low battery sensors to the low battery array
- Ecowitt Cloud Station now has an additional option to specify the expected data update rate
- New uptime web tags
	- Davis AirLink: new web tags for uptime and link uptime
		`<#AirLinkUptimeIn> <#AirLinkLinkUptimeIn>`
		`<#AirLinkUptimeOut> <#AirLinkLinkUptimeOut>`
	- Two new web tags for the Station Up-time, and Station Link Up-time as time spans
		`<#StationUptime>`, `<#StationLinkUptime>`
		- Currently only the following stations supply this information:
			Davis WLL - both
			Davis WLC - both
			Ecowitt HTTP API - Uptime only
	- The web tag custom format= parameter for these tags is slightly different from the normal DateTime web tags
		- To get the values for hours use `{0:hh}` (two digits, leading zero) or `{0:%h}` (no leading zero)
		- To get the values for minutes use `{0:mm}` (two digits, leading zero) or `{0:%m}` (no leading zero)
		- A normal the format string can contain free text, for example:
		`format="Uptime is {0:hh} hrs {0:mm} mins"`
- Basic support for live data from PurpleAir sensors
	- It uses the existing AirQuality1-4, AirQualityAvg1-4, and Extra Temp/Hum/Dewpoint sensor web tags
	- It adds pm10 values
		- New web tags to support this:
			`<#AirQuality10_[1-4]>`
			`<#AirQuality10Avg[1-4]>`
			`<#AirQuality10Idx[1-4]>`
			`<#AirQuality10AvgIdx[1-4]>`
		- Adds these AQ pm10 and pm10 average to the extra log file
- Ecowitt Local HTTP API adds support for solar units in "Klux" and "Kfc", in addition to the existing "lux" and "fc"

### Changed

- Davis VP2 IP logger IP address can now be changed and take effect without restarting MX
- Third Party, MQTT, and MySQL password fields now 'reveal' when they receive focus
- Renamed WoW settings screen upload 'PIN' to 'Authentication Key'
- Low Battery Alarms now append the low battery status strings to the message
	- Plus, quite a few internal Alarm code changes
- Number of supported Extra Temperature/Humidity/Dewpoint sensors increased from 10 to 16
	- New web tags to access these new values and the existing sensors 1-10:
		`<#ExtraTemp sensor=N>`, `<#ExtraHum sensor=N>`, `<#ExtraDP sensor=N>`
	- Adds Extra Temp/Hum/DP sensors to extra log file, log file editor, and graph data
- Davis WLL: number of supported extra sensors increased
	- Soil Temperature from 4 to 16
	- Soil Moisture from 4 to 16
	- Leaf Wetness from 2 to 8
	- Extra Temperature/Humidity from 8 to 16
- Station Pressure limits now derived from sea level limits taking station elevation into account
- Station Pressure limits now apply to all stations - previously only Davis VP2 implemented them
- Ecowitt camera web tag warning messages changed to plain messages
- Add support for Timestamp in Ecowitt SD card log files
- The web tags `<#daylength>` and `<#daylightlength>` now have revised format strings. **Please see the uptime tags in the New section above for details**

### Fixed

- Revert 12h time format change in NOAA reports, 12h times now reported as "h:mmtt" rather than "h:mm tt"
- Missing times of Feels Like highs and lows on the dashboard gauges page
- Entry of EMEI codes on the Station Settings and Wizard pages for Ecowitt stations
- Fixed the web tag `<#ProgramUpTimeMs>`, it now returns an integer value as originally intended
- Dashboard Select-a-Period AQ PM2.5 chart only showed data from the last N days - N defined by the recent data setting
- MQTT IP protocol version not saving to Cumulus.ini
- MQTT Protocol version - removed Auto detect as it does not work
- Davis AirLink not writing to the log file during catch-up. Note not all values are available in catch-up
- Fix leaf wetness graph data when null
- CO₂ graphs not using the localised captions
- Ecowitt Camera webtag logic changes
- Ambient Station improved AQ decoding
- Fix AWEKAS only sending soil moisture 1, and leaf wetness 1

### Package Updates

- FluentFTP
- MailKit
- Microsoft.Win32.SystemEvents
- ServiceStack.Text
- SixLabors.ImageSharp
- SSH.NET
- SQLitePCLRaw.bundle_green
- System.CodeDom
- System.Diagnostics.PerformanceCounter
- System.IO.Ports
- System.ServiceProcess.ServiceController



## [4.4.5 \[b4088\]][18] - 2025-04-10

### New

- Adds current values for Ecowitt CO₂ sensor pm1 and pm4, and 24-hour averages for pm 2.5, 10, 1, 4 to Ecowitt HTTP API

### Changed

- Removed Cumulus snow accumulation filtering/averaging from laser depth as this is now available in the station

### Fixed

- WeatherUnderground rapid fire no longer uploading in v4.4.5
- Remove spurious messages about non-present HTTP Rollover commands being invalid
- Ecowitt binary API CO₂ sensor causing decode unknown sensor detection and unpredictable decodes
- Rounding error on laser depth increases to snowfall accumulation meant small increments were rounded to zero


## [4.4.4 \[b4087\]][17] - 2025-04-01

### New

- Locale Strings has a new Web Tags format for general dates
- Added PM2.5 24 hour value handling to Ecowitt local HTTP API

### Fixed

- NaN error when Wunderground rapid fire is enabled and an upload fails
- Web tag `<#recordsbegandate>` now uses the Locale Strings "General Date" format by default
- Error in dashboard script `locale.js` when setting laser sensor field titles
- Laser unit to snow unit conversion when the units are different
- Fix stack overflow when converting units from Ecowitt SD Card log files


## [4.4.3 \[b4086\]][16] - 2025-03-18

### New

- Ecowitt Rainfall Rate now read from SD card log files (if present), previously the field was missing, and the hourly rain figure was used
- Ecowitt SD card log file lightning distance units are now converted if required

### Changed

- MQTT settings now has an Advanced Settings section, this allows you to override the default values for:
	- Using TLS
	- IP version
	- MQTT protocol version
- Ecowitt SD card log file processing removes corresponding extra log file from the processing list if no data is found in the base file
- Changing the PHP Brotli Compression setting now forces the test of supported compressions to be rerun
- Ecowitt Local HTTP API current data decode now uses case insensitive checks on the value units
- Ecowitt SD card log file processing now uses case insensitive checks for all field names

### Fixed

- Exclude possible empty lines from SD card log files
- Fix handing of pressures in mmHg in the Ecowitt HTTP API
- Ecowitt SD card log file decode of LDS Depth values
- Davis WLL was saving the gust direction as the speed when a new daily high gust was detected that had been missed in the broadcast data


## [4.4.2 \[b4085\]][15] - 2025-03-12

### New

- New web tag `<#DataDateTime>` which reflects the current date time with respect to the data
	- Use this web tag in things like Custom MySQL INSERT statements to pick up the historic data's date/time rather than the current clock date/time
- Add catch-up option to MySQL Custom Interval Minutes commands - this defaults to disabled

### Fixed

- Fix Ecowitt HTTP Local API not processing temps in Fahrenheit correctly
- Fix the table background colours of the dashboard records editors pages


## [4.4.1 \[b4084\]][14] - 2025-03-09

### Fixed

- Fix for FTP Logger errors when FTP logging is not enabled
- Fix for "jumbled" NOAA plain text reports on the default web site
	- Updated `websitedataT.json`
	- Updated `\webfiles\js\noaarpts.js` to default to plain text rather than HTML if the option is not visible
		You will need to upload this file to your web site


## [4.4.0 \[b4083\]][13] - 2025-03-09

### New

- NOAA Reports can now be created as HTML
	- Two sample templates are included for annual and monthly reports: `Reports/SampleHtmlTemplateYear.htm`, and `SampleHtmlTemplateMonth.htm`
	- The reports are generated from the "in-use" templates: `Reports/HtmlTemplateYear.htm`, and `HtmlTemplateMonth.htm`
	- If you enable HTML reports and do not create custom "in-use" templates, the sample templates will be copied to "in-use" templates
	- You can edit the "in-use" templates to alter the localisation, adjust formatting etc
	- A new web tag `<#Option_noaaFormat>` which returns `"text"` or `"html"`
	- Changes to the default web site files to accommodate this:
		- `noaareports.htm`
		- `js/noaarpts.js`

- Adds 24-hour snowfall accumulation totals
	- The selected laser sensor accumulation is included in automated diary entries
	- The accumulators reset at the defined snow recording hour
	- The current values can be retrieved with new web tags `<#SnowAccum24h[1-4]>`
	- Added to Display Settings and Extra Sensors dashboard pages
- Add Snow Season
	- Define start month in Station Settings | Common Options
	- New web tags `<#SnowAccumSeason[1-4]>`

- Adds laser sensors to Display Settings, Locale Strings, and Extra Sensors dashboard pages
- Adds laser depth calculation to the Extra Sensor settings. Use this with simple laser distance sensors to allow Cumulus to calculate a depth value. Note Ecowitt already provide this ability with their LDS01 sensors.
- New version of MigrateData3to4 to now finds custom daily files correctly
- Add File Ignore time to JSON station advanced settings
- You can now embed web tags in both Standard Alarms and User Defined Alarm email messages
- Custom MySQL Minutes queries are now processed during catch-up
- New web tag for Vapour Pressure deficit `<#VapourPressDeficit>`
	- Takes a parameter of `sensor=N` to calculate the VPD for outdoor (=0, default if omitted), or any extra temp/humidity sensor (=1-8)
	- Returns the VPD in user pressure units
	- The returned units can be changed using the standard `unit=` parameter

- EXPERIMENTAL
	- Adds ability to the Ecowitt local HTTP API station to read historic data from the SD card
	- Currently only supported by the GW3000 and WS6210

### Changed

- Add NOAA report format (options.noaaFormat) to websitedataT.json
- Davis WLL checks for missed wind gusts in multicasts, now uses the "current" 2-minute gust value, and "back dates" it one minute in the recent wind data
- The latitude and longitude strings now use localised compass point directions (set in Locale strings)
- Switched from NRec.Logging.File to NLog for FTP logging
- FTP logging now creates separate log files for realtime and interval FTP activities
- Web tag `<#CPUtemp>` now returns `"-"` if no value present, or whatever is specified by `nv=`
- Log file editors now scroll the data horizontally and vertically with a fixed header and fixed first two columns
- Change of name of the Ecowitt "TCP Local API" station to "Binary Local API (Legacy)" to reflect the status of the protocol

### Fixed

- Fix writing of the first Custom MySQL Minutes interval value to Cumulus.ini
- Ecowitt HTTP Local API station not mapping extra temperature to outdoor temperature correctly
- The web tag parser now accepts empty parameter values. eg. `nv=""`
- Web camera not appearing in Ecowitt Extra Sensor settings page
- Improve Ecowitt API Current data date/time detection - now defaults to query time if no data time found
- Fix "Regenerate all missing reports" not creating current year/month reports
- Ecowitt camera URLs not working when the station is configured as an Extra Station
- Web tag `<#CPUtemp>` now supports options `rc`, `dp`, `tc`, `unit`
- 9am values not always rolling over correctly during "catch-up"
- Fix Monthly Log/Extra Monthly log viewers for 9am meteo day users
- Davis station: Fix for the 00:00 (or 09:00) rainfall being counted on both days during catch-up
- User Alarms not accepting "equals" type
- Fix Station Pressure calibration settings being read from the Pressure settings in Cumulus.ini
- Web tags `<#snowdepth>` and `<#snow24hr>` now accept the dp= and tc= web tag parameters
- Changes to how MQTT connects and reconnects to the server

### Package Updates

- Sixlabors.ImageSharp


## [4.3.3 \[b4070\]][12] - 2025-01-01

### Fixed

- JSON Station null reference Exception
- Error rewriting Cumulus.ini on some Windows installations
- Trend values on rollover (rates, rain in last hour etc) now include the last minute before rollover


## [4.3.2 \[b4067\]][11] - 2024-12-18

### New

- Web tags now have the optional null value parameter `nv=xxxx`
	- This overrides the default string returned when the value is null or not available
	- The default for most web tags is a dashed value like "--"
	- Example, if extra temp sensor #4 is missing `<#ExtraTemp4>` outputs "-" by default, but `<#ExtraTemp4 nv=null>` outputs "null", `<#ExtraTemp4 nv=0>` outputs "0"

### Changed

- Improvements to graph data creation when there are null values present. Affects Solar, UV, Indoor Temp/Hum, Extra Sensors
- HTTP Files now correctly allows the custom entry `<ecowittcameraurl>`, other URLs can now contain web tags
- Rain week was added to the Realtime file in v4.3.0 but not to the Realtime MySQL table. This version adds it to MySQL as well
	- You can use the 'Update Realtime' button on the MySQL Settings page to amend your existing realtime table.
	- Or there is a SQL script included to update existing Realtime MySQL tables with the new column: `/MXutils/v4.3.2-AlterMySqlTables.sql`
- AI2 switching to default dashboard now opens the dashboard in the same tab

### Fixed

- Fix non-present indoor humidity values causing the dashboard gauges to fail
- Indoor temperature in Select-a-period graphs show 10x value in comma decimal locales
- Snow graphs now show all days from the first diary entry to present
- Weather Diary database migration process updated
	- Databases migrated by v4.3.0 and v4.3.1 are now scanned for issues, and fixes automatically applied
- Fix some errors parsing log file entries, affects load recent data, and graph files
- Fix Unknown sensor errors in Ecowitt TCP API sensor info
- Ecowitt historic catch-up average wind speed was being mishandled by MX = a lower value than reality


## [4.3.1 \[b4064\]][10] - 2024-12-09

### Changed

- Add weekly rain (rweek) to websitedataT.json

### Fixed

- AWEKAS uploads fix to avoid frequent rate limiting messages
- Ability to edit sunshine hours to two decimal palces in the log and day file editors
- Fix recent Solar graph data bad format if solar or UV sensor data is null
- Add check for CustomHttpXXX URLs to begin with http
- Davis WLL change to avoid potential extension of 10 minute gust values when broadcasts are working
- Bluesky variable timed posts not firing if no timed posts are defined
- Snow graphs now show all days
- Fix Rain Week calculation
- Fix for LoadRecent failing if indoor sensors are absent
- Fix for Tempest stations not reading historic data


## [4.3.0 \[b4063\]][9] - 2024-12-04

### New

- Adds Rain Week to the dashboard
	- There is also a new web tag `<#rweek>`
	- Configure the start-of-week day in `Station Settings > Rainfall`
- Added displaying snowfall data on the dashboard and default web site
	- Enable display of snow data on the dashboard, default web site, and graphs in `Display Options`
	- New web tag `<#Option_showSnow>`
	- You will need to re-upload the default web site files for this:<br>
		`webfiles\historic.htm`<br>
		`webfiles\js\historiccharts.js`
- New web tags for 9am High/Low temperatures<br>
	`<#temp9amTH>`,`<#Ttemp9amTH>`<br>
	`<#temp9amTL>`,`<#Ttemp9amTL>`<br>
	`<#temp9amRangeT>`<br>
	`<#temp9amYH>`,`<#Ttemp9amYH>`<br>
	`<#temp9amYL>`,`<#Ttemp9amYL>`<br>
	`<#temp9amRangeY>`
- New web tags for dawn and dusk<br>
	`<#IsDawn>`, `<#IsDusk>`
- New web tags for user defined wind speeds<br>
	`<#WindAvgCust m=NN>`, `<#WindGustCust m=NN>`
	- Where m=NN defines the period NN minutes to average the wind speed, or measure the peak gust speed
- Added some validation to the fields in the log editors
- The dashboard and default web site can now display Chill Hours charts
- Initial support for the new Ecowitt WH54 LDS01 Laser Distance sensors, just sensor info/battery decoding for now as that is all that is documented
- The JSON Station type can now be used to input Extra Sensor data.
	- This supports all the JSON Station input feed types: file watcher, HTTP POST, and MQTT
- Adds calibration for Station pressure (and so also for Altimeter pressure)
	- Note: If you use the option for Cumulus to calculate sea level pressure, then this new station pressure calibration is the one that will applied to the SLP as well
- Adds Bluesky posting to the Third Party uploads list
	- The content to be posted at fixed intervals is contained in the `web/Bluesky.txt` file, you can include all the usual web tags
	- A sample file is included in the web folder for you to edit `web/BlueskySample.txt`
	- Timed posts default to using the same Interval tempate file `web/Bluesky.txt`, but you can override this so posts at different time can have independent content
	- In the template file(s), you may use the following features:
		- Include web links using the syntax: `https:\\my.site.com\page|Text for link|`
		- Include hashtags using the normal `#MyTagName`
		- Include mentions using the normal `@identifier`
		- Attach images (max 4) to a post using the syntax: `image:path_to_file|Alternative text|`
			- The path_to_file can be either a local filesystem path, or a http url
			- Image formats supported: JPEG and PNG - it is best to post jpg images as Bluesky converts other formats to jpg and may alter them in the process
		- Cumulus will convert these to active links, tags, and mentions when posting the message
	- After editing the `web/Bluesky.txt` file, you must load it back into Cumulus by viewing the `Third party uploads` page where it will display the contents
- Adds Bluesky posting from Alarms and User Alarms
	- Each alarm can have a different template file, but they all default to `web/BlueskyAlarm.txt`
	- A sample file is included in the web folder for you to edit `web/BlueskyAlarmSample.txt`
	- You can include the same features as the regular template above
	- In addition you can include the text `|IncludeAlarmMessage|` and this will include at the point the message that would be sent via email. These messages are editable
		in the `Settings > Locale Strings` page
	- **Note**: You must enable Bluesky and enter your Bluesky credentials in the Third Party Uploads settings, but you need not configure any Interval or Timed posts.
- Adds support for the new Ecowitt Laser Distance Sensors to Ecowitt HTTP API and HTTP Station (Ecowitt) stations
	- Adds new web tags `<#LaserDist1> - <#LaserDist4>` and `<#LaserDepth1> - <#LaserDepth4>`
- The upload.php script has been updated, it will now create the upload destination folder if it does not exist.

### Changed

- The extra sensors and the extra sensors log file now records null value values for absent readings. This will void logging spurious zero values at start-up
- AirLinks and the AirLink log file now records null value values for absent readings. This will void logging spurious zero values at start-up
- Indoor temperature and humidity now record null values
- Solar Rad and UV-I now record null values
- Removed NOT NULL requirements from the MySQL table definitions for Realtime, Monthly, and Dayfile
- Efficiency improvements to all the "recent data" web tags
- The Weather Diary has been revamped with revised fields to make it more useful
	- The existing Weather Diary database data is migrated to the new format on first run of v4.3.0
	- The Editor page gains a new "Export All" button to export your diary to CSV format
	- The Editor page gains a new "Upload File" button to re-import your exported CSV files
	- The Editor page now also has a Time field which defaults to the configured snow recording time, but you may override it
	- There is a new option to automatically create a snow depth record on your snow recording hour. This requires the connection of an Ecowitt WH54/LDS-01 sensor to your station
	- A new web tags `<#snow24hr>`, `<#snowcomment>`, and `<#Option_showSnow>`
	- The web tag `<#snowfalling>` has been deprecated (it will return an empty string until it is removed)
	- A new daily graph data file `alldailysnowdata.json`
- Chill Hours now allows you to define a base temperature, where chill hours are only counted if the temperature is < threshold AND > base
	- The base temperature defaults to -99 (°C or °F) to mimic the current behaviour where chill hours are counted if the temperature is just < threshold
	- Some cold stratification of seeds in the UK for instance only counts chill hours when it is between 1°C and 10°C
- APRS/CWOP now sends the full "Ecowitt/Ambient/Tempest" station types
- Revised US EPA PM2.5 AQI index to match February 2024 update
- JSON station type now accepts laser distance measurements
- Adds weekly rainfall to the end of the realtime.txt file
- AWEKAS uploads to now increase the interval up to 10 minutes in the event of being rate limited
- Dashboard Select-a-Period chart now defaults to a range of one month
- Accessibility improvements to the Dashboard main menu
- User Alarms:
	- You can now use multiple web tags and arithmetic operators in the data field
	- You now have the option of data Equals value type alarms as well as Above and Below
- Changed Custom Alarms to User defined alarms in the dashboard menu
- Reverts the change in v4.2.0 where External Programs sets the working directory to the location of the executable/script rather than the Cumulus MX home directory

### Fixed

- Fix error editing extra log file data when MySQL updates enabled
- Lightning Time showing a 1900 date when no lightning has been detected
- Suppress Tempest station TaskCancellation message to the console on shutdown
- Ecowitt WH51 channels 9-16 decoding in HTTP API fixed(?) Still all undocumented by Ecowitt, sigh!
- Improvements to Ecowitt latest firmware checking
- Error message output processing historic Ecowitt AQI Combo PM10 data
- SQLite exception loading duplicate dayfile entries
- Fix for Davis WLL gust checking attributing gusts from the last minutes of the previous day to the current day
- Fix low battery warning for old model Ecowitt WH40 sensors that do not send the battery status when using the Ecowitt HTTP Local API
- Alarms: removes checks for File.Exists(Action) and traps FileNotFound exceptions instead
- Fix for new installs registering the year-to-date rainfall as todays rainfall
- Fix for alarm sounds not playing on main dashboard after the initial page load

### Package Updates

- MySqlConnector
- SSH.NET
- Sixlabors.ImageSharp
- NReco.Logging.File
- ServceStack.Text
- Lots of System/Microsoft packages updated from v8.0 to v9.0

---

## [4.2.1 \[b4043\]][8] - 2024-10-19

### New

- The web tag `<#DayFileQuery>` has been extended to allow "on this day" type queries.
	- Please read the separate documention (`/MXutils/QueryDayFile.md`) for more details
- The web tag `<#DayFileQuery>` has been extended to add the optional parameter dateOnly=y
- Please read the separate documention (`/MXutils/QueryDayFile.md`) for more details
	- The `Daily Data Query` page on the dashboard has also been extended to support "on this day" queries
- Ecowitt TCP API station now supports the LowBatteryList web tag
- Adds Brotli compression support to PHP uploads
	- Enabled via `Internet Settings > Web/Upload Site > Advanced Settings`
	- Using this option also requires PHP support for Brotli compression on your web server. Check this available before enabling this option
	- Also requires the latest version of `upload.php` to be uploaded to your web server
- Two new web tags for wind speeds. These allow you to extract the average and gust speeds for custom intervals. If for example you have 10 minutes defined as your average and gust speed intervals in Cumulus,
but you also want to see the two minute values then you can use these new tags
	- `<#WindAvgCust m=N>` - returns the average windspeed for the last N minutes
	- `<#WindGustCust m=N>` - returns the maximum gust value for the last N minutes
	- **Note:** *You should not use values for m greater than around 20 minutes due to the limited storage time of 'live' wind values. This is station update rate dependent*

### Changed

- The AQI web tag now returns a decimal value when using the Canada AQHI calculation<br>
	To return to the previous behaviour of using integer values, set your Air Quality decimal places to zero in `Station Settings > General Settings > Units > Advanced Options`
- The web tag `<#CO2_pm10_24_aqih>` has been corrected to `<#CO2_pm10_24h_aqi>`<br>
	If you use this tag in any of your files please amend your files to match the corrected tag name
- Clean-up of the AI2 pages for Interval and Daily Data viewers

### Fixed

- Interval data viewer not working over month ends for extra sensor values
- Ecowitt TCP API Station, incorrect interpretation of Ecowitt WH34 sensor low battery state
- Ecowitt TCP API Station, false detection of WS90 when a WH34 sensor was detected
- Web tag `<#ErrorLight>` is now functional again. It is triggered if Latest Error has any text, and cleared when you clear the latest error list from the interface
- Fix web tag `<#NewBuildNumber>` showing the latest build number as "0000"
- Error when logging enabled at start-up with SFTP uploads
- Windy pressure uploads failing
- Fix Ecowitt WS69 battery state decode in TCP API station

### Package Updates

- SQLite
- MailKit
- System.Diagnostics.PerformanceCounter
- System.ServiceProcess.ServiceController

---

## [4.2.0 \[b4039\]][7] - 2024-10-01

### New

- New station type: Ecowitt Local HTTP API
	- Ecowitt Local HTTP API is an alternative the local TCP API used by the gateways and some stations
	- Currently it is slightly less capable than the TCP API, but does provide all the sensor values
	- Allows direct support of Ecowitt stations that do not support the TCP API and currently have to use the Custom HTTP Server mode
- Exposes the UseDataLogger setting in `Station Settings > Options > Advanced`
- Implements a new data viewer where you can select and view historic data from the monthly logs for a given period, for a set of data values. See: `Data logs > Interval Data Viewer`
- Implements a new data viewer where you can select and view historic daily data from the day file for a given period, for a set of data values. See: `Data logs > Daily Data Viewer`
- New web tag `<#LowBatteryList>` which returns the list of sensors/transmitters that have low batteries
	- The format is a comma separated list of sensor/transmitter IDs and battery states
	- Eg. "wh80-&lt;state&gt;,wh41ch1-&lt;state&gt;,wh41ch2-&lt;state&gt;"
	- Where `<state>` is "LOW" or "0" or "1" depending on what the sensor sends
- New web tag `<#MonthRainfall>` which returns the rainfall total for the current month by default
	- Takes optional parameters `y=YYYY` and `m=MM` (both must be specified) to return the total rainfall for specified month in the specified year
	- Eg. `<#MonthRainfall y=2018 m=10>`
- Support for Ecowitt WS90 piezo IsRaining status to trigger MX IsRaining.
	- Currently only supported with a WS90/WS85 connected to a GW2000 (Sept. 2024). This value is being added to more stations as they get firmware updates.
- Two new web tags `<#NewRecordAlarm>` and `<#NewRecordAlarmMessage>`
	- NewRecordAlarm somewhat replicates the existing #newrecord web tag but is also controlled by the alarm being enable/disabled
	- NewRecordAlarmMessage displays the last new record alarm text message
- Old MD5 hash files are now deleted on startup
- New data viewer where you can query daily data in all sorts of flexible ways. See `Records > Daily Data Query`
- New web tag `<#DayFileQuery>` which allows flexible querying of the day file.
	- Please read the separate documention (`/MXutils/QueryDayFile.md`) for more details
- Added a script `/MXutils/linux/Fix_FineOffset_USB.sh` to fix Fine Offset USB stations

### Changed

- Davis WLL/Davis Cloud stations now use the WL.com subscription level to determine if they use WL.com as a logger
- The Station Settings screen now does a two-stage selection: First the manufacturer, then the station model. This shortens the long and list to select from.
- AI2 dashboard now shows some Davis WLL hardware information
- External Programs now sets the working directory to the location of the executable/script rather than the Cumulus MX home directory
- Latest AI2 updates applied
- Updates.txt is now `CHANGELOG.md`

### Fixed

- Davis Cloud stations in endless loop at startup if there is no historic data to process or access is denied
- Davis Cloud stations no longer continuously try to fetch history data if there is no Pro subscription
- WMR928 Station now correctly converts indoor temperatures to the user defined units
- Not logging PHP upload failures to the warning log
- Soil moisture units now follow the source
	- Example if you have a main station Davis with sensors 1 & 2 then their units will be cb; and you have Ecowitt extra sensors 3 & 4 then their units will be %
	- This does mean a change to the Units JSON and the graph scripts for the default web site. A re-upload of `/js/cumuluscharts.js` and `/js/selectachart.js` will be required
- Fix Davis Cloud Station continually attempting to download history data on error
- Fix a lurking problem with the `today.ini` and `yesterday.ini` files that has been there from day 0. Times are now stored as a full date/time
- Fix crash in Ecowitt getting station firmware version in some circumstances

### Package Updates

- SQLite: Reverted to v2.1.8 pending fix from author
- MQTTnet
- FluentFTP
- ServiceStack.Text
- SixLabors.ImageSharp

---

## [4.1.3 \[b4028\]][6] - 2024-08-20

### New

- New web tag `<#stationId>` which returns the internal station number used by CMX to determine the station type
- For Davis WLL and WeatherLink Cloud stations you can now specify the station identifier using the stations UUID instead of the numeric Id. The UUID is simpler to find
	as it forms part of the URL of every web page related to your station on weatherlink.com

### Changed

No changes

### Fixed

- The Cumulus MX version comparison with latest online at startup and daily
- Fix CMX version check when no betas are available on GitHub repo
- Davis Cloud Station can now accurately determine the current conditions update rate
- Fix Davis WLL (and others) creating erroneous wind speed spike warnings
- Alternative Interface 2 - Davis reception stats display incorrectly
- Davis Cloud Station (VP2) now correctly displays the Davis ET values when "Cumulus calculates ET" is not enabled
	- Note: If "Cumulus calculates ET" is not enabled the last hours ET every day will be accumulated in the first hour of the following day
- Davis WLL, and Davis Cloud stations, fixed a problem where the rollover would not be performed if historic data was not available and MX was stopped before the rollover and restarted after
- Improved Ctrl-C shutdown of Cumulus MX for Davis VP2 stations when they are failing to connect with the station
- Fix Ecowitt firmware check when running test firmware

### Package Updates

- SixLabors.ImageSharp
- FluentFTP
- MailKit
- NReco.Logging.File
- ServiceStack.Text
- SSH.Net
- SQLite

---

## [4.1.2 \[b4027\]][5] - 2024-07-23

### New

- Adds wind run to the dashboard "now" page
- Adds support for the format parameter to the `<#ProgramUpTime>` and `<#SystemUpTime>` web tags
	- The format syntax is different from date/time web tags as these two tags use a elapsed time.
	- For Custom format specifiers see: [Custom TimeSpan formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings)
	- For Standard format specifiers see: [Standard TimeSpan formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings)
	- The default output is generated using the format string `"{0:%d} days {0:%h} hours"`
	- You can customise this like this example: `<#SystemUpTime format="{0:%d}d {0:%h}h {0:%m}m">`  >> "12d 9h 46m"
- New web tag `<#AnnualRainfall>`
	- Defaults to the current year if no year is specified, so is equivalent to the preferred web tag `<#ryear>`
	- Accepts a tag parameter of `y=nnnn`, which will return the total rainfall for the specified year. Eg. `<#AnnualRainfall y=2021>`

### Changed

- Daily backup now runs asynchronously to prevent it stopping MX continue to run

### Fixed

- Davis VP2 connection type being decoded from Cumulus.ini incorrectly
- Add missing knots input to JSON station
- Solar W/m2 should use superscript in dashboard
- Cumulus Calculates SLP giving a spike on start-up with the following stations: Ecowitt Local API, Davis Cloud Station, Ecowitt Cloud Station, HTTP Ambient, HTTP Ecowitt
- Add missing wind run units and extra space in humidity % in ai2 dashboard
- Remove UV/Solar missing data messages from Davis Cloud (VP2)
- A new version of ***MigrateData3to4*** (1.0.3) to fix issues migrating the day file
- Negative 0.0 appearing when no rainfall has occurred
- Davis Cloud Station wind processing changed, instead of MX trying to calculate an average wind speed, MX now uses the wind speed data from Davis directly.<br>
	The data granularity from the cloud is not enough for the average to be calculated.

---

## [4.1.1 \[b4025\]][4] - 2024-06-19

### New

No new features

### Changed

No changes

### Fixed

- Davis VP2/Vue raincounter reset problems
- Another raincounter reset issue that has been lurking
- Wizard made Ecowitt API key and secret mandatory
- Fix for FTP overwrite performing delete + create of remote file

---

## [4.1.0 \[b4024\]][3] - 2024-06-05

### New

- HTTP (Ecowitt) station now accepts the data via a simple GET url as well as POST
- Cumulus now calculates the AQi for Ecowitt PM and CO₂ sensors
	- New web tags:

	`<#AirQualityIdx[1-4]>`, `<#AirQualityAvgIdx[1-4]>`<br>
	`<#CO2_pm2p5_aqi>`, `<#CO2_pm2p5_24h_aqi>`<br>
	`<#CO2_pm10_aqi>`, `<#CO2_pm10_24_aqi>`

- Add new pressure units option of kilopascal (kPa)
- New station type added: JSON Data Input, marked as "experimental" for now, but testing so far has been successful
	- Accepts data in a JSON format defined in `MXutils/WeatherStationInput.jsonc`
	- Input mechanism is via:
		- Named file
		- HTTP POST to `http://[CMX_IP_Address]:8998/station/json`
		- MQTT using a named topic
- Locale Strings now has settings for the default record date/time text

### Changed

- Removed option for WOW catch-up, it isn't supported by WOW
- Moved the log file header info files to the `MXutils/fileheaders` folder

### Fixed

- Temperature Sum graph data when Sum0 is the only selected range
- Fix `<#NewBuildAvailable>` and `<#NewBuildNumber>` web tags
- Fix for Davis VP2 consoles losing todays rainfall on a full power cycle
- Exception when enabling real-time FTP whilst running and FTP logging is enabled
- Davis WLL now fires a single "sensor contact lost" warning message + contact restored
- Fix for multiple realtime FTP log-ins being attempted in parallel
- Alarm actions errored if the action parameter field is empty

### Package Updates

- MQTTnet
- MailKit
- BouncyCastle

---

## [4.0.1 \[b4023\]][2] - 2024-05-16

### New

- There is now a 32 Windows specific version of executable - `CumulusMX32.exe`
	- The same applies to `MigrateData3to4` and `CreateMissing`

### Changed

- Removed the experimental Gmail OATH2 authentication method
- Third party uploads now have retries and the timeout increased to 30 seconds


### Fixed

- Fixed Spike handling for outdoor temperature
- Fixed David Cloud (VP2) station sometimes not decoding dew point, adds indoor temp/hum decode
- The -install option now works on 32 bit Windows

---

## [4.0.0 \[b4022\]][1] - 2024-05-11

Initial release of Cumulus MX which now runs under Microsoft .NET 8.0 and removes the requirement for the Mono runtime environment on Linux.

### New

- Moon Image now supports transparent shadows
- The -install/-unistall command line switches now support both Windows and Linux
	- Under Linux run<br>
	`sudo dotnet CumulusMX.dll -install -user <username> [-port <port_number>] [-lang <lang-code>]`
	- Windows install-as-a-service now self-elevates and requests UAC
- Implements encryption of the credentials in the cumulus.ini file
	- This requires a new file in the root folder called `UniqueId.txt`
	- You **must** copy this `UniqueId.txt` file when you copy the `cumulus.ini` file to new installs, otherwise your sensitive information will not decrypt and you will have to enter it again
- Experimental Gmail OATH 2.0 authentication
- New web tag for the average temperature of the previous 24 hours from now: `<#TempAvg24Hrs>`
- Cumulus backups are now zipped
- Add Enable option to Extra Web Files so you can now save entries but not have them active
- Ecowitt - added firmware update check on start-up and once a day at 13:00
	- New Firmware Alarm to support this
	- New web tag `<#FirmwareAlarm>`
- Adds new web tags for temperature means<br>
	`<#ByMonthTempAvg mon=[1-12]>`<br>
    Mean for requested month over the entire history. Omit the `mon` parameter for the current month<br>
	`<#MonthTempAvg m=[1-12] y=[YYYY]>`<br>
    Mean for the requested specific month. Omit the parameters for the current month<br>
	`<#YearTempAvg y=[YYYY]>`<br>
    Mean for the requested year. Omit the y parameter for the current year
- Add "MX calculates Sea Level Pressure"
	- Applies to HTTP Ecowitt, HTTP Ambient, GW1000, Ecowitt Cloud, FO, Davis Cloud WLC stations
	- When enabled, the pressure calibration is applied to the raw station pressure
	- Check your station pressure (Absolute) calibration!
- Adds true Altimeter Pressure calculation to GW1000, Ecowitt HTTP, Ecowitt Cloud
	- Check your station pressure (Absolute) calibration!
- Added localisation of records web tag date/time formats

### Changed

- Now **requires** Microsoft .Net 8.0 rather than mono to run under Linux and MacOS
- All data files are now written/read as invariant - dayfile, monthly log files, extra log files, AirLink, and custom log files
	- NOTE: Custom log files may require the user to alter their configuration to use comma separators and add the `rc=y` parameter to numeric web tags
- Monthly log files now renamed to `[yyyyMM]log.txt` to remove localised month name - and now sortable in the file system!
- Added `MigrateData3to4` utility.
	Basic workflow:
		- Clean install v4
		- Copy v3 Cumulus.ini to root
		- Copy v3 `/data` and `/Reports` folders to v4 install
		- Rename the `/data` folder to `/datav3`
		- Run `MigrateData3to4`
		- Done!
- Removed previously deprecated web tags
	`CO2-24h, CO2-pm2p5, CO2-pm2p5-24h, CO2-pm10, CO2-temp, CO2-hum`
- Loading dayfile now continues on error and reports total errors - only the first 20 errors are logged
- You now only set the Ecowitt MAC/IMEI address in one place for the various station types
	- In Local API settings for GW1000 type
	- In Cloud Access API for Cloud and HTTP station types

### Fixed

- Problems when using a 9am rollover in the records editors for values from the monthly log files
- Select-a-Period charts not respecting the interval dates: Air Quality, CO₂, Soil Moisture, Leaf Wetness
- Calibration Limits not changing when the user changes units - eg initial install
- Potential fix for corruption at the end of all data log files when shutting down
- Error that the username is not set when sending email to a server that requires no authentication
- Improvement to GW1000 API reconnects
- Improved web socket initial connection to send data immediately on dashboard/now/gauges connection
- Fix for soil moisture conversion from percentage to cb in Weather Cloud uploads
- Reload dayfile can now only be run as a single instance
- Improvements to Davis WLL wind handling when:
	- Transitioning from catch-up to live running
	- No broadcasts are received
- Davis WLL improved recovery from loss of broadcast messages
- Spike/limit improvements


[1]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4022
[2]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4023
[3]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4024
[4]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4025
[5]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4027
[6]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4028
[7]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4039
[8]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4043
[9]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4063
[10]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4064
[11]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4067
[12]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4070
[13]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4083
[14]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4084
[15]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4085
[16]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4086
[17]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4087
[18]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4088
[19]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4102
[20]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4103
[21]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4104
[22]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4105
[23]: https://github.com/cumulusmx/CumulusMX/releases/tag/b4113
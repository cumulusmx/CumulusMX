# Changelog

All notable changes to this project will be documented in this file.

Additional notes are available on the [forum release thread](https://cumulus.hosiene.co.uk/viewtopic.php?t=17887)

This file is formatted as [markdown](https://www.markdownguide.org/), any decent editor should display it correctly formatted.
Alternatively, view it [online on GitHub](https://github.com/cumulusmx/CumulusMX/blob/main/CHANGELOG.md)

---


## 4.2.1 \[b4040\] - 2024-??-??

### New
No new features

### Changed
- The AQI web tag now returns a decimal value when using the Canada AQHI calculation<br>
	To return to the previous behaviour of using integer values, set your Air Quality decimal places to zero in `Station Settings > General Settings > Units > Advanced Options`

### Fixed
- Interval data viewer not working over month ends for extra sensor values

### Package Updates
No package updates

---

## 4.2.0 \[b4039\] - 2024-10-01

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
	- NewRecordAlarm somewhat replicates the existing #newrecord web tag, but is also controlled by the alarm being enable/disabled
	- NewRecordAlarmMessage displays the last new record alarm text message
- Old MD5 hash files are now deleted on startup
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
	- Example if you have a main station Davis with sensors 1 & 2, their units will be cb, and you have Ecowitt extra sensors 3 & 4, their units will be %
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

## 4.1.3 \[b4028\] - 2024-08-20

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
	- Note: If "Cumulus calculates ET" is not enabled, the last hours ET every day, will be accumulated in the first hour of the following day
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

## 4.1.2 \[b4027\] - 2024-07-23

### New
- Adds wind run to the dashboard "now" page
- Adds support for the format parameter to the `<#ProgramUpTime>` and `<#SystemUpTime>` web tags
	- The format syntax is different from date/time web tags as these two tags use a elapsed time.
	- For Custom format specifiers see: [Custom TimeSpan formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings)
	- For Standard format specifiers see: [Standard TimeSpan formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings)
	- The default output is generated using the format string `"{0:%d} days {0:%h} hours"`
	- You can customise this like this example: `<#SystemUpTime format="{0:%d}d {0:%h}h {0:%m}m">`  --> "12d 9h 46m"
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

## 4.1.1 \[b4025\] - 2024-06-19

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

## 4.1.0 \[b4024\] - 2024-06-05

### New
- HTTP (Ecowitt) station now accepts the data via a simple GET url as well as POST
- Cumulus now calculates the AQi for Ecowitt PM and CO₂ sensors
	- New web tags:

	`<#AirQualityIdx1[-4]>`, `<#AirQualityAvgIdx1[-4]>`<br>
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

## 4.0.1 \[b4023\] - 2024-05-16

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

## 4.0.0 \[b4022\] - 2024-05-11

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

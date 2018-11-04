/*!
 * A starter gauges page for Cumulus and Weather Display, based
 * on the JavaScript SteelSeries gauges by Gerrit Grunwald.
 *
 * Created by Mark Crossley, July 2011
 *  see scriptVer below for latest release
 *
 * Released under GNU GENERAL PUBLIC LICENSE, Version 2, June 1991
 * See the enclosed License file
 *
 * File encoding = UTF-8
 *
 */
/*globals steelseries, LANG, changeLang */
/*jshint jquery:true,nomen:false,plusplus:false */

/*! Tiny Pub/Sub - v0.7.0 - 2013-01-29
* https://github.com/cowboy/jquery-tiny-pubsub
* Copyright (c) 2013 "Cowboy" Ben Alman; Licensed MIT */
(function($) {
    'use strict';
    var o = $({});
    $.subscribe = function() {o.on.apply(o, arguments);};
    $.unsubscribe = function() {o.off.apply(o, arguments);};
    $.publish = function() {o.trigger.apply(o, arguments);};
}(jQuery));

var gauges = (function () {
    'use strict';
    var strings = LANG.EN,         //Set to your default language. Store all the strings in one object
        config = {
            // Script configuration parameters you may want to 'tweak'
            scriptVer         : '2.5.5',
            weatherProgram    : 0,                      //Set 0=Cumulus, 1=Weather Display, 2=VWS, 3=WeatherCat, 4=Meteobridge, 5=WView, 6=WeeWX
            imgPathURL        : 'images/',             //*** Change this to the relative path for your 'Trend' graph images
            oldGauges         : 'gauges.htm',           //*** Change this to the relative path for your 'old' gauges page.
            realtimeInterval  : 5,                      //*** Download data interval, set to your realtime data update interval in seconds
            longPoll          : false,                  // if enabled, use long polling and PHP generated data !!only enable if you understand how this is implemented!!
            gaugeMobileScaling: 0.85,                   //scaling factor to apply when displaying the gauges mobile devices, set to 1 to disable (default 0.85)
            graphUpdateTime   : 15,                     //period of pop-up data graph refresh, in minutes (default 15)
            stationTimeout    : 3,                      //period of no data change before we declare the station off-line, in minutes (default 3)
            pageUpdateLimit   : 20,                     //period after which the page stops automatically updating, in minutes (default 20),
                                                        // - set to 0 (zero) to disable this feature
            pageUpdatePswd    : 'its-me',               //password to over ride the page updates time-out, do not set to blank even if you do not use a password
            digitalFont       : false,                  //Font control for the gauges & timer
            digitalForecast   : false,                  //Font control for the status display, set this to false for languages that use accented characters in the forecasts
            showPopupData     : true,                   //Pop-up data displayed
            showPopupGraphs   : false,                   //If pop-up data is displayed, show the graphs?
            mobileShowGraphs  : false,                  //If false, on a mobile/narrow display, always disable the graphs
            showWindVariation : true,                   //Show variation in wind direction over the last 10 minutes on the direction gauge
            showIndoorTempHum : true,                   //Show the indoor temperature/humidity options
            showCloudGauge    : true,                   //Display the Cloud Base gauge
            showUvGauge       : true,                   //Display the UV Index gauge
            showSolarGauge    : true,                   //Display the Solar gauge
            showSunshineLed   : true,                   //Show 'sun shining now' LED on solar gauge
            showRoseGauge     : true,                   //Show the optional Wind Rose gauge
            showRoseGaugeOdo  : true,                   //Show the optional Wind Rose gauge wind run Odometer
            showRoseOnDirGauge: true,                   //Show the rose data as sectors on the direction gauge
            showGaugeShadow   : true,                   //Show a drop shadow outside the gauges
                                                        // The realtime files should be absolute paths, "/xxx.txt" refers to the public root of your web server
            realTimeURL_LongPoll: 'realtimegauges-longpoll.php',     //*** ALL Users: If using long polling, change to your location of the PHP long poll realtime file ***
            realTimeURL_Cumulus: 'realtimegauges.txt',     //*** Cumulus Users: Change to your location of the realtime file ***
            realTimeURL_WD     : 'customclientraw.txt',    //*** WD Users: Change to your location of the ccr file ***
            realTimeURL_VWS    : 'steelseriesVWSjson.php',  //*** VWS Users: Change to your location of the JSON script generator ***
            realTimeURL_WC     : 'realtimegaugesWC.txt',   //*** WeatherCat Users: Change to your location of the JSON script generator ***
            realTimeURL_MB     : 'MBrealtimegauges.txt',   //*** Meteobridge Users: Change to the location of the JSON file
            realTimeURL_WView  : 'customclientraw.txt',    //*** WView Users: Change to your location of the customclientraw.txt file ***
            realTimeURL_weewx  : 'gauge-data.txt',         //*** WeeWX Users: Change to your location of the gauge data file ***
            useCookies        : true,                   //Persistently store user preferences in a cookie?
            tipImages         : [],
            dashboardMode     : false,                  //Used by Cumulus MX dashboard - SET TO FALSE OTHERWISE
            dewDisplayType    : 'app'                   //Initial 'scale' to display  'dew' - Dewpoint
                                                        // on the 'dew point' gauge.  'app' - Apparent temperature
                                                        //                            'wnd' - Wind Chill
                                                        //                            'hea' - Heat Index
                                                        //                            'hum' - Humidex
        },

        //Gauge global look'n'feel settings
        gaugeGlobals = {
            minMaxArea             : 'rgba(212,132,134,0.3)', //area sector for today's max/min. (red, green, blue, transparency)
            windAvgArea            : 'rgba(132,212,134,0.3)',
            windVariationSector    : 'rgba(120,200,120,0.7)', //only used when rose data is shown on direction gauge
            frameDesign            : steelseries.FrameDesign.TILTED_GRAY,
            background             : steelseries.BackgroundColor.BEIGE,
            foreground             : steelseries.ForegroundType.TYPE1,
            pointer                : steelseries.PointerType.TYPE8,
            pointerColour          : steelseries.ColorDef.RED,
            dirAvgPointer          : steelseries.PointerType.TYPE8,
            dirAvgPointerColour    : steelseries.ColorDef.BLUE,
            gaugeType              : steelseries.GaugeType.TYPE4,
            lcdColour              : steelseries.LcdColor.STANDARD,
            knob                   : steelseries.KnobType.STANDARD_KNOB,
            knobStyle              : steelseries.KnobStyle.SILVER,
            labelFormat            : steelseries.LabelNumberFormat.STANDARD,
            tickLabelOrientation   : steelseries.TickLabelOrientation.HORIZONTAL, // was .NORMAL up to v1.6.4
            rainUseSectionColours  : false,                                       // Only one of these colour options should be true
            rainUseGradientColours : false,                                       // Set both to false to use the pointer colour
            tempTrendVisible       : true,
            pressureTrendVisible   : true,
            uvLcdDecimals          : 1,
            // sunshine threshold values
            sunshineThreshold      : 50,    // the value in W/m² above which we can consider the Sun to be shining, *if* the current value exceeds...
            sunshineThresholdPct   : 75,    // the percentage of theoretical solar irradiance above which we consider the Sun to be shining
            // default gauge ranges - before auto-scaling/ranging
            tempScaleDefMinC       : -20,
            tempScaleDefMaxC       : 40,
            tempScaleDefMinF       : 0,
            tempScaleDefMaxF       : 100,
            baroScaleDefMinhPa     : 990,
            baroScaleDefMaxhPa     : 1030,
            baroScaleDefMinkPa     : 99,
            baroScaleDefMaxkPa     : 103,
            baroScaleDefMininHg    : 29.2,
            baroScaleDefMaxinHg    : 30.4,
            windScaleDefMaxMph     : 20,
            windScaleDefMaxKts     : 20,
            windScaleDefMaxMs      : 10,
            windScaleDefMaxKmh     : 30,
            rainScaleDefMaxmm      : 10,
            rainScaleDefMaxIn      : 0.5,
            rainRateScaleDefMaxmm  : 10,
            rainRateScaleDefMaxIn  : 0.5,
            uvScaleDefMax          : 10,				//Northern Europe may be lower - max. value recorded in the UK is 8, so use a scale of 10 for UK
            solarGaugeScaleMax     : 1000,              //Max value to be shown on the solar gauge - theoretical max without atmosphere ~ 1374 W/m²
                                                        // - but Davis stations can read up to 1800, use 1000 for Northern Europe?
            cloudScaleDefMaxft     : 3000,
            cloudScaleDefMaxm      : 1000,
            shadowColour           : 'rgba(0,0,0,0.3)'  //Colour to use for gauge shadows - default 30% transparent black
        },

        commonParams = {
            // Common parameters for all the SteelSeries gauges
            fullScaleDeflectionTime : 4,				//Bigger numbers (seconds) slow the gauge pointer movements more
            gaugeType               : gaugeGlobals.gaugeType,
            minValue                : 0,
            niceScale               : true,
            ledVisible              : false,
            frameDesign             : gaugeGlobals.frameDesign,
            backgroundColor         : gaugeGlobals.background,
            foregroundType          : gaugeGlobals.foreground,
            pointerType             : gaugeGlobals.pointer,
            pointerColor            : gaugeGlobals.pointerColour,
            knobType                : gaugeGlobals.knob,
            knobStyle               : gaugeGlobals.knobStyle,
            lcdColor                : gaugeGlobals.lcdColour,
            lcdDecimals             : 1,
            digitalFont             : config.digitalFont,
            tickLabelOrientation    : gaugeGlobals.tickLabelOrientation,
            labelNumberFormat       : gaugeGlobals.labelFormat
        },
        _firstRun = true,          //Used to set-up units & scales etc
        _userUnitsSet = false,     //Tracks if the display units have been set by a user preference
        data = {},                 //Stores all the values from realtime.txt
        _tickTockInterval,         //The 1s clock interval timer

                                   // _ajaxDelay, used by long polling, the delay between getting a response and queueing the next request, as the default PHP
                                   // script runtime timout is 30 seconds, we do not want the PHP task to run for more than 20 seconds. So queue the next
                                   // request half of the realtime interval, or 20 seconds before it is due, which ever is the larger.
        _ajaxDelay = config.longPoll ? Math.max(config.realtimeInterval - 20, 0) : config.realtimeInterval,
        _downloadTimer,            //Stores a reference to the ajax download setTimout() timer
        _timestamp = 0,            //the timestamp of last data update on the server.
        _jqXHR = null,             //handle to the jQuery web request
        _httpError = 0,            //global to track download errors
        _pageUpdateParam,          //Stores the password if any from the page URL
        _displayUnits = null,      //Stores the display units cookie settings
        _sampleDate,
        _realtimeVer,   //minimum version of the realtime JSON file required
        _programLink = ['<a href="http://sandaysoft.com/products/cumulus" target="_blank">Cumulus</a>',
                        '<a href="http://www.weather-display.com/" target="_blank">Weather Display</a>',
                        '<a href="http://www.ambientweather.com/virtualstation.html" target="_blank">Virtual Weather Station</a>',
                        '<a href="http://trixology.com/weathercat/" target="_blank">WeatherCat</a>',
                        '<a href="http://www.meteobridge.com/" target="_blank">Meteobridge</a>',
                        '<a href="http://www.wviewweather.com/" target="_blank">Wview</a>',
                        '<a href="http://www.weewx.com/" target="_blank">weewx</a>'],

        ledIndicator, statusScroller, statusTimer,

        gaugeTemp, gaugeDew, gaugeRain, gaugeRRate,
        gaugeHum, gaugeBaro, gaugeWind, gaugeDir,
        gaugeUV, gaugeSolar, gaugeCloud, gaugeRose,

        /* _imgBackground,    // Uncomment if using a background image on the gauges */

        // ==================================================================================================================
        // Nothing below this line needs to be modified for the gauges as supplied
        // - unless you really know what you are doing
        // - but remember, if you break it, it's up to you to fix it ;-)
        // ==================================================================================================================

        //
        // init() Called when the document is ready, pre-draws the Status Display then calls
        // the first Ajax fetch of realtimegauges.txt. First draw of the gauges now deferred until
        // the Ajax data is available as a 'speed up'.
        //
        init = function (dashboard) {

            // Cumulus, Weather Display, VWS, WeatherCat?
            switch (config.weatherProgram) {
            case 0:
                // Cumulus
                _realtimeVer = 12;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_Cumulus;
                // the trend images to be used for the pop-up data, used in conjunction with config.imgPathURL
                // by default this is configured for the Cumulus 'standard' web site
                // ** If you specify one image in a sub-array, then you MUST provide images for all the other sub-elements
                config.tipImgs = [                                  // config.tipImgs for Cumulus users using the 'default' weather site
                    ['temp.png', 'intemp.png'],                     // Temperature: outdoor, indoor
                    // Temperature: dewpoint, apparent, windChill, heatIndex, humidex
                    ['temp.png', 'temp.png', 'temp.png', 'temp.png', 'temp.png'],
                    'raint.png',                                    // Rainfall
                    'rain.png',                                     // Rainfall rate
                    ['hum.png', 'hum.png'],                         // Humidity: outdoor, indoor
                    'press.png',                                    // Pressure
                    'wind.png',                                     // Wind speed
                    'windd.png',                                    // Wind direction
                    (config.showUvGauge ? 'uv.png' : null),         // UV graph if UV sensor is present | =null if no UV sensor
                    (config.showSolarGauge ? 'solar.png' : null),   // Solar rad graph if Solar sensor is present | Solar =null if no Solar sensor
                    (config.showRoseGauge ? 'windd.png' : null),    // Wind direction if Rose is enabled | =null if Rose is disabled
                    (config.showCloudGauge ? 'press.png' : null)    // Pressure for cloud height | =null if Cloud Height is disabled
                ];
                break;
            case 1:
                // Weather Display
                _realtimeVer = 12;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_WD;
                config.tipImgs = [                                      // config.tipImgs for Weather Display users with wxgraph
                    ['temp+hum_24hr.php', 'indoor_temp_24hr.php'],      // Temperature: outdoor, indoor
                    // Temperature: dewpnt, apparent, windChill, HeatIndx, humidex
                    ['temp+dew+hum_1hr.php', 'temp+dew+hum_1hr.php', 'temp+dew+hum_1hr.php', 'temp+dew+hum_1hr.php', 'temp+dew+hum_1hr.php'],
                    'rain_24hr.php',                                    // Rainfall
                    'rain_1hr.php',                                     // Rainfall rate
                    ['humidity_1hr.php', 'humidity_7days.php'],         // Humidity: outdoor, indoor
                    'baro_24hr.php',                                    // Pressure
                    'windgust_1hr.php',                                 // Wind speed
                    'winddir_24hr.php',                                 // Wind direction
                    (config.showUvGauge ? 'uv_24hr.php' : null),        // UV graph if UV sensor is present | =null if no UV sensor
                    (config.showSolarGauge ? 'solar_24hr.php' : null),  // Solar rad graph if Solar sensor is present | Solar =null if no Solar sensor
                    (config.showRoseGauge ? 'winddir_24hr.php' : null), // Wind direction if Rose is enabled | =null if Rose is disabled
                    (config.showCloudGauge ? 'baro_24hr.php' : null)    // Pressure for cloud height | =null if Cloud Height is disabled
                ];
                break;
            case 2:
                // WVS
                _realtimeVer = 11;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_VWS;
                config.showRoseGauge = false; // no wind rose data from VWS
                config.showCloudGauge = false;
                config.tipImgs = [                                  // config.tipImgs for VWS users
                    ['vws742.jpg', 'vws741.jpg'],                   // Temperature: outdoor, indoor
                    // Temperature: dewpnt, apparent, windChill, HeatIndx, humidex
                    ['vws757.jpg', 'vws762.jpg', 'vws754.jpg', 'vws756.jpg', null],
                    'vws744.jpg',                                   // Rainfall
                    'vws859.jpg',                                   // Rainfall rate
                    ['vws740.jpg', 'vws739.jpg'],                   // Humidity: outdoor, indoor
                    'vws758.jpg',                                   // Pressure
                    'vws737.jpg',                                   // Wind speed
                    'vws736.jpg',                                   // Wind direction
                    (config.showUvGauge ? 'vws752.jpg' : null),     // UV graph if UV sensor is present | =null if no UV sensor
                    (config.showSolarGauge ? 'vws753.jpg' : null),  // Solar rad graph if Solar sensor is present | Solar =null if no Solar sensor
                    (config.showRoseGauge ? 'vws736.jpg' : null),   // Wind direction if Rose is enabled | =null if Rose is disabled
                    (config.showCloudGauge ? 'vws758.jpg' : null)   // Pressure for cloud height | =null if Cloud Height is disabled
                ];
                break;
            case 3:
                // WeatherCat
                _realtimeVer = 13;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_WC;
                config.tipImgs = [                                       // config.tipImgs - WeatherCat users using the 'default' weather site
                    ['temperature1.jpg', 'tempin1.jpg'],                 // Temperature: outdoor, indoor
                    // Temperature: dewpoint, apparent, windChill, heatIndex, humidex
                    ['dewpoint1.jpg', 'temperature1.jpg', 'windchill1.jpg', 'heatindex1.jpg', 'temperature1.jpg'],
                    'precipitationc1.jpg',                               // Rainfall
                    'precipitation1.jpg',                                // Rainfall rate
                    ['rh1.jpg', 'rhin1.jpg'],                            // Humidity: outdoor, indoor
                    'pressure1.jpg',                                     // Pressure
                    'windspeed1.jpg',                                    // Wind speed
                    'winddirection1.jpg',                                // Wind direction
                    (config.showUvGauge ? 'uv1.jpg' : null),             // UV graph if UV sensor is present | =null if no UV sensor
                    (config.showSolarGauge ? 'solarrad1.jpg' : null),    // Solar rad graph if Solar sensor is present | Solar =null if no Solar sensor
                    (config.showRoseGauge ? 'winddirection1.jpg' : null),// Wind direction if Rose is enabled | =null if Rose is disabled
                    (config.showCloudGauge ? 'cloudbase1.jpg' : null)     // Pressure for cloud height | =null if Cloud Height is disabled
                ];
                break;
            case 4:
                // Meteobridge
                _realtimeVer = 10;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_MB;
                config.showPopupGraphs = false;        // config.tipImgs - no Meteobridge images available
                config.showRoseGauge = false;          // no windrose data from MB
                config.showCloudGauge = false;
                config.tipImgs = null;                 // config.tipImgs - no Meteobridge images available
                break;
            case 5:
                // WView
                _realtimeVer = 10;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_WView;
                config.showSunshineLed = false;     //WView does not provide the current theoretical solar max required to determine sunshine
                config.showWindVariation = false;   //no wind variation from WView
                config.showRoseGauge = false;       //No wind rose array from WView by default - there is a user supplied modification to enable it though.
                config.showCloudGauge = false;
                config.tipImgs = [                                      // config.tipImgs for WView users
                    ['tempdaycomp.png', 'indoor_temp.png'],             // Temperature: outdoor, indoor
                    // Temperature: dewpnt, apparent, windChill, HeatIndx, humidex
                    ['tempdaycomp.png', 'tempdaycomp.png', 'heatchillcomp.png', 'heatchillcomp.png', 'tempdaycomp.png'],
                    'rainday.png',                                      // Rainfall
                    'rainrate.png',                                     // Rainfall rate
                    ['humidday.png', 'humidday.png'],                   // Humidity: outdoor, indoor
                    'baromday.png',                                     // Pressure
                    'wspeeddaycomp.png',                                // Wind speed
                    'wdirday.png',                                      // Wind direction
                    (config.showUvGauge ? 'uvday.png' : null),          // UV graph if UV sensor is present | =null if no UV sensor
                    (config.showSolarGauge ? 'solarday.png' : null),    // Solar rad graph if Solar sensor is present | Solar =null if no Solar sensor
                    (config.showRoseGauge ? 'windroseday.png' : null),  // Wind direction if Rose is enabled | =null if Rose is disabled
                    (config.showCloudGauge ? 'baromday.png' : null)     // Pressure for cloud height | =null if Cloud Height is disabled
                ];
                break;
            case 6:
                // weewx
                _realtimeVer = 12;   //minimum version of the realtime JSON file required
                config.realTimeURL = config.longPoll ? config.realTimeURL_LongPoll : config.realTimeURL_weewx;
                config.showSunshineLed = true;
                config.showWindVariation = true;
                config.showRoseGauge = false;       // FIXME: add wind histogram to weewx SLE
                config.tipImgs = [                                      // config.tipImgs for weewx
                    ['dayinouttemp.png', 'dayinouttemp.png'],           // Temperature: outdoor, indoor
                    // Temperature: dewpnt, apparent, windChill, HeatIndx, humidex
                    ['dayouttemphum.png', 'dayouttemphum.png', 'dayouttemphum.png', 'dayouttemphum.png', 'dayouttemphum.png'],
                    'dayrain.png',                                      // Rainfall
                    'dayrainrate.png',                                  // Rainfall rate
                    ['dayinouthum.png', 'dayinouthum.png'],             // Humidity: outdoor, indoor
                    'daybarometer.png',                                 // Pressure
                    'daywind.png',                                      // Wind speed
                    'daywinddir.png',                                   // Wind direction
                    (config.showUvGauge ? 'dayuv.png' : null),          // UV graph if UV sensor is present | =null if no UV sensor
                    (config.showSolarGauge ? 'dayradiation.png' : null),// Solar rad graph if Solar sensor is present | Solar =null if no Solar sensor
                    (config.showRoseGauge ? 'daywindvec.png' : null),   // Wind direction if Rose is enabled | =null if Rose is disabled
                    (config.showCloudGauge ? 'daybarometer.png' : null) // Pressure for cloud height | =null if Cloud Height is disabled
                ];
                break;
            default:
                // Who knows!
                _realtimeVer = 0;   //minimum version of the realtime JSON file required
                config.realtimeURL = null;
                config.showPopupGraphs = false;
                config.tipImgs = null;                     // config.tipImgs - unknown
            }


            // Are we running on a phone device (or really low res screen)?
            if ($(window).width() < 480) {
                // Change the gauge scaling
                config.gaugeScaling = config.gaugeMobileScaling;
                // Switch off graphs?
                config.showPopupGraphs = config.mobileShowGraphs;
            } else {
                config.gaugeScaling = 1;
            }

            // Logo images are used to 'personalise' the gauge backgrounds
            // To add a logo to a gauge, add the parameter:
            //    params.customLayer = _imgBackground;
            // to the corresponding drawXxxx() function below.
            //
            // These are for demo only, to add them remove the comments around the following lines, and
            // the _imgBackground definition line above...
            /*
            _imgBackground = document.createElement('img');                 // small logo
            $(_imgBackground).attr('src', config.imgPathURL + 'logoSmall.png');
            */
            // End of logo images


            // Get the display units the user last used when they visited before - if present
            _displayUnits = getCookie('units');
            // Set 'units' radio buttons to match preferred units
            if (_displayUnits !== null) {
                //User wants specific units
                _userUnitsSet = true;

                // temperature
                setRadioCheck('rad_unitsTemp', _displayUnits.temp);
                data.tempunit = '°' + _displayUnits.temp;
                // rain
                setRadioCheck('rad_unitsRain', _displayUnits.rain);
                data.rainunit = _displayUnits.rain;
                // pressure
                setRadioCheck('rad_unitsPress', _displayUnits.press);
                data.pressunit = _displayUnits.press;
                // wind
                setRadioCheck('rad_unitsWind', _displayUnits.wind);
                data.windunit = _displayUnits.wind;
                _displayUnits.windrun = getWindrunUnits(data.windunit);
                // cloud base
                setRadioCheck('rad_unitsCloud', _displayUnits.cloud);
                data.cloudunit = _displayUnits.cloud;
            } else {
                // Set the defaults to metric )
                // DO NOT CHANGE THESE - THE SCRIPT DEPENDS ON THESE DEFAULTS
                // The units actually displayed will be read from the realtime.txt file, or from the users last visit cookie
                _displayUnits = {
                    temp: 'C',
                    rain: 'mm',
                    press: 'hPa',
                    wind: 'km/h',
                    windrun: 'km',
                    cloud: 'm'
                };

                data.tempunit = '°C';
                data.rainunit = 'mm';
                data.pressunit = 'hPa';
                data.windunit = 'km/h';
                data.cloudunit = 'm';
            }


            // enable popup data
            if (config.showPopupData) {
                ddimgtooltip.showTips = config.showPopupData;
            }

            if (config.showPopupGraphs) {
                // Note the number of array elements must match 'i' in ddimgtooltip.tiparray()
                ddimgtooltip.tiparray[0][0] = (config.tipImgs[0][0] === null ? null : '');
                ddimgtooltip.tiparray[1][0] = (config.tipImgs[1][0] === null ? null : '');
                ddimgtooltip.tiparray[2][0] = (config.tipImgs[2]    === null ? null : '');
                ddimgtooltip.tiparray[3][0] = (config.tipImgs[3]    === null ? null : '');
                ddimgtooltip.tiparray[4][0] = (config.tipImgs[4][0] === null ? null : '');
                ddimgtooltip.tiparray[5][0] = (config.tipImgs[5]    === null ? null : '');
                ddimgtooltip.tiparray[6][0] = (config.tipImgs[6]    === null ? null : '');
                ddimgtooltip.tiparray[7][0] = (config.tipImgs[7]    === null ? null : '');
                ddimgtooltip.tiparray[8][0] = (config.tipImgs[8]    === null ? null : '');
                ddimgtooltip.tiparray[9][0] = (config.tipImgs[9]    === null ? null : '');
                ddimgtooltip.tiparray[10][0] = (config.tipImgs[10]  === null ? null : '');
                ddimgtooltip.tiparray[11][0] = (config.tipImgs[11]  === null ? null : '');
            }

            // draw the status gadgets first, they will display any errors in the initial set-up
            ledIndicator = singleLed.getInstance();
            statusScroller = singleStatus.getInstance();
            statusTimer = singleTimer.getInstance();


            gaugeTemp = singleTemp.getInstance();
            if (gaugeTemp) { gauges.doTemp = gaugeTemp.update; }

            gaugeDew = singleDew.getInstance();
            if (gaugeDew) { gauges.doDew = gaugeDew.update; }

            gaugeHum = singleHum.getInstance();
            if (gaugeHum) { gauges.doHum = gaugeHum.update; }

            gaugeBaro = singleBaro.getInstance();

            gaugeWind = singleWind.getInstance();
            gaugeDir = singleDir.getInstance();

            gaugeRain = singleRain.getInstance();
            gaugeRRate = singleRRate.getInstance();


            // remove the UV gauge?
            if (!config.showUvGauge) {
                $('#canvas_uv').parent().remove();
            } else {
                gaugeUV = singleUV.getInstance();
            }

            // remove the Solar gauge?
            if (!config.showSolarGauge) {
                $('#canvas_solar').parent().remove();
            } else {
                gaugeSolar = singleSolar.getInstance();
            }

            // remove the Wind Rose?
            if (!config.showRoseGauge) {
                $('#canvas_rose').parent().remove();
            } else {
                gaugeRose = singleRose.getInstance();
                if (gaugeRose) {
                    gaugeRose.setTitle(strings.windrose);
                    gaugeRose.setCompassString(strings.compass);
                }
            }

            // remove the cloud base gauge?
            if (!config.showCloudGauge) {
                $('#canvas_cloud').parent().remove();
                // and remove cloudbase unit selection options
                $('#cloud').parent().remove();
            } else {
                gaugeCloud = singleCloudBase.getInstance();
            }

            // Set the language
            changeLang(strings, false);

            if (!dashboard) {
                // Go do get the data!
                getRealtime();

                // start a timer to update the status time
                _tickTockInterval = setInterval(
                    function () {
                        $.publish('gauges.clockTick', null);
                    },
                    1000);

                // start a timer to stop the page updates after the timeout period
                if (config.pageUpdateLimit > 0 && _pageUpdateParam !== config.pageUpdatePswd) {
                    setTimeout(pageTimeout, config.pageUpdateLimit * 60 * 1000);
                }
            }
        },

        //
        // singleXXXX functions define a singleton for each of the gauges
        //

        //
        // Singleton for the LED Indicator
        //
        singleLed = (function () {
            var instance;   // Stores a reference to the Singleton
            var led;        // Stores a reference to the SS LED

            function init() {

                // create led indicator
                if ($('#canvas_led').length) {
                    led = new steelseries.Led(
                        'canvas_led', {
                        ledColor : steelseries.LedColor.GREEN_LED,
                        size : $('#canvas_led').width()
                    });

                    setTitle(strings.led_title);
                }

                function setTitle(newTitle) {
                    $('#canvas_led').attr('title',newTitle);
                }

                function setLedColor(newColour) {
                    if (led) {
                        led.setLedColor(newColour);
                    }
                }

                function setLedOnOff(onState) {
                    if (led) {
                        led.setLedOnOff(onState);
                    }
                }

                function blink(blinkState) {
                    if (led) {
                        led.blink(blinkState);
                    }
                }

                return {
                    setTitle: setTitle,
                    setLedColor: setLedColor,
                    setLedOnOff: setLedOnOff,
                    blink: blink
                };
            }


            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Status Scroller
        //
        singleStatus = (function () {
            var instance;   // Stores a reference to the Singleton
            var scroller;   // Stores a reference to the SS scrolling display

            function init() {
                // create forecast display
                if ($('#canvas_status').length) {
                    scroller = new steelseries.DisplaySingle(
                        'canvas_status', {
                            width             : $('#canvas_status').width(),
                            height            : $('#canvas_status').height(),
                            lcdColor          : gaugeGlobals.lcdColour,
                            unitStringVisible : false,
                            value             : strings.statusStr,
                            digitalFont       : config.digitalForecast,
                            valuesNumeric     : false,
                            autoScroll        : true,
                            alwaysScroll      : false
                    });

                }

                function setValue(newTxt) {
                    if (scroller) {
                        scroller.setValue(newTxt);
                    }
                }

                return {setText: setValue};
            }

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Status Timer
        //
        singleTimer = (function () {
            var instance,   // Stores a reference to the Singleton
                lcd,        // Stores a reference to the SS LED
                count = 1;

            function init() {

                function tick() {
                    if (lcd) {
                        lcd.setValue(count);
                        count += config.longPoll ? 1 : -1;
                    }
                }

                function reset(val) {
                    count = val;
                }

                function setValue(newVal) {
                    if (lcd) {
                        lcd.setValue(newVal);
                    }
                }

                // create timer display
                if ($('#canvas_timer').length) {
                    lcd = new steelseries.DisplaySingle(
                        'canvas_timer', {
                            width             : $('#canvas_timer').width(),
                            height            : $('#canvas_timer').height(),
                            lcdColor          : gaugeGlobals.lcdColour,
                            lcdDecimals       : 0,
                            unitString        : strings.timer,
                            unitStringVisible : true,
                            digitalFont       : config.digitalFont,
                            value             : count
                    });
                    // subcribe to data updates
                    $.subscribe('gauges.clockTick', tick);
                }

                return {
                    reset: reset,
                    setValue: setValue
                };
            }

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Temperature Gauge
        //
        singleTemp = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define temperature gauge start values
                cache.sections = createTempSections(true);
                cache.areas = [];
                cache.minValue = gaugeGlobals.tempScaleDefMinC;
                cache.maxValue = gaugeGlobals.tempScaleDefMaxC;
                cache.title = strings.temp_title_out;
                cache.value = gaugeGlobals.tempScaleDefMinC + 0.0001;
                cache.maxMinVisible = false;
                cache.selected = 'out';

                // create temperature radial gauge
                if ($('#canvas_temp').length) {
                    params.size = Math.ceil($('#canvas_temp').width() * config.gaugeScaling);
                    params.section = cache.sections;
                    params.area = cache.areas;
                    params.minValue = cache.minValue;
                    params.maxValue = cache.maxValue;
                    params.thresholdVisible = false;
                    params.minMeasuredValueVisible = cache.maxMinVisible;
                    params.maxMeasuredValueVisible = cache.maxMinVisible;
                    params.titleString = cache.title;
                    params.unitString = data.tempunit;
                    params.trendVisible = gaugeGlobals.tempTrendVisible;
                    //params.customLayer = _imgBackground;      // uncomment to add a background image - See Logo Images above

                    ssGauge = new steelseries.Radial('canvas_temp', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_temp').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_temp').css(gaugeShadow(params.size));
                    }

                    // remove indoor temperature/humidity options?
                    if (!config.showIndoorTempHum) {
                        $('#rad_temp1').remove();
                        $('#lab_temp1').remove();
                        $('#rad_temp2').remove();
                        $('#lab_temp2').remove();
                        $('#rad_hum1').remove();
                        $('#lab_hum1').remove();
                        $('#rad_hum2').remove();
                        $('#lab_hum2').remove();
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }


                function update() {
                    var sel = cache.selected;


                    // Argument length === 1 when called from radio input
                    // Argument length === 2 when called from event handler
                    if (arguments.length === 1) {
                        sel = arguments[0].value;
                    }

                    // if rad isn't specified, just use existing value
                    var t1, scaleStep, tip;

                    if (sel === 'out') {
                        cache.minValue = data.tempunit[1] === 'C' ? gaugeGlobals.tempScaleDefMinC : gaugeGlobals.tempScaleDefMinF;
                        cache.maxValue = data.tempunit[1] === 'C' ? gaugeGlobals.tempScaleDefMaxC : gaugeGlobals.tempScaleDefMaxF;
                        cache.low = extractDecimal(data.tempTL);
                        cache.lowScale = getMinTemp();
                        cache.high = extractDecimal(data.tempTH);
                        cache.highScale = getMaxTemp();
                        cache.value = extractDecimal(data.temp);
                        cache.title = strings.temp_title_out;
                        cache.loc = strings.temp_out_info;
                        cache.trendVal =  extractDecimal(data.temptrend);
                        cache.popupImg = 0;
                        if (gaugeGlobals.tempTrendVisible) {
                            t1 = tempTrend(+cache.trendVal, data.tempunit, false);
                            if (t1 === -9999) {
                                // trend value isn't currently available
                                cache.trend = steelseries.TrendState.OFF;
                            } else if (t1 > 0) {
                                cache.trend = steelseries.TrendState.UP;
                            } else if (t1 < 0) {
                                cache.trend = steelseries.TrendState.DOWN;
                            } else {
                                cache.trend = steelseries.TrendState.STEADY;
                            }
                        }
                    } else {
                        cache.low = extractDecimal(data.intemp);
                        cache.lowScale = cache.low;
                        cache.high = cache.low;
                        cache.highScale = cache.low;
                        cache.value = cache.low;
                        cache.title = strings.temp_title_in;
                        cache.loc = strings.temp_in_info;
                        cache.maxMinVisible = false;
                        cache.popupImg = 1;
                        if (gaugeGlobals.tempTrendVisible) {
                            cache.trend = steelseries.TrendState.OFF;
                        }
                    }

                    // has the gauge type changed?
                    if (cache.selected !== sel) {
                        cache.selected = sel;
                        //Change gauge title
                        ssGauge.setTitleString(cache.title);
                        ssGauge.setMaxMeasuredValueVisible(cache.maxMinVisible);
                        ssGauge.setMinMeasuredValueVisible(cache.maxMinVisible);
                        if (config.showPopupGraphs && config.tipImgs[0][cache.popupImg] !== null) {
                            var cacheDefeat = '?' + $('#imgtip0_img').attr('src').split('?')[1];
                            $('#imgtip0_img').attr('src', config.imgPathURL + config.tipImgs[0][cache.popupImg] + cacheDefeat);
                        }
                    }

                    //auto scale the ranges
                    scaleStep = data.tempunit[1] === 'C' ? 10 : 20;
                    while (cache.lowScale < cache.minValue) {
                        cache.minValue -= scaleStep;
                        if (cache.highScale <= cache.maxValue - scaleStep) {
                            cache.maxValue -= scaleStep;
                        }
                    }
                    while (cache.highScale > cache.maxValue) {
                        cache.maxValue += scaleStep;
                        if (cache.minValue >= cache.minValue + scaleStep) {
                            cache.minValue += scaleStep;
                        }
                    }

                    if (cache.minValue !== ssGauge.getMinValue() || cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setMinValue(cache.minValue);
                        ssGauge.setMaxValue(cache.maxValue);
                        ssGauge.setValue(cache.minValue);
                    }
                    if (cache.selected === 'out') {
                        cache.areas = [steelseries.Section(+cache.low, +cache.high, gaugeGlobals.minMaxArea)];
                    } else {
                        cache.areas = [];
                    }

                    if (gaugeGlobals.tempTrendVisible) {
                        ssGauge.setTrend(cache.trend);
                    }
                    ssGauge.setArea(cache.areas);
                    ssGauge.setValueAnimated(+cache.value);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        if (cache.selected === 'out') {
                            tip = cache.loc + ' - ' + strings.lowestF_info + ': ' + cache.low + data.tempunit + ' ' + strings.at + ' ' + data.TtempTL +
                                 ' | ' +
                                 strings.highestF_info + ': ' + cache.high + data.tempunit + ' ' + strings.at + ' ' + data.TtempTH;
                            if (cache.trendVal !== -9999) {
                                tip += '<br>' +
                                    strings.temp_trend_info + ': ' + tempTrend(cache.trendVal, data.tempunit, true) + ' ' + cache.trendVal + data.tempunit + '/h';
                            }
                        } else {
                            tip = cache.loc + ': ' + data.intemp + data.tempunit;
                        }
                        $('#imgtip0_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[0][cache.popupImg] !== null) {
                        $('#imgtip0_img').attr('src', config.imgPathURL + config.tipImgs[0][cache.popupImg] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(), // End singleTemp()

        //
        // Singleton for the Dewpoint Gauge
        //
        singleDew = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);
                var tmp;

                //define dew point gauge start values
                cache.sections = createTempSections(true);
                cache.areas = [];
                cache.minValue = gaugeGlobals.tempScaleDefMinC;
                cache.maxValue = gaugeGlobals.tempScaleDefMaxC;
                cache.value = gaugeGlobals.tempScaleDefMinC + 0.0001;
                // Has the end user selected a preferred 'scale' before
                tmp = getCookie('dewGauge');
                cache.selected = tmp ? tmp :  config.dewDisplayType;
                setRadioCheck('rad_dew', cache.selected);
                switch (cache.selected) {
                case 'dew':
                    cache.title = strings.dew_title;
                    cache.popupImg = 0;
                    break;
                case 'app':
                    cache.title = strings.apptemp_title;
                    cache.popupImg = 1;
                    break;
                case 'wnd':
                    cache.title = strings.chill_title;
                    cache.popupImg = 2;
                    break;
                case 'hea':
                    cache.title = strings.heat_title;
                    cache.popupImg = 3;
                    break;
                case 'hum':
                    cache.title = strings.humdx_title;
                    cache.popupImg = 4;
                }
                cache.minMeasuredVisible = false;
                cache.maxMeasuredVisible = false;

                // create dew point radial gauge
                if ($('#canvas_dew').length) {
                    params.size = Math.ceil($('#canvas_dew').width() * config.gaugeScaling);
                    params.section = cache.sections;
                    params.area = cache.areas;
                    params.minValue = cache.minValue;
                    params.maxValue = cache.maxValue;
                    params.thresholdVisible = false;
                    params.titleString = cache.title;
                    params.unitString = data.tempunit;

                    ssGauge = new steelseries.Radial('canvas_dew', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_dew').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_dew').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    // if rad isn't specified, just use existing value
                    var sel = cache.selected;

                    // Argument length === 2 when called from event handler
                    if (arguments.length === 1) {
                        sel = arguments[0].value;
                        // save the choice in a cookie
                        setCookie('dewGauge', sel);
                    }

                    var tip, scaleStep;

                    cache.lowScale = getMinTemp();
                    cache.highScale = getMaxTemp();

                    switch (sel) {
                    case 'dew': // dew point
                        cache.low = extractDecimal(data.dewpointTL);
                        cache.high = extractDecimal(data.dewpointTH);
                        cache.value = extractDecimal(data.dew);
                        cache.areas = [steelseries.Section(+cache.low, +cache.high, gaugeGlobals.minMaxArea)];
                        cache.title = strings.dew_title;
                        cache.minMeasuredVisible = false;
                        cache.maxMeasuredVisible = false;
                        cache.popupImg = 0;
                        tip = strings.dew_info + ':' +
                             '<br>' +
                             '- ' + strings.lowest_info + ': ' + cache.low + data.tempunit + ' ' + strings.at + ' ' + data.TdewpointTL +
                             ' | ' + strings.highest_info + ': ' + cache.high + data.tempunit + ' ' + strings.at + ' ' + data.TdewpointTH;
                        break;
                    case 'app': // apparent temperature
                        cache.low = extractDecimal(data.apptempTL);
                        cache.high = extractDecimal(data.apptempTH);
                        cache.value = extractDecimal(data.apptemp);
                        cache.areas = [steelseries.Section(+cache.low, +cache.high, gaugeGlobals.minMaxArea)];
                        cache.title = strings.apptemp_title;
                        cache.minMeasuredVisible = false;
                        cache.maxMeasuredVisible = false;
                        cache.popupImg = 1;
                        tip = tip = strings.apptemp_info + ':' +
                             '<br>' +
                             '- ' + strings.lowestF_info + ': ' + cache.low + data.tempunit + ' ' + strings.at + ' ' + data.TapptempTL +
                             ' | ' + strings.highestF_info + ': ' + cache.high + data.tempunit + ' ' + strings.at + ' ' + data.TapptempTH;
                        break;
                    case 'wnd': // wind chill
                        cache.low = extractDecimal(data.wchillTL);
                        cache.high = extractDecimal(data.wchill);
                        cache.value = extractDecimal(data.wchill);
                        cache.areas = [];
                        cache.title = strings.chill_title;
                        cache.minMeasuredVisible = true;
                        cache.maxMeasuredVisible = false;
                        cache.popupImg = 2;
                        tip = strings.chill_info + ':' +
                            '<br>' +
                            '- ' + strings.lowest_info + ': ' + cache.low + data.tempunit + ' ' + strings.at + ' ' + data.TwchillTL;
                        break;
                    case 'hea': // heat index
                        cache.low = extractDecimal(data.heatindex);
                        cache.high = extractDecimal(data.heatindexTH);
                        cache.value = extractDecimal(data.heatindex);
                        cache.areas = [];
                        cache.title = strings.heat_title;
                        cache.minMeasuredVisible = false;
                        cache.maxMeasuredVisible = true;
                        cache.popupImg = 3;
                        tip = strings.heat_info + ':' +
                            '<br>' +
                            '- ' + strings.highest_info + ': ' + cache.high + data.tempunit + ' ' + strings.at + ' ' + data.TheatindexTH;
                        break;
                    case 'hum': // humidex
                        cache.low = extractDecimal(data.humidex);
                        cache.high = extractDecimal(data.humidex);
                        cache.value = extractDecimal(data.humidex);
                        cache.areas = [];
                        cache.title = strings.humdx_title;
                        cache.minMeasuredVisible = false;
                        cache.maxMeasuredVisible = false;
                        cache.popupImg = 4;
                        tip = strings.humdx_info + ': ' + cache.value + data.tempunit;
                        break;
                    }

                    if (cache.selected !== sel) {
                        cache.selected = sel;
                        // change gauge title
                        ssGauge.setTitleString(cache.title);
                        // and graph image
                        if (config.showPopupGraphs && config.tipImgs[1][cache.popupImg] !== null) {
                            var cacheDefeat = '?' + $('#imgtip1_img').attr('src').split('?')[1];
                            $('#imgtip1_img').attr('src', config.imgPathURL + config.tipImgs[1][cache.popupImg] + cacheDefeat);
                        }
                    }

                    //auto scale the ranges
                    scaleStep = data.tempunit[1] === 'C' ? 10 : 20;
                    while (cache.lowScale < cache.minValue) {
                        cache.minValue -= scaleStep;
                        if (cache.highScale <= cache.maxValue - scaleStep) {
                            cache.maxValue -= scaleStep;
                        }
                    }
                    while (cache.highScale > cache.maxValue) {
                        cache.maxValue += scaleStep;
                        if (cache.minValue >= cache.minValue + scaleStep) {
                            cache.minValue += scaleStep;
                        }
                    }

                    if (cache.minValue !== ssGauge.getMinValue() || cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setMinValue(cache.minValue);
                        ssGauge.setMaxValue(cache.maxValue);
                        ssGauge.setValue(cache.minValue);
                    }
                    ssGauge.setMinMeasuredValueVisible(cache.minMeasuredVisible);
                    ssGauge.setMaxMeasuredValueVisible(cache.maxMeasuredVisible);
                    ssGauge.setMinMeasuredValue(+cache.low);
                    ssGauge.setMaxMeasuredValue(+cache.high);
                    ssGauge.setArea(cache.areas);
                    ssGauge.setValueAnimated(+cache.value);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        $('#imgtip1_txt').html(tip);
                    }
                }

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[1][cache.popupImg] !== null) {
                        $('#imgtip1_img').attr('src', config.imgPathURL + config.tipImgs[1][cache.popupImg] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(), // End of singleDew()

        //
        // Singleton for the Rainfall Gauge
        //
        singleRain = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define rain gauge start values
                cache.maxValue = gaugeGlobals.rainScaleDefMaxmm;
                cache.value = 0.0001;
                cache.title = strings.rain_title;
                cache.lcdDecimals = 1;
                cache.scaleDecimals = 1;
                cache.labelNumberFormat = gaugeGlobals.labelFormat;
                cache.sections = (gaugeGlobals.rainUseSectionColours ? createRainfallSections(true) : []);
                cache.valGrad = (gaugeGlobals.rainUseGradientColours ? createRainfallGradient(true) : null);

                // create rain radial bargraph gauge
                if ($('#canvas_rain').length) {
                    params.size = Math.ceil($('#canvas_rain').width() * config.gaugeScaling);
                    params.maxValue = cache.maxValue;
                    params.thresholdVisible = false;
                    params.titleString = cache.title;
                    params.unitString = data.rainunit;
                    params.valueColor = steelseries.ColorDef.BLUE;
                    params.valueGradient = cache.valGrad;
                    params.useValueGradient = gaugeGlobals.rainUseGradientColours;
                    params.useSectionColors = gaugeGlobals.rainUseSectionColour;
                    params.useSectionColors = gaugeGlobals.rainUseSectionColours;
                    params.labelNumberFormat = cache.labelNumberFormat;
                    params.fractionalScaleDecimals = cache.scaleDecimals;

                    ssGauge = new steelseries.RadialBargraph('canvas_rain', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_rain').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_rain').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    cache.value = extractDecimal(data.rfall);

                    if (data.rainunit === 'mm') { // 10, 20, 30...
                        cache.maxValue = Math.max(Math.ceil(cache.value / 10) * 10, gaugeGlobals.rainScaleDefMaxmm);
                    } else {
                        // inches 0.5, 1.0, 2.0, 3.0 ... 10.0, 12.0, 14.0
                        if (cache.value < 6) {
                            cache.maxValue = Math.max(Math.ceil(cache.value), gaugeGlobals.rainRateScaleDefMaxIn);
                        } else {
                            cache.maxValue = Math.max(Math.ceil(cache.value / 2) * 2, gaugeGlobals.rainRateScaleDefMaxIn);
                        }
                        cache.scaleDecimals = cache.maxValue < 1 ? 2 : 1;
                    }

                    if (cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setValue(0);
                        ssGauge.setFractionalScaleDecimals(cache.scaleDecimals);
                        ssGauge.setMaxValue(cache.maxValue);
                    }

                    ssGauge.setValueAnimated(cache.value);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        $('#imgtip2_txt').html(strings.LastRain_info + ': ' + data.LastRained);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[2] !== null) {
                        $('#imgtip2_img').attr('src', config.imgPathURL + config.tipImgs[2] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Rainfall Rate Gauge
        //
        singleRRate = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define rain rate gauge start values
                cache.maxMeasured = 0;
                cache.maxValue = gaugeGlobals.rainRateScaleDefMaxmm;
                cache.value = 0.0001;
                cache.title = strings.rrate_title;
                cache.lcdDecimals = 1;
                cache.scaleDecimals = 0;
                cache.labelNumberFormat = gaugeGlobals.labelFormat;
                cache.sections = createRainRateSections(true);

                // create rain rate radial gauge
                if ($('#canvas_rrate').length) {
                    params.size = Math.ceil($('#canvas_rrate').width() * config.gaugeScaling);
                    params.section = cache.sections;
                    params.maxValue = cache.maxValue;
                    params.thresholdVisible = false;
                    params.maxMeasuredValueVisible = true;
                    params.titleString = cache.title;
                    params.unitString = data.rainunit + '/h';
                    params.lcdDecimals = cache.lcdDecimals;
                    params.labelNumberFormat = cache.labelNumberFormat;
                    params.fractionalScaleDecimals = cache.scaleDecimals;
                    params.niceScale = false;

                    ssGauge = new steelseries.Radial('canvas_rrate', params);
                    ssGauge.setMaxMeasuredValue(cache.maxMeasured);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_rrate').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_rrate').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    var tip;

                    cache.value = extractDecimal(data.rrate);
                    cache.maxMeasured = extractDecimal(data.rrateTM);
                    cache.overallMax = Math.max(cache.maxMeasured, cache.value);  // workaround for VWS bug, not supplying correct max value today

                    if (data.rainunit === 'mm') { // 10, 20, 30...
                        cache.maxValue = Math.max(Math.ceil(cache.overallMax / 10) * 10, 10);
                    } else {
                        // inches 0.5, 1.0, 2.0, 3.0 ...
                        if (cache.overallMax <= 0.5) {
                            cache.maxValue = 0.5;
                        } else {
                            cache.maxValue = Math.ceil(cache.overallMax);
                        }
                        cache.scaleDecimals = cache.maxValue < 1 ? 2 : (cache.maxValue < 7 ? 1 : 0);
                    }

                    if (cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setValue(0);
                        ssGauge.setFractionalScaleDecimals(cache.scaleDecimals);
                        ssGauge.setMaxValue(cache.maxValue);
                    }

                    ssGauge.setValueAnimated(cache.value);
                    ssGauge.setMaxMeasuredValue(cache.maxMeasured);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        tip = strings.rrate_info + ':<br>' +
                            '- ' + strings.maximum_info + ': ' + data.rrateTM + ' ' + data.rainunit + '/h ' + strings.at + ' ' + data.TrrateTM +
                            ' | ' + strings.max_hour_info + ': ' + extractDecimal(data.hourlyrainTH) + ' ' + data.rainunit + ' ' + strings.at + ' ' + data.ThourlyrainTH;
                        $('#imgtip3_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[3] !== null) {
                        $('#imgtip3_img').attr('src', config.imgPathURL + config.tipImgs[3] + cacheDefeat);
                    }
                }
                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };

            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Humidity Gauge
        //
        singleHum = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define humidity gauge start values
                cache.areas = [];
                cache.value = 0.0001;
                cache.title = strings.hum_title_out;
                cache.selected = 'out';

                // create humidity radial gauge
                if ($('#canvas_hum').length) {
                    params.size = Math.ceil($('#canvas_hum').width() * config.gaugeScaling);
                    params.section = [steelseries.Section(0, 20, 'rgba(255,255,0,0.3)'),
                                      steelseries.Section(20, 80, 'rgba(0,255,0,0.3)'),
                                      steelseries.Section(80, 100, 'rgba(255,0,0,0.3)')];
                    params.area = cache.areas;
                    params.maxValue = 100;
                    params.thresholdVisible = false;
                    params.titleString = cache.title;
                    params.unitString = 'RH%';

                    ssGauge = new steelseries.Radial('canvas_hum', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_hum').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_hum').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    var radio;
                    // Argument length === 2 when called from event handler
                    if (arguments.length === 1) {
                        radio = arguments[0];
                    }

                    //if rad isn't specified, just use existing value
                    var sel = (radio === undefined ? cache.selected : radio.value),
                        tip;

                    if (sel === 'out') {
                        cache.value = extractDecimal(data.hum);
                        cache.areas = [steelseries.Section(+extractDecimal(data.humTL), +extractDecimal(data.humTH), gaugeGlobals.minMaxArea)];
                        cache.title = strings.hum_title_out;
                        cache.popupImg = 0;
                    } else {
                        cache.value = extractDecimal(data.inhum);
                        if (data.inhumTL && data.inhumTH) {
                            cache.areas = [steelseries.Section(+extractDecimal(data.inhumTL), +extractDecimal(data.inhumTH), gaugeGlobals.minMaxArea)];
                        } else {
                            cache.areas = [];
                        }
                        cache.title = strings.hum_title_in;
                        cache.popupImg = 1;
                    }

                    if (cache.selected !== sel) {
                        cache.selected = sel;
                        //Change gauge title
                        ssGauge.setTitleString(cache.title);
                        if (config.showPopupGraphs && config.tipImgs[4][cache.popupImg] !== null) {
                            var cacheDefeat = '?' + $('#imgtip4_img').attr('src').split('?')[1];
                            $('#imgtip4_img').attr('src', config.imgPathURL + config.tipImgs[4][cache.popupImg] + cacheDefeat);
                        }
                    }

                    ssGauge.setArea(cache.areas);
                    ssGauge.setValueAnimated(cache.value);

                    if (ddimgtooltip.showTips) {
                        //update tooltip
                        if (cache.selected === 'out') {
                            tip = strings.hum_out_info + ':' +
                                '<br>' +
                                '- ' + strings.minimum_info + ': ' + extractDecimal(data.humTL) + '% ' + strings.at + ' ' + data.ThumTL +
                                ' | ' + strings.maximum_info + ': ' + extractDecimal(data.humTH) + '% ' + strings.at + ' ' + data.ThumTH;
                        } else if (data.inhumTL && data.inhumTH) {
                            // we have indoor high/low data
                            tip = strings.hum_in_info + ':' +
                                 '<br>' +
                                '- ' + strings.minimum_info + ': ' + extractDecimal(data.inhumTL) + '% ' + strings.at + ' ' + data.TinhumTL +
                                ' | ' + strings.maximum_info + ': ' + extractDecimal(data.inhumTH) + '% ' + strings.at + ' ' + data.TinhumTH;
                        } else {
                            // no indoor high/low data
                            tip = strings.hum_in_info + ': ' + extractDecimal(data.inhum) + '%';
                        }
                        $('#imgtip4_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[4][cache.popupImg] !== null) {
                        $('#imgtip4_img').attr('src', config.imgPathURL + config.tipImgs[4][cache.popupImg] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Barometer Gauge
        //
        singleBaro = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define pressure/barometer gauge start values
                cache.sections = [];
                cache.areas = [];
                cache.minValue = gaugeGlobals.baroScaleDefMinhPa;
                cache.maxValue = gaugeGlobals.baroScaleDefMaxhPa;
                cache.value = cache.minValue + 0.0001;
                cache.title = strings.baro_title;
                cache.lcdDecimals = 1;
                cache.scaleDecimals = 0;
                cache.labelNumberFormat = gaugeGlobals.labelFormat;

                // create pressure/barometric radial gauge
                if ($('#canvas_baro').length) {
                    params.size = Math.ceil($('#canvas_baro').width() * config.gaugeScaling);
                    params.section = cache.sections;
                    params.area = cache.areas;
                    params.minValue = cache.minValue;
                    params.maxValue = cache.maxValue;
                    params.niceScale = false;
                    params.thresholdVisible = false;
                    params.titleString = cache.title;
                    params.unitString = data.pressunit;
                    params.lcdDecimals = cache.lcdDecimals;
                    params.trendVisible = gaugeGlobals.pressureTrendVisible;
                    params.labelNumberFormat = cache.labelNumberFormat;
                    params.fractionalScaleDecimals = cache.scaleDecimals;

                    ssGauge = new steelseries.Radial('canvas_baro', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_baro').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_baro').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    var tip, t1;

                    cache.recLow  = +extractDecimal(data.pressL);
                    cache.recHigh = +extractDecimal(data.pressH);
                    cache.todayLow  = +extractDecimal(data.pressTL);
                    cache.todayHigh = +extractDecimal(data.pressTH);
                    cache.value = +extractDecimal(data.press);
                    // Convert the WD change over 3 hours to an hourly rate
                    cache.trendVal =  +extractDecimal(data.presstrendval) / (config.weatherProgram === 2 ? 3 : 1);

                    if (data.pressunit === 'hPa' || data.pressunit === 'mb') {
                        //  min range 990-1030 - steps of 10 hPa
                        cache.minValue = Math.min(Math.floor((cache.recLow - 2) / 10) * 10, 990);
                        cache.maxValue = Math.max(Math.ceil((cache.recHigh + 2) / 10) * 10, 1030);
                        cache.trendValRnd = cache.trendVal.toFixed(1);    // round to 0.1
                    } else if (data.pressunit === 'kPa') {
                        //  min range 99-105 - steps of 1 kPa
                        cache.minValue = Math.min(Math.floor(cache.recLow - 0.2), 99);
                        cache.maxValue = Math.max(Math.ceil(cache.recHigh + 0.2), 105);
                        cache.trendValRnd = cache.trendVal.toFixed(2);    // round to 0.01
                    } else {
                        // inHg: min range 29.5-30.5 - steps of 0.5 inHg
                        cache.minValue = Math.min(Math.floor((cache.recLow - 0.1) * 2) / 2, 29.5);
                        cache.maxValue = Math.max(Math.ceil((cache.recHigh + 0.1) * 2) / 2, 30.5);
                        cache.trendValRnd = cache.trendVal.toFixed(3);    // round to 0.001
                    }
                    if (cache.minValue !== ssGauge.getMinValue() || cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setMinValue(cache.minValue);
                        ssGauge.setMaxValue(cache.maxValue);
                        ssGauge.setValue(cache.minValue);
                    }
                    if (cache.recHigh === cache.todayHigh && cache.recLow === cache.todayLow) {
                        // VWS does not provide record hi/lo values
                        cache.sections = [];
                        cache.areas = [steelseries.Section(cache.todayLow, cache.todayHigh, gaugeGlobals.minMaxArea)];
                    } else {
                        cache.sections = [
                            steelseries.Section(cache.minValue, cache.recLow, 'rgba(255,0,0,0.5)'),
                            steelseries.Section(cache.recHigh, cache.maxValue, 'rgba(255,0,0,0.5)')
                        ];
                        cache.areas = [
                            steelseries.Section(cache.minValue, cache.recLow, 'rgba(255,0,0,0.5)'),
                            steelseries.Section(cache.recHigh, cache.maxValue, 'rgba(255,0,0,0.5)'),
                            steelseries.Section(cache.todayLow, cache.todayHigh, gaugeGlobals.minMaxArea)
                        ];
                    }

                    if (gaugeGlobals.pressureTrendVisible) {
                        // Use the baroTrend rather than simple arithmetic test - steady is more/less than zero!
                        t1 = baroTrend(cache.trendVal, data.pressunit, false);
                        if (t1 === -9999) {
                            // trend value isn't currently available
                            cache.trend = steelseries.TrendState.OFF;
                        } else if (t1 > 0) {
                            cache.trend = steelseries.TrendState.UP;
                        } else if (t1 < 0) {
                            cache.trend = steelseries.TrendState.DOWN;
                        } else {
                            cache.trend = steelseries.TrendState.STEADY;
                        }
                        ssGauge.setTrend(cache.trend);
                    }

                    ssGauge.setArea(cache.areas);
                    ssGauge.setSection(cache.sections);
                    ssGauge.setValueAnimated(cache.value);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        tip = strings.baro_info + ':' +
                            '<br>' +
                            '- ' + strings.minimum_info + ': ' + cache.todayLow + ' ' + data.pressunit + ' ' + strings.at + ' ' + data.TpressTL +
                            ' | ' + strings.maximum_info + ': ' + cache.todayHigh + ' ' + data.pressunit + ' ' + strings.at + ' ' + data.TpressTH;
                        if (cache.trendVal !== -9999) {
                            tip +=  '<br>' +
                                '- ' + strings.baro_trend_info + ': ' + baroTrend(cache.trendVal, data.pressunit, true) + ' ' +
                                (cache.trendValRnd > 0 ? '+' : '') + cache.trendValRnd + ' ' + data.pressunit + '/h';
                        }
                        $('#imgtip5_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[5] !== null) {
                        $('#imgtip5_img').attr('src', config.imgPathURL + config.tipImgs[5] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Wind Speed Gauge
        //
        singleWind = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define wind gauge start values
                cache.maxValue = gaugeGlobals.windScaleDefMaxKph;
                cache.areas = [];
                cache.maxMeasured = 0;
                cache.value = 0.0001;
                cache.title = strings.wind_title;

                // create wind speed radial gauge
                if ($('#canvas_wind').length) {
                    params.size = Math.ceil($('#canvas_wind').width() * config.gaugeScaling);
                    params.area = cache.areas;
                    params.maxValue = cache.maxValue;
                    params.niceScale = false;
                    params.thresholdVisible = false;
                    params.maxMeasuredValueVisible = true;
                    params.titleString = cache.title;
                    params.unitString = data.windunit;

                    ssGauge = new steelseries.Radial('canvas_wind', params);
                    ssGauge.setMaxMeasuredValue(cache.maxMeasured);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_wind').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_wind').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }


                function update() {
                    var tip;

                    cache.value = extractDecimal(data.wlatest);
                    cache.average = extractDecimal(data.wspeed);
                    cache.gust = extractDecimal(data.wgust);
                    cache.maxGustToday = extractDecimal(data.wgustTM);
                    cache.maxAvgToday = extractDecimal(data.windTM);

                    switch (data.windunit) {
                    case 'mph':
                    case 'kts':
                        cache.maxValue = Math.max(Math.ceil(cache.maxGustToday / 10) * 10, gaugeGlobals.windScaleDefMaxMph);
                        break;
                    case 'm/s':
                        cache.maxValue = Math.max(Math.ceil(cache.maxGustToday / 5) * 5, gaugeGlobals.windScaleDefMaxMs);
                        break;
                    default:
                        cache.maxValue = Math.max(Math.ceil(cache.maxGustToday / 20) * 20, gaugeGlobals.windScaleDefMaxKmh);
                    }
                    cache.areas = [
                        steelseries.Section(0, +cache.average, gaugeGlobals.windAvgArea),
                        steelseries.Section(+cache.average, +cache.gust, gaugeGlobals.minMaxArea)
                    ];
                    if (cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setMaxValue(cache.maxValue);
                    }

                    ssGauge.setArea(cache.areas);
                    ssGauge.setMaxMeasuredValue(cache.maxGustToday);
                    ssGauge.setValueAnimated(cache.value);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        tip = strings.tenminavgwind_info + ': ' + cache.average + ' ' + data.windunit + ' | ' +
                              strings.maxavgwind_info + ': ' + cache.maxAvgToday + ' ' + data.windunit + '<br>' +
                              strings.tenmingust_info + ': ' + cache.gust + ' ' + data.windunit + ' | ' +
                              strings.maxgust_info + ': ' + cache.maxGustToday + ' ' + data.windunit + ' ' +
                              strings.at + ' ' + data.TwgustTM + ' ' + strings.bearing_info + ': ' + data.bearingTM +
                              (isNaN(parseFloat(data.bearingTM)) ? '' : '° (' + getord(+data.bearingTM) + ')');
                        $('#imgtip6_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[6] !== null) {
                        $('#imgtip6_img').attr('src', config.imgPathURL + config.tipImgs[6] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(), // End of singleWind()

        //
        // Singleton for the Wind Direction Gauge
        //
        singleDir = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {}; // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define wind direction gauge start values
                cache.valueLatest = 0;
                cache.valueAverage = 0;
                cache.titles = [strings.latest_web, strings.tenminavg_web];

                // create wind direction/compass radial gauge
                if ($('#canvas_dir').length) {
                    params.size = Math.ceil($('#canvas_dir').width() * config.gaugeScaling);
                    params.pointerTypeLatest = gaugeGlobals.pointer; // default TYPE8,
                    params.pointerTypeAverage = gaugeGlobals.dirAvgPointer; // default TYPE8
                    params.pointerColorAverage = gaugeGlobals.dirAvgPointerColour;
                    params.degreeScale = true;             // Show degree scale rather than ordinal directions
                    params.pointSymbols = strings.compass;
                    params.roseVisible = false;
                    params.lcdTitleStrings = cache.titles;
                    params.useColorLabels = false;

                    ssGauge = new steelseries.WindDirection('canvas_dir', params);
                    ssGauge.setValueAverage(+cache.valueAverage);
                    ssGauge.setValueLatest(+cache.valueLatest);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_dir').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_dir').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }


                function update() {
                    var windSpd, windGst, range, tip, i,
                        rosePoints = 0, roseMax = 0,
                        roseSectionAngle = 0, roseAreas = [];

                    cache.valueLatest = extractInteger(data.bearing);
                    cache.valueAverage = extractInteger(data.avgbearing);
                    cache.bearingFrom = extractInteger(data.BearingRangeFrom10);
                    cache.bearingTo   = extractInteger(data.BearingRangeTo10);

                    ssGauge.setValueAnimatedAverage(+cache.valueAverage);
                    if (cache.valueAverage === 0) {
                        cache.valueLatest = 0;
                    }
                    ssGauge.setValueAnimatedLatest(+cache.valueLatest);

                    if (config.showWindVariation) {
                        windSpd = +extractDecimal(data.wspeed);
                        windGst = +extractDecimal(data.wgust);
                        switch (data.windunit.toLowerCase()) {
                        case 'mph':
                            cache.avgKnots = 0.868976242 * windSpd;
                            cache.gstKnots = 0.868976242 * windGst;
                            break;
                        case 'kts':
                            cache.avgKnots = windSpd;
                            cache.gstKnots = windGst;
                            break;
                        case 'm/s':
                            cache.avgKnots = 1.94384449 * windSpd;
                            cache.gstKnots = 1.94384449 * windGst;
                            break;
                        case 'km/h':
                            cache.avgKnots = 0.539956803 * windSpd;
                            cache.gstKnots = 0.539956803 * windGst;
                            break;
                        }
                        cache.avgKnots = Math.round(cache.avgKnots);
                        cache.gstKnots = Math.round(cache.gstKnots);
                        ssGauge.VRB = ' - METAR: ' + ('0' + data.avgbearing).slice(-3) + ('0' + cache.avgKnots).slice(-2) + 'G' + ('0' + cache.gstKnots).slice(-2) + 'KT ';

                        if (windSpd > 0) {
                            // If variation less than 60 degrees, then METAR = Steady
                            // Unless range = 0 and from/to direction = avg + 180
                            range = (+cache.bearingTo < +cache.bearingFrom ?  360 + (+cache.bearingTo) : +cache.bearingTo) - (+cache.bearingFrom);

                            if (cache.avgKnots < 3) { // Europe uses 3kts, USA 6kts as the threshold
                                if (config.showRoseOnDirGauge) {
                                    ssGauge.setSection([steelseries.Section(cache.bearingFrom, cache.bearingTo, gaugeGlobals.windVariationSector)]);
                                    ssGauge.setSection([]);
                                } else {
                                    ssGauge.setSection([steelseries.Section(cache.bearingFrom, cache.bearingTo, gaugeGlobals.minMaxArea)]);
                                    ssGauge.setArea([]);
                                }
                            } else {
                                if (config.showRoseOnDirGauge) {
                                    ssGauge.setSection([steelseries.Section(cache.bearingFrom, cache.bearingTo, gaugeGlobals.windVariationSector)]);

                                } else {
                                    ssGauge.setSection([]);
                                    ssGauge.setArea([steelseries.Section(cache.bearingFrom, cache.bearingTo, gaugeGlobals.minMaxArea)]);
                                }
                            }

                            if ((range < 60 && range > 0) || range === 0 && cache.bearingFrom === cache.valueAverage) {
                                ssGauge.VRB += ' STDY';
                            } else {
                                if (cache.avgKnots < 3) { // Europe uses 3kts, USA 6kts as the threshold
                                    ssGauge.VRB += ' VRB';
                                } else {
                                    ssGauge.VRB += ' ' + cache.bearingFrom + 'V' + cache.bearingTo;
                                }
                            }
                        } else {
                            // Zero wind speed, calm
                            ssGauge.VRB = ' - METAR: 00000KT';
                            ssGauge.setSection([]);
                            if (!config.showRoseOnDirGauge) {
                                ssGauge.setArea([]);
                            }
                        }
                    } else {
                        ssGauge.VRB = '';
                    }

                    // optional rose data on direction gauge
                    if (config.showRoseOnDirGauge && data.WindRoseData) {
                        // Process rose data
                        rosePoints = data.WindRoseData.length;
                        roseSectionAngle = 360 / rosePoints;
                        // Find total for all directions
                        for (i = 0; i < rosePoints; i++) {
                            roseMax = Math.max(roseMax, data.WindRoseData[i]);
                        }
                        // Check we actually have some data, bad things happen if roseMax=0!
                        if (roseMax > 0) {
                            // Find relative value for each point, and create a gauge area for it
                            for (i = 0; i < rosePoints; i++) {
                                roseAreas[i] = steelseries.Section(
                                    i * roseSectionAngle - roseSectionAngle / 2,
                                    (i + 1) * roseSectionAngle - roseSectionAngle / 2,
                                    'rgba(' + gradient('2020D0', 'D04040', data.WindRoseData[i] / roseMax) + ',' + (data.WindRoseData[i] / roseMax).toFixed(2) + ')'
                                );
                            }
                        }
                        ssGauge.setArea(roseAreas);
                    }

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        tip = strings.latest_title + ' ' + strings.bearing_info + ': ' + cache.valueLatest + '° (' + getord(+cache.valueLatest) + ')' + ssGauge.VRB + '<br>' +
                              strings.tenminavg_web + ' ' + strings.bearing_info + ': ' + cache.valueAverage + '° (' + getord(+cache.valueAverage) + ')' + ', ' + strings.dominant_bearing + ': ' + data.domwinddir;
                        if (!config.showRoseGauge) {
                            // Wind run is shown on the wind rose if it is available
                            tip += '<br>' + strings.windruntoday + ': ' + data.windrun + ' ' + _displayUnits.windrun;
                        }
                        $('#imgtip7_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[7] !== null) {
                        $('#imgtip7_img').attr('src', config.imgPathURL + config.tipImgs[7] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Wind Rose Gauge
        //
        singleRose = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge


            var buffers = {};   // Stores references to the various canvas buffers
            var cache = {};     // various parameters to store for the life time of gauge
            var ctxRoseCanvas;  // 2D context for the plotted gauge

            cache.firstRun = true;
            cache.odoDigits = 5;  // Total number of odometer digits including the decimal

            function init() {
                var div, roseCanvas;
                // Get the context of the gauge canvas on the HTML page
                if ($('#canvas_rose').length) {

                    cache.gaugeSize = Math.ceil($('#canvas_rose').width() * config.gaugeScaling);
                    cache.gaugeSize2 = cache.gaugeSize / 2;
                    cache.showOdo = config.showRoseGaugeOdo || false;

                    cache.compassString = strings.compass;

                    // Create a hidden div to host the Rose plot
                    div = document.createElement('div');
                    div.style.display = 'none';
                    document.body.appendChild(div);

                    // Calcuate the size of the gauge background and so the size of rose plot required
                    cache.plotSize = Math.floor(cache.gaugeSize * 0.68);
                    cache.plotSize2 = cache.plotSize / 2;

                    // rose plot canvas buffer
                    buffers.plot = document.createElement('canvas');
                    buffers.plot.width = cache.plotSize;
                    buffers.plot.height = cache.plotSize;
                    buffers.plot.id = 'rosePlot';
                    buffers.ctxPlot = buffers.plot.getContext('2d');
                    div.appendChild(buffers.plot);

                    // Create a steelseries gauge frame
                    buffers.frame = document.createElement('canvas');
                    buffers.frame.width = cache.gaugeSize;
                    buffers.frame.height = cache.gaugeSize;
                    buffers.ctxFrame = buffers.frame.getContext('2d');
                    steelseries.drawFrame(buffers.ctxFrame, gaugeGlobals.frameDesign, cache.gaugeSize2, cache.gaugeSize2, cache.gaugeSize, cache.gaugeSize);

                    // Create a steelseries gauge background
                    buffers.background = document.createElement('canvas');
                    buffers.background.width = cache.gaugeSize;
                    buffers.background.height = cache.gaugeSize;
                    buffers.ctxBackground = buffers.background.getContext('2d');
                    steelseries.drawBackground(buffers.ctxBackground, gaugeGlobals.background, cache.gaugeSize2, cache.gaugeSize2, cache.gaugeSize, cache.gaugeSize);
                    // Optional - add a background image
                    /*
                    if (g_imgSmall !== null) {
                        var drawSize = g_size * 0.831775;
                        var x = (g_size - drawSize) / 2;
                        buffers.ctxBackground.drawImage(g_imgSmall, x, x, cache.gaugeSize, cache.gaugeSize);
                    }
                    */
                    // Add the compass points
                    drawCompassPoints(buffers.ctxBackground, cache.gaugeSize);

                    // Create a steelseries gauge foreground
                    buffers.foreground = document.createElement('canvas');
                    buffers.foreground.width = cache.gaugeSize;
                    buffers.foreground.height = cache.gaugeSize;
                    buffers.ctxForeground = buffers.foreground.getContext('2d');
                    steelseries.drawForeground(buffers.ctxForeground, gaugeGlobals.foreground, cache.gaugeSize, cache.gaugeSize, false);

                    roseCanvas = document.getElementById('canvas_rose');
                    ctxRoseCanvas = roseCanvas.getContext('2d');
                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_rose').css({'width': cache.gaugeSize + 'px', 'height': cache.gaugeSize + 'px'});
                    }
                    // resize canvas on main page
                    roseCanvas.width = cache.gaugeSize;
                    roseCanvas.height = cache.gaugeSize;
                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_rose').css(gaugeShadow(cache.gaugeSize));
                    }

                    // Render an empty gauge, looks better than just the shadow background and odometer ;)
                    // Paint the gauge frame
                    ctxRoseCanvas.drawImage(buffers.frame, 0, 0);

                    // Paint the gauge background
                    ctxRoseCanvas.drawImage(buffers.background, 0, 0);

                    // Paint the gauge foreground
                    ctxRoseCanvas.drawImage(buffers.foreground, 0, 0);

                    // Create an odometer
                    if (cache.showOdo) {
                        cache.odoHeight = Math.ceil(cache.gaugeSize * 0.08); // Sets the size of the odometer
                        cache.odoWidth = Math.ceil(Math.floor(cache.odoHeight * 0.68) * cache.odoDigits);  // 'Magic' number, do not alter
                        // Create a new canvas for the oodometer
                        buffers.Odo = document.createElement('canvas');
                        $(buffers.Odo).attr({
                            'id': 'canvas_odo',
                            'width': cache.odoWidth,
                            'height': cache.odoHeight
                        });
                        // Position it
                        $(buffers.Odo).css({
                            'position': 'absolute',
                            'top': Math.ceil(cache.gaugeSize * 0.7 + $('#canvas_rose').position().top) + 'px',
                            'left': Math.ceil((cache.gaugeSize - cache.odoWidth) / 2 + $('#canvas_rose').position().left) + 'px'
                        });
                        // Insert it into the DOM before the Rose gauge
                        $(buffers.Odo).insertBefore('#canvas_rose');
                        // Create the odometer
                        ssGauge = new steelseries.Odometer('canvas_odo', {
                            height: cache.odoHeight,
                            digits: cache.odoDigits - 1,
                            decimals: 1
                        });
                    }
                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                cache.firstRun = false;

                function update() {
                    var rose, offset;

                    if (ctxRoseCanvas && !cache.firstRun) {

                        // Clear the gauge
                        ctxRoseCanvas.clearRect(0, 0, cache.gaugeSize, cache.gaugeSize);

                        // Clear the existing rose plot
                        buffers.ctxPlot.clearRect(0, 0, cache.plotSize, cache.plotSize);

                        // Create a new rose plot
                        rose = new RGraph.Rose('rosePlot', data.WindRoseData);
                        rose.Set('chart.strokestyle', 'black');
                        rose.Set('chart.background.axes.color', 'gray');
                        rose.Set('chart.colors.alpha', 0.5);
                        rose.Set('chart.colors', ['Gradient(#408040:red:#7070A0)']);
                        rose.Set('chart.margin', Math.ceil(40 / data.WindRoseData.length));

                        rose.Set('chart.title', cache.gaugeTitle);
                        rose.Set('chart.title.size', Math.ceil(0.05 * cache.plotSize));
                        rose.Set('chart.title.bold', false);
                        rose.Set('chart.title.color', gaugeGlobals.background.labelColor.getRgbColor());
                        rose.Set('chart.gutter.top', 0.2 * cache.plotSize);
                        rose.Set('chart.gutter.bottom', 0.2 * cache.plotSize);

                        rose.Set('chart.tooltips.effect', 'snap');
                        rose.Set('chart.labels.axes', '');
                        rose.Set('chart.background.circles', true);
                        rose.Set('chart.background.grid.spokes', 16);
                        rose.Set('chart.radius', cache.plotSize2);
                        rose.Draw();

                        // Paint the gauge frame
                        ctxRoseCanvas.drawImage(buffers.frame, 0, 0);

                        // Paint the gauge background
                        ctxRoseCanvas.drawImage(buffers.background, 0, 0);

                        // Paint the rose plot
                        offset = Math.floor(cache.gaugeSize2 - cache.plotSize2);
                        ctxRoseCanvas.drawImage(buffers.plot, offset, offset);

                        // Paint the gauge foreground
                        ctxRoseCanvas.drawImage(buffers.foreground, 0, 0);

                        // update the odometer
                        if (cache.showOdo) {
                            ssGauge.setValueAnimated(extractDecimal(data.windrun));
                        }

                        // update tooltip
                        if (ddimgtooltip.showTips) {
                            $('#imgtip10_txt').html(strings.dominant_bearing + ': ' + data.domwinddir + '<br>' +
                                                    strings.windruntoday + ': ' + data.windrun + ' ' + _displayUnits.windrun);
                        }
                    }

                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[10] !== null) {
                        $('#imgtip10_img').attr('src', config.imgPathURL + config.tipImgs[10] + cacheDefeat);
                    }
                }

                // Helper function to put the compass points on the background
                function drawCompassPoints(ctx, size) {
                    ctx.save();
                    // set the font
                    ctx.font = 0.08 * size + 'px serif';
                    ctx.fillStyle = gaugeGlobals.background.labelColor.getRgbColor();
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';

                    // Draw the compass points
                    for (var i = 0; i < 4; i++) {
                        ctx.translate(size / 2, size * 0.125);
                        ctx.fillText(cache.compassString[i * 2], 0, 0, size);
                        ctx.translate(-size / 2, -size * 0.125);
                        // Move to center
                        ctx.translate(size / 2, size / 2);
                        ctx.rotate(Math.PI / 2);
                        ctx.translate(-size / 2, -size / 2);
                    }
                    ctx.restore();
                }

                function setTitle(newTitle) {
                    cache.gaugeTitle = newTitle;
                }

                function setCompassString(newStr) {

                    cache.compassString = newStr;

                    if (!cache.firstRun) {

                        // Redraw the background
                        steelseries.drawBackground(buffers.ctxBackground, gaugeGlobals.background, cache.gaugeSize2, cache.gaugeSize2, cache.gaugeSize, cache.gaugeSize);

                        // Add the compass points
                        drawCompassPoints(buffers.ctxBackground, cache.gaugeSize);
                    }
                }

                return {
                    update: update,
                    gauge: ssGauge,
                    drawCompassPoints: drawCompassPoints,
                    setTitle: setTitle,
                    setCompassString: setCompassString
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the UV-Index Gauge
        //
        singleUV = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define UV start values
                cache.value = 0.0001;
                cache.sections = [
                    steelseries.Section(0, 2.9, '#289500'),
                    steelseries.Section(2.9, 5.8, '#f7e400'),
                    steelseries.Section(5.8, 7.8, '#f85900'),
                    steelseries.Section(7.8, 10.9, '#d8001d'),
                    steelseries.Section(10.9, 20, '#6b49c8')
                ];
                // Define value gradient for UV
                cache.gradient = new steelseries.gradientWrapper(0, 16,
                    [0, 0.1, 0.19, 0.31, 0.45, 0.625, 1],
                    [
                        new steelseries.rgbaColor(0, 200, 0, 1),
                        new steelseries.rgbaColor(0, 200, 0, 1),
                        new steelseries.rgbaColor(255, 255, 0, 1),
                        new steelseries.rgbaColor(248, 89, 0, 1),
                        new steelseries.rgbaColor(255, 0, 0, 1),
                        new steelseries.rgbaColor(255, 0, 144, 1),
                        new steelseries.rgbaColor(153, 140, 255, 1)
                    ]
                );
                cache.useSections = false;
                cache.useValueGradient = true;

                // create UV bargraph gauge
                if ($('#canvas_uv').length) {
                    params.size = Math.ceil($('#canvas_uv').width() * config.gaugeScaling);
                    params.gaugeType = steelseries.GaugeType.TYPE3;
                    params.maxValue = gaugeGlobals.uvScaleDefMax;
                    params.titleString = strings.uv_title;
                    params.niceScale = false;
                    params.section = cache.sections;
                    params.useSectionColors = cache.useSections;
                    params.valueGradient = cache.gradient;
                    params.useValueGradient = cache.useValueGradient;
                    params.lcdDecimals = gaugeGlobals.uvLcdDecimals;

                    ssGauge = new steelseries.RadialBargraph('canvas_uv', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_uv').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_uv').css(gaugeShadow(params.size));
                    }

                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    var tip, indx;

                    cache.value = extractDecimal(data.UV);

                    if (+cache.value === 0) {
                        indx = 0;
                    } else if (cache.value < 2.5) {
                        indx = 1;
                    } else if (cache.value < 5.5) {
                        indx = 2;
                    } else if (cache.value < 7.5) {
                        indx = 3;
                    } else if (cache.value < 10.5) {
                        indx = 4;
                    } else {
                        indx = 5;
                    }


                    if (cache.value > ssGauge.getMaxValue()) {
                        ssGauge.setMaxValue(Math.ceil(cache.value) + Math.ceil(cache.value) % 2);
                    }

                    cache.risk = strings.uv_levels[indx];
                    cache.headLine = strings.uv_headlines[indx];
                    cache.detail = strings.uv_details[indx];
                    ssGauge.setUnitString(cache.risk);
                    ssGauge.setValueAnimated(cache.value);

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        tip = '<b>' + strings.uv_title + ': ' + cache.value + '</b> - <i>' + strings.solar_maxToday + ': ' + data.UVTH + '</i><br>';
                        tip += '<i>' + cache.headLine + '</i><br>';
                        tip += cache.detail;
                        $('#imgtip8_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[8] !== null) {
                        $('#imgtip8_img').attr('src', config.imgPathURL + config.tipImgs[8] + cacheDefeat);
                    }
            }

                return {
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Solar Irradiation Gauge
        //
        singleSolar = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                // define Solar start values
                cache.value = 0.0001;
                cache.sections = [
                    steelseries.Section(0, 600, 'rgba(40,149,0,0.3)'),
                    steelseries.Section(600, 800, 'rgba(248,89,0,0.3)'),
                    steelseries.Section(800, 1000, 'rgba(216,0,29,0.3)'),
                    steelseries.Section(1000, 1800, 'rgba(107,73,200,0.3)')
                ];

                // create Solar gauge
                if ($('#canvas_solar').length) {
                    params.size = Math.ceil($('#canvas_solar').width() * config.gaugeScaling);
                    params.section = cache.sections;
                    params.maxValue = gaugeGlobals.solarGaugeScaleMax;
                    params.titleString = strings.solar_title;
                    params.unitString = 'Wm\u207B\u00B2';
                    params.niceScale = false;
                    params.thresholdVisible = false;
                    params.lcdDecimals = 0;

                    if (config.showSunshineLed) {
                        params.userLedVisible = true;
                        params.userLedColor = steelseries.LedColor.YELLOW_LED;
                    }

                    ssGauge = new steelseries.Radial('canvas_solar', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_solar').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_solar').css(gaugeShadow(params.size));
                    }
                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    var tip, percent;

                    cache.value = +extractInteger(data.SolarRad);
                    cache.maxToday = extractInteger(data.SolarTM);
                    cache.currMaxValue = +extractInteger(data.CurrentSolarMax);
                    percent = (+cache.currMaxValue === 0 ? '--' : Math.round(+cache.value / +cache.currMaxValue * 100));

                    // Set a section (100 units wide) to show current theoretical max value
                    if (data.CurrentSolarMax !== 'N/A') {
                        ssGauge.setArea([steelseries.Section(cache.currMaxValue, Math.min(cache.currMaxValue + 100, gaugeGlobals.solarGaugeScaleMax), 'rgba(220,0,0,0.5)')]);
                    }

                    // Need to rescale the gauge?
                    cache.maxValue = Math.max(
                        Math.ceil(cache.value / 100) * 100,
                        Math.ceil(cache.currMaxValue / 100) * 100,
                        Math.ceil(cache.maxToday / 100) * 100,
                        gaugeGlobals.solarGaugeScaleMax);
                    if (cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setMaxValue(cache.maxValue);
                    }

                    // Set the values
                    ssGauge.setMaxMeasuredValue(cache.maxToday);
                    ssGauge.setValueAnimated(cache.value);

                    if (config.showSunshineLed) {
                        ssGauge.setUserLedOnOff((percent !== '--' && percent >= gaugeGlobals.sunshineThresholdPct && +cache.value >= gaugeGlobals.sunshineThreshold) ? true : false);
                    }

                    if (ddimgtooltip.showTips) {
                        // update tooltip
                        tip = '<b>' + strings.solar_title + ': ' + cache.value + ' W/m²</b> - ' +
                              '<i>' + percent + '% ' + strings.solar_ofMax + '</i><br>' +
                              strings.solar_currentMax + ': ' + cache.currMaxValue + ' W/m²';
                        if (data.SolarTM !== undefined) {
                            tip += '<br>' + strings.solar_maxToday + ': ' + cache.maxToday + ' W/m²';
                        }
                        $('#imgtip9_txt').html(tip);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[9] !== null) {
                        $('#imgtip9_img').attr('src', config.imgPathURL + config.tipImgs[9] + cacheDefeat);
                    }
                }

                return {
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // Singleton for the Cloudbase Gauge
        //
        singleCloudBase = (function () {
            var instance;   // Stores a reference to the Singleton
            var ssGauge;    // Stores a reference to the SS Gauge
            var cache = {};      // Stores various config values and parameters

            function init() {
                var params = $.extend(true, {}, commonParams);

                cache.sections = createCloudBaseSections(true);
                cache.value = 0.0001;
                cache.maxValue = gaugeGlobals.cloudScaleDefMaxm;

                // create Cloud base radial gauge
                if ($('#canvas_cloud').length) {
                    params.size = Math.ceil($('#canvas_cloud').width() * config.gaugeScaling);
                    params.section = cache.sections;
                    params.maxValue = cache.maxValue;
                    params.titleString = strings.cloudbase_title;
                    params.unitString = strings.metres;
                    params.thresholdVisible = false;
                    params.lcdDecimals = 0;

                    ssGauge = new steelseries.Radial('canvas_cloud', params);
                    ssGauge.setValue(cache.value);

                    // over-ride CSS applied size?
                    if (config.gaugeScaling !== 1) {
                        $('#canvas_cloud').css({'width': params.size + 'px', 'height': params.size + 'px'});
                    }

                    // add a shadow to the gauge
                    if (config.showGaugeShadow) {
                        $('#canvas_cloud').css(gaugeShadow(params.size));
                    }
                    // subcribe to data updates
                    $.subscribe('gauges.dataUpdated', update);
                    $.subscribe('gauges.graphUpdate', updateGraph);
                } else {
                    // cannot draw gauge, return null
                    return null;
                }

                function update() {
                    cache.value = extractInteger(data.cloudbasevalue);

                    if (data.cloudbaseunit === 'm') {
                        // adjust metre gauge in jumps of 1000 metres, don't downscale during the session
                        cache.maxValue = Math.max(Math.ceil(cache.value / 1000) * 1000, gaugeGlobals.cloudScaleDefMaxm, cache.maxValue);
                        if (cache.value <= 1000) {
                            // and round the value to the nearest  10 m
                            cache.value = Math.round(cache.value / 10) * 10;
                        } else {
                            // and round the value to the nearest 50 m
                            cache.value = Math.round(cache.value / 50) * 50;
                        }
                    } else {
                        // adjust feet gauge in jumps of 2000 ft, don't downscale during the session
                        cache.maxValue = Math.max(Math.ceil(cache.value / 2000) * 2000, gaugeGlobals.cloudScaleDefMaxft, cache.maxValue);
                        if (cache.value <= 2000) {
                            // and round the value to the nearest 50 ft
                            cache.value = Math.round(cache.value / 50) * 50;
                        } else {
                            // and round the value to the nearest 50 ft
                            cache.value = Math.round(cache.value / 100) * 100;
                        }
                   }

                    if (cache.maxValue !== ssGauge.getMaxValue()) {
                        ssGauge.setValue(0);
                        ssGauge.setMaxValue(cache.maxValue);
                    }
                    ssGauge.setValueAnimated(cache.value);

                    if (config.showPopupData) {
                        // static tooltip on cloud gauge
                        $('#imgtip11_txt').html('<strong>' + strings.cloudbase_popup_title + '</strong><br>' + strings.cloudbase_popup_text);
                    }
                } // End of update()

                function updateGraph(evnt, cacheDefeat) {
                    if (config.tipImgs[11] !== null) {
                        $('#imgtip11_img').attr('src', config.imgPathURL + config.tipImgs[11] + cacheDefeat);
                    }
                }

                return {
                    data: cache,
                    update: update,
                    gauge: ssGauge
                };
            } // End of init()

            return {
                // Get the Singleton instance if one exists
                // or create one if it doesn't
                getInstance: function () {
                    if (!instance) {
                        instance = init();
                    }
                    return instance;
                }
            };
        })(),

        //
        // getRealtime() fetches the realtimegauges JSON data from the server
        //
        getRealtime = function () {
            var url = config.realTimeURL;
            if ($.active > 0) {
                // kill any outstanding requests
                _jqXHR.abort();
            }
            if (config.longPoll) {
                url += '?timestamp=' + _timestamp;
            }
            _jqXHR = $.ajax({url: url,
                            cache: (config.longPoll ? true : false),
                            dataType: 'json',
                            timeout: 21000 // 21 second time-out by default
                        }).done(function (data) {
                            checkRtResp(data);
                        }).fail(function (xhr, status, err) {
                            checkRtError(xhr, status, err);
                        });
        },

        //
        // checkRtResp() called by the Ajax fetch once data has been downloaded
        //
        checkRtResp = function (response) {
            var delay;
            statusTimer.reset(config.longPoll ? 1 : config.realtimeInterval);
            _httpError = 0;
            if (config.longPoll && response.status !== 'OK') {
                checkRtError(null, 'PHP Error', response.status);
            } else {
                if (processData(response)) {
                    delay = _ajaxDelay;
                } else {
                    delay = 5;
                }
                if (delay > 0) {
                    _downloadTimer = setTimeout(getRealtime, delay * 1000);
                } else {
                    getRealtime();
                }
            }
        },

        //
        // checkRtError() called by the Ajax fetch if an error occurs during the fetching realtimegauges.txt
        //
        checkRtError = function (xhr, status, error) {
            if (xhr.statusText !== 'abort') {
                if (!config.longPoll) {
                    // Clear any existing download timer
                    clearTimeout(_downloadTimer);
                }
                ledIndicator.setLedOnOff(false);
                ledIndicator.setTitle(strings.led_title_unknown);
                statusScroller.setText(status + ': ' + error);
                // wait 5 seconds, then try again...
                _downloadTimer = setTimeout(getRealtime, 5000);
            }
        },

        //
        // processData() massages the data returned in realtimegauges.txt, and calls doUpdate() to update the page
        //
        processData = function (dataObj) {
            var str, dt, tm, today, now, then, tmp, elapsedMins, retVal;
            // copy the realtime fields into the global 'data' object
            if (config.longPoll) {
                _timestamp = dataObj.timestamp;
                data = dataObj.data;
            } else {
                //normal polling
                data = dataObj;
            }

            // and check we have the expected version number
            if (data.ver !== undefined && data.ver >= _realtimeVer) {

                // mainpulate the last rain time into something more friendly
                try {
                    str = data.LastRainTipISO.split(' ');
                    dt = str[0].replace(/\//g, '-').split('-');  // WD uses dd/mm/yyyy, we use a '-'
                    tm = str[1].split(':');
                    today = new Date();
                    today.setHours(0, 0, 0, 0);
                    if (data.dateFormat === undefined) {
                        data.dateFormat = 'y/m/d';
                    } else {
                        // frig for WD bug which leaves a trailing % character from the tag
                        data.dateFormat = data.dateFormat.replace('%', '');
                    }
                    if (data.dateFormat === 'y/m/d') {
                        // ISO/Cumulus format
                        then = new Date(dt[0], dt[1] - 1, dt[2], tm[0], tm[1], 0, 0);
                    } else if (data.dateFormat === 'd/m/y') {
                        then = new Date(dt[2], dt[1] - 1, dt[0], tm[0], tm[1], 0, 0);
                    } else { // m/d/y
                        then = new Date(dt[2], dt[0] - 1, dt[1], tm[0], tm[1], 0, 0);
                    }
                    if (then.getTime() >= today.getTime()) {
                        data.LastRained = strings.LastRainedT_info + ' ' + str[1];
                    } else if (then.getTime() + 86400000 >= today.getTime()) {
                        data.LastRained = strings.LastRainedY_info + ' ' + str[1];
                    } else {
                        data.LastRained = then.getDate().toString() + ' ' + strings.months[then.getMonth()] + ' ' + strings.at + ' ' + str[1];
                    }
                } catch(e) {
                    data.LastRained = data.LastRainTipISO;
                }
                if (data.tempunit.length > 1) {
                    // clean up temperature units - remove html encoded degree symbols
                    data.tempunit = data.tempunit.replace(/&\S*;/, '°');  // old Cumulus versions uses &deg;, WeatherCat uses &#176;
                } else {
                    // using new realtimegaugesT.txt with Cumulus > 1.9.2
                    data.tempunit = '°' + data.tempunit;
                }

                // Check for station off-line
                now = Date.now();
                tmp = data.timeUTC.split(',');
                _sampleDate = Date.UTC(tmp[0], tmp[1] - 1, tmp[2], tmp[3], tmp[4], tmp[5]);
                if (now - _sampleDate > config.stationTimeout * 60 * 1000) {
                    elapsedMins = Math.floor((now - _sampleDate) / (1000 * 60));
                    // the realtimegauges.txt file isn't being updated
                    ledIndicator.setLedColor(steelseries.LedColor.RED_LED);
                    ledIndicator.setTitle(strings.led_title_offline);
                    ledIndicator.blink(true);
                    if (elapsedMins < 120) {
                        // up to 2 hours ago
                        tm = elapsedMins.toString() + ' ' + strings.StatusMinsAgo;
                    } else if (elapsedMins < 2 * 24 * 60) {
                        // up to 48 hours ago
                        tm = Math.floor(elapsedMins / 60).toString() + ' ' + strings.StatusHoursAgo;
                    } else {
                        // days ago!
                        tm = Math.floor(elapsedMins / (60 * 24)).toString() + ' ' + strings.StatusDaysAgo;
                    }
                    data.forecast = strings.led_title_offline + ' ' + strings.StatusLastUpdate + ' ' + tm;
                } else if (+data.SensorContactLost === 1) {
                    // Fine Offset sensor status
                    ledIndicator.setLedColor(steelseries.LedColor.RED_LED);
                    ledIndicator.setTitle(strings.led_title_lost);
                    ledIndicator.blink(true);
                    data.forecast = strings.led_title_lost;
                } else {
                    ledIndicator.setLedColor(steelseries.LedColor.GREEN_LED);
                    ledIndicator.setTitle(strings.led_title_ok + '. ' + strings.StatusLastUpdate + ': ' + data.date);
                    ledIndicator.blink(false);
                    ledIndicator.setLedOnOff(true);
                }

                // de-encode the forecast string if required (Cumulus support for extended characters)
                data.forecast = $('<div/>').html(data.forecast).text();
                data.forecast = data.forecast.trim();

                data.pressunit = data.pressunit.trim();  // WView sends ' in', ' mb', or ' hPa'
                if (data.pressunit === 'in') {  // Cumulus and WView send 'in'
                    data.pressunit = 'inHg';
                }

                data.windunit = data.windunit.trim(); // WView sends ' kmh' etc
                data.windunit = data.windunit.toLowerCase(); // WeatherCat sends "MPH"
                if (data.windunit === 'knots') {             // WeatherCat/weewx send "Knots", we use "kts"
                    data.windunit = 'kts';
                }

                if (data.windunit === 'kmh' || data.windunit === 'kph') {  // WD wind unit omits '/', weewx sends 'kph'
                    data.windunit = 'km/h';
                }

                data.rainunit = data.rainunit.trim(); // WView sends ' mm' etc

                // take a look at the cloud base data...
                // change WeatherCat units from Metres/Feet to m/ft
                try {
                    if (data.cloudbaseunit.toLowerCase() === 'metres') {
                        data.cloudbaseunit = 'm';
                    } else if (data.cloudbaseunit.toLowerCase()=== 'feet') {
                        data.cloudbaseunit = 'ft';
                    }
                } catch(e) {
                    cloudbaseunit = '';
                }
                if (config.showCloudGauge && (
                        (config.weatherProgram === 4 || config.weatherProgram === 5) ||
                        data.cloudbasevalue === "")) {
                    // WeatherCat and VWS (and WView?) do not provide a cloud base value, so we have to calculate it...
                    // assume if the station uses an imperial wind speed they want cloud base in feet, otherwise metres
                    data.cloudbaseunit = (data.windunit === 'mph' || data.windunit === 'kts') ? 'ft' : 'm';
                    data.cloudbasevalue = calcCloudbase(data.temp, data.tempunit, data.dew, data.cloudbaseunit);
                }

                // Temperature data conversion for display required?
                if (data.tempunit[1] !== _displayUnits.temp && _userUnitsSet) {
                    // temp needs converting
                    if (data.tempunit[1] === 'C') {
                        convTempData(c2f);
                    } else {
                        convTempData(f2c);
                    }
                } else if (_firstRun) {
                    _displayUnits.temp = data.tempunit[1];
                    setRadioCheck('rad_unitsTemp', _displayUnits.temp);
                }

                // Rain data conversion for display required?
                if (data.rainunit !== _displayUnits.rain && _userUnitsSet) {
                    // rain needs converting
                    convRainData(_displayUnits.rain === 'mm' ? in2mm : mm2in);
                } else if (_firstRun) {
                    _displayUnits.rain = data.rainunit;
                    setRadioCheck('rad_unitsRain', _displayUnits.rain);
                }

                // Wind data conversion for display required?
                if (data.windunit !== _displayUnits.wind && _userUnitsSet) {
                    // wind needs converting
                    convWindData(data.windunit, _displayUnits.wind);
                } else if (_firstRun) {
                    _displayUnits.wind = data.windunit;
                    _displayUnits.windrun = getWindrunUnits(data.windunit);
                    setRadioCheck('rad_unitsWind', _displayUnits.wind);
                }

                // Pressure data conversion for display required?
                if (data.pressunit !== _displayUnits.press && _userUnitsSet) {
                    convBaroData(data.pressunit, _displayUnits.press);
                } else if (_firstRun) {
                    _displayUnits.press = data.pressunit;
                    setRadioCheck('rad_unitsPress', _displayUnits.press);
                }

                // Cloud height data conversion for display required?
                if (data.cloudbaseunit !== _displayUnits.cloud && _userUnitsSet) {
                    // Cloud height needs converting
                    convCloudBaseData(_displayUnits.cloud === 'm' ? ft2m : m2ft);
                } else if (_firstRun) {
                    _displayUnits.cloud = data.cloudbaseunit;
                    setRadioCheck('rad_unitsCloud', _displayUnits.cloud);
                }

                statusScroller.setText(data.forecast);

                // first time only, setup units etc
                if (_firstRun) {
                    doFirst();
                }

                // publish the update, use the shared data object rather than transferring it
                $.publish('gauges.dataUpdated', {});

                retVal = true;

            } else {
                // set an error message
                if (data.ver < _realtimeVer) {
                    statusTimer.setValue(0);
                    statusScroller.setText('Your ' + config.realTimeURL.substr(config.realTimeURL.lastIndexOf('/') + 1) + ' file template needs updating!');
                    return;
                } else {
                    // oh-oh! The number of data fields isn't what we expected
                    statusScroller.setText(strings.realtimeCorrupt);
                }
                ledIndicator.setLedOnOff(false);
                ledIndicator.setTitle(strings.led_title_unknown);

                retVal = false;
            }
            return retVal;
        },


        //
        // pagetimeout() called once every config.pageUpdateLimit minutes to stop updates and prevent page 'sitters'
        //
        pageTimeout = function () {
            statusScroller.setText(strings.StatusPageLimit);
            ledIndicator.setLedColor(steelseries.LedColor.RED_LED);
            ledIndicator.setTitle(strings.StatusPageLimit);
            ledIndicator.blink(true);
            ledIndicator.setTitle(strings.StatusTimeout);

            // stop any pending download
            clearTimeout(_downloadTimer);

            // stop any long polling in progress
            if ($.active > 0) {
                _jqXHR.abort();
            }

            // stop the clock
            clearInterval(_tickTockInterval);

            // clear the timer display
            statusTimer.setValue(0);

            // set an onclick event on the LED to restart everything
            $('#canvas_led').click(function () {
                    // disable the onClick event again
                    $('#canvas_led').unbind('click');
                    // reset the timer count to 1
                    statusTimer.reset(1);
                    // restart the timer to update the status time
                    _tickTockInterval = setInterval(function () {
                            $.publish('gauges.clockTick', null);
                        },
                        1000);

                    // restart the page timeout timer, so we hit this code again
                    setTimeout(pageTimeout, config.pageUpdateLimit * 60 * 1000);

                    // refresh the page data
                    getRealtime();
                });
        },


        //
        // doFirst() called by doUpdate() the first time the page is updated to set-up various things that are
        // only known when the realtimegauges.txt data is available
        //
        doFirst = function () {
            var cacheDefeat = '?' + (new Date()).getTime().toString();

            if (data.tempunit[1] === 'F') {
                _displayUnits.temp = 'F';
                setRadioCheck('rad_unitsTemp', 'F');
                setTempUnits(false);
            }

            if (data.pressunit !== 'hPa') {
                _displayUnits.press = data.pressunit;
                setRadioCheck('rad_unitsPress', data.pressunit);
                setBaroUnits(data.pressunit);
            }

            if (data.windunit !== 'km/h') {
                _displayUnits.wind = data.windunit;
                setRadioCheck('rad_unitsWind', data.windunit);
                setWindUnits(data.windunit);
            }


            if (data.rainunit !== 'mm') {
                _displayUnits.rain = data.rainunit;
                setRadioCheck('rad_unitsRain', data.rainunit);
                setRainUnits(false);
            }

            if (config.showSolarGauge && data.SolarTM !== undefined && gaugeSolar) {
                gaugeSolar.gauge.setMaxMeasuredValueVisible(true);
            }


            if (config.showCloudGauge && data.cloudbaseunit !== 'm') {
                _displayUnits.cloud = data.cloudbaseunit;
                setRadioCheck('rad_unitsCloud', data.cloudbaseunit);
                setCloudBaseUnits(false);
            }

            // set the script version on the page
            $('#scriptVer').html(config.scriptVer);
            // set the version information from the station
            $('#programVersion').html(data.version);
            $('#programBuild').html(data.build);
            $('#programName').html(_programLink[config.weatherProgram]);

            // has a page time-out over ride password been supplied?
            _pageUpdateParam = getUrlParam('pageUpdate');

            if (config.showPopupData) {
                // now initialise the pop-up script and download the trend images
                // - has to be done here as doFirst may remove elements from the page
                // - and we delay the download of the images speeding up page display
                ddimgtooltip.init('[id^="tip_"]');
                // Are we running on a phone device (or really low res screen)?
                if ($(window).width() < 480) {
                    $('.ddimgtooltip').filter(':hidden').width('200px');
                }
            }

            if (config.showPopupData && config.showPopupGraphs) {
                // now download the trend images
                // - has to be done here as doFirst may remove elements from the page
                // - and we delay the download of the images speeding up page display
                //
                $('#imgtip0_img').attr('src', config.imgPathURL + config.tipImgs[0][0] + cacheDefeat);
                if (gaugeDew) {
                    $('#imgtip1_img').attr('src', config.imgPathURL + config.tipImgs[1][gaugeDew.data.popupImg] + cacheDefeat);
                }
                $('#imgtip2_img').attr('src', config.imgPathURL + config.tipImgs[2] + cacheDefeat);
                $('#imgtip3_img').attr('src', config.imgPathURL + config.tipImgs[3] + cacheDefeat);
                $('#imgtip4_img').attr('src', config.imgPathURL + config.tipImgs[4][0] + cacheDefeat);
                $('#imgtip5_img').attr('src', config.imgPathURL + config.tipImgs[5] + cacheDefeat);
                $('#imgtip6_img').attr('src', config.imgPathURL + config.tipImgs[6] + cacheDefeat);
                $('#imgtip7_img').attr('src', config.imgPathURL + config.tipImgs[7] + cacheDefeat);
                $('#imgtip8_img').attr('src', config.imgPathURL + config.tipImgs[8] + cacheDefeat);
                $('#imgtip9_img').attr('src', config.imgPathURL + config.tipImgs[9] + cacheDefeat);
                $('#imgtip10_img').attr('src', config.imgPathURL + config.tipImgs[10] + cacheDefeat);
                $('#imgtip11_img').attr('src', config.imgPathURL + config.tipImgs[11] + cacheDefeat);
                // start a timer for popup graphic updates
                setInterval(function () {
                        $.publish('gauges.graphUpdate', '?' + (new Date()).getTime().toString());
                    },
                    config.graphUpdateTime * 60 * 1000);

            }

            _firstRun = false;

        },

        //
        // createTempSections() creates an array of gauge sections appropriate for Celsius or Fahrenheit scales
        //
        createTempSections = function (celsius) {
            var section;
            if (celsius) {
                section = [
                    steelseries.Section(-100, -35, 'rgba(195,  92, 211, 0.4)'),
                    steelseries.Section(-35,  -30, 'rgba(139,  74, 197, 0.4)'),
                    steelseries.Section(-30,  -25, 'rgba( 98,  65, 188, 0.4)'),
                    steelseries.Section(-25,  -20, 'rgba( 62,  66, 185, 0.4)'),
                    steelseries.Section(-20,  -15, 'rgba( 42,  84, 194, 0.4)'),
                    steelseries.Section(-15,  -10, 'rgba( 25, 112, 210, 0.4)'),
                    steelseries.Section(-10,   -5, 'rgba(  9, 150, 224, 0.4)'),
                    steelseries.Section(-5,     0, 'rgba(  2, 170, 209, 0.4)'),
                    steelseries.Section(0,      5, 'rgba(  0, 162, 145, 0.4)'),
                    steelseries.Section(5,     10, 'rgba(  0, 158, 122, 0.4)'),
                    steelseries.Section(10,    15, 'rgba( 54, 177,  56, 0.4)'),
                    steelseries.Section(15,    20, 'rgba(111, 202,  56, 0.4)'),
                    steelseries.Section(20,    25, 'rgba(248, 233,  45, 0.4)'),
                    steelseries.Section(25,    30, 'rgba(253, 142,  42, 0.4)'),
                    steelseries.Section(30,    40, 'rgba(236,  45,  45, 0.4)'),
                    steelseries.Section(40,   100, 'rgba(245, 109, 205, 0.4)')
                ];
            } else {
                section = [
                    steelseries.Section(-200, -30, 'rgba(195,  92, 211, 0.4)'),
                    steelseries.Section(-30,  -25, 'rgba(139,  74, 197, 0.4)'),
                    steelseries.Section(-25,  -15, 'rgba( 98,  65, 188, 0.4)'),
                    steelseries.Section(-15,   -5, 'rgba( 62,  66, 185, 0.4)'),
                    steelseries.Section(-5,     5, 'rgba( 42,  84, 194, 0.4)'),
                    steelseries.Section(5,     15, 'rgba( 25, 112, 210, 0.4)'),
                    steelseries.Section(15,    25, 'rgba(  9, 150, 224, 0.4)'),
                    steelseries.Section(25,    32, 'rgba(  2, 170, 209, 0.4)'),
                    steelseries.Section(32,    40, 'rgba(  0, 162, 145, 0.4)'),
                    steelseries.Section(40,    50, 'rgba(  0, 158, 122, 0.4)'),
                    steelseries.Section(50,    60, 'rgba( 54, 177,  56, 0.4)'),
                    steelseries.Section(60,    70, 'rgba(111, 202,  56, 0.4)'),
                    steelseries.Section(70,    80, 'rgba(248, 233,  45, 0.4)'),
                    steelseries.Section(80,    90, 'rgba(253, 142,  42, 0.4)'),
                    steelseries.Section(90,   110, 'rgba(236,  45,  45, 0.4)'),
                    steelseries.Section(110,  200, 'rgba(245, 109, 205, 0.4)')
                ];
            }
            return section;
        },

        //
        // createRainSections() returns an array of section highlights for the Rain Rate gauge
        //
        /*
          Assumes 'standard' descriptive limits from UK met office:
           < 0.25 mm/hr - Very light rain
           0.25mm/hr to 1.0mm/hr - Light rain
           1.0 mm/hr to 4.0 mm/hr - Moderate rain
           4.0 mm/hr to 16.0 mm/hr - Heavy rain
           16.0 mm/hr to 50 mm/hr - Very heavy rain
           > 50.0 mm/hour - Extreme rain

           Roughly translated to the corresponding Inch rates
           < 0.001
           0.001 to 0.05
           0.05 to 0.20
           0.20 to 0.60
           0.60 to 2.0
           > 2.0
        */
        createRainRateSections = function (metric) {
            var section;
            if (metric) {
                section = [ steelseries.Section(0, 0.25, 'rgba(0, 140, 0, 0.5)'),
                            steelseries.Section(0.25, 1, 'rgba(80, 192, 80, 0.5)'),
                            steelseries.Section(1, 4, 'rgba(150, 203, 150, 0.5)'),
                            steelseries.Section(4, 16, 'rgba(212, 203, 109, 0.5)'),
                            steelseries.Section(16, 50, 'rgba(225, 155, 105, 0.5)'),
                            steelseries.Section(50, 1000, 'rgba(245, 86, 59, 0.5)')];
            } else {
                section = [ steelseries.Section(0, 0.05, 'rgba(0, 140, 0, 0.5)'),
                            steelseries.Section(0.05, 0.1, 'rgba(80, 192, 80, 0.5)'),
                            steelseries.Section(0.1, 0.15, 'rgba(150, 203, 150, 0.5)'),
                            steelseries.Section(0.15, 0.6, 'rgba(212, 203, 109, 0.5)'),
                            steelseries.Section(0.6, 2, 'rgba(225, 155, 105, 0.5)'),
                            steelseries.Section(2, 100, 'rgba(245, 86, 59, 0.5)')];
            }
            return section;
        },

        //
        // createRainFallSections()returns an array of section highlights for total rainfall in mm or inches
        //
        createRainfallSections = function (metric) {
            var section;
            if (metric) {
                section = [ steelseries.Section(0, 5, 'rgba(0, 250, 0, 1)'),
                            steelseries.Section(5, 10, 'rgba(0, 250, 117, 1)'),
                            steelseries.Section(10, 25, 'rgba(218, 246, 0, 1)'),
                            steelseries.Section(25, 40, 'rgba(250, 186, 0, 1)'),
                            steelseries.Section(40, 50, 'rgba(250, 95, 0, 1)'),
                            steelseries.Section(50, 65, 'rgba(250, 0, 0, 1)'),
                            steelseries.Section(65, 75, 'rgba(250, 6, 80, 1)'),
                            steelseries.Section(75, 100, 'rgba(205, 18, 158, 1)'),
                            steelseries.Section(100, 125, 'rgba(0, 0, 250, 1)'),
                            steelseries.Section(125, 500, 'rgba(0, 219, 212, 1)')];
            } else {
                section = [ steelseries.Section(0, 0.2, 'rgba(0, 250, 0, 1)'),
                            steelseries.Section(0.2, 0.5, 'rgba(0, 250, 117, 1)'),
                            steelseries.Section(0.5, 1, 'rgba(218, 246, 0, 1)'),
                            steelseries.Section(1, 1.5, 'rgba(250, 186, 0, 1)'),
                            steelseries.Section(1.5, 2, 'rgba(250, 95, 0, 1)'),
                            steelseries.Section(2, 2.5, 'rgba(250, 0, 0, 1)'),
                            steelseries.Section(2.5, 3, 'rgba(250, 6, 80, 1)'),
                            steelseries.Section(3, 4, 'rgba(205, 18, 158, 1)'),
                            steelseries.Section(4, 5, 'rgba(0, 0, 250, 1)'),
                            steelseries.Section(5, 20, 'rgba(0, 219, 212, 1)')];
            }
            return section;
        },

        //
        // createRainfallGradient() returns an array of SS colours for continuous gradient colouring of the total rainfall LED gauge
        //
        createRainfallGradient = function (metric) {
            var grad = new steelseries.gradientWrapper(
                0,
                (metric ? 100 : 4),
                [0, 0.1, 0.62, 1],
                [new steelseries.rgbaColor(15,  148,  0, 1),
                 new steelseries.rgbaColor(213, 213,  0, 1),
                 new steelseries.rgbaColor(213,   0, 25, 1),
                 new steelseries.rgbaColor(250,   0,  0, 1)]
            );
            return grad;
        },

        //
        // createClousBaseSections() returns an array of section highlights for the Cloud Base gauge
        //
        createCloudBaseSections = function (metric) {
            var section;
            if (metric) {
                section = [ steelseries.Section(0, 150, 'rgba(245, 86, 59, 0.5)'),
                            steelseries.Section(150, 300, 'rgba(225, 155, 105, 0.5)'),
                            steelseries.Section(300, 750, 'rgba(212, 203, 109, 0.5)'),
                            steelseries.Section(750, 1000, 'rgba(150, 203, 150, 0.5)'),
                            steelseries.Section(1000, 1500, 'rgba(80, 192, 80, 0.5)'),
                            steelseries.Section(1500, 2500, 'rgba(0, 140, 0, 0.5)'),
                            steelseries.Section(2500, 5500, 'rgba(19, 103, 186, 0.5)')];
            } else {
                section = [ steelseries.Section(0, 500, 'rgba(245, 86, 59, 0.5)'),
                            steelseries.Section(500, 1000, 'rgba(225, 155, 105, 0.5)'),
                            steelseries.Section(1000, 2500, 'rgba(212, 203, 109, 0.5)'),
                            steelseries.Section(2500, 3500, 'rgba(150, 203, 150, 0.5)'),
                            steelseries.Section(3500, 5500, 'rgba(80, 192, 80, 0.5)'),
                            steelseries.Section(5500, 8500, 'rgba(0, 140, 0, 0.5)'),
                            steelseries.Section(8500, 18000, 'rgba(19, 103, 186, 0.5)')];
            }
            return section;
        },

        //
        //--------------- Helper functions ------------------
        //

        //
        // getord() converts a value in degrees (0-360) into a localised compass point (N, ENE, NE, etc)
        //
        getord = function (deg) {
            if (deg === 0) {
                // Special case, 0=No wind, 360=North
                return strings.calm;
            } else {
                return (strings.coords[Math.floor((deg + 11.25) / 22.5) % 16]);
            }
        },

        //
        // getUrlParam() extracts the named parameter from the current page URL
        //
        getUrlParam = function (paramName) {
            var regexS, regex, results;
            paramName = paramName.replace(/(\[|\])/g, '\\$1');
            regexS = '[\\?&]' + paramName + '=([^&#]*)';
            regex = new RegExp(regexS);
            results = regex.exec(window.location.href);
            if (results === null) {
                return '';
            } else {
                return results[1];
            }
        },

        //
        // extractDecimal() returns a decimal number from a string, the decimal point can be either a dot or a comma
        // it ignores any text such as pre/appended units
        //
        extractDecimal = function (str) {
            try {
                return (/[\-+]?[0-9]+\.?[0-9]*/).exec(str.replace(',', '.'))[0];
            } catch (e) {
                return -9999;
            }
        },

        //
        // extractInteger() returns an integer from a string
        // it ignores any text such as pre/appended units
        //
        extractInteger = function (str) {
            try {
                return (/[\-+]?[0-9]+/).exec(str)[0];
            } catch (e) {
                return -9999;
            }
        },

        //
        // tempTrend() converts a temperature trend value into a localised string, or +1, 0, -1 depending on the value of bTxt
        //
        tempTrend = function (trend, units, bTxt) {
            // Scale is over 3 hours, in Celsius
            var val = trend * 3 * (units[1] === 'C' ? 1 : (5 / 9)),
                ret;
            if (trend === -9999) {
                ret = (bTxt ? '--' : trend);
            } else if (val > 5) {
                ret = (bTxt ? strings.RisingVeryRapidly : 1);
            } else if (val > 3) {
                ret = (bTxt ? strings.RisingQuickly : 1);
            } else if (val > 1) {
                ret = (bTxt ? strings.Rising : 1);
            } else if (val > 0.5) {
                ret = (bTxt ? strings.RisingSlowly : 1);
            } else if (val >= -0.5) {
                ret = (bTxt ? strings.Steady : 0);
            } else if (val >= -1) {
                ret = (bTxt ? strings.FallingSlowly : -1);
            } else if (val >= -3) {
                ret = (bTxt ? strings.Falling : -1);
            } else if (val >= -5) {
                ret = (bTxt ? strings.FallingQuickly : -1);
            } else {
                ret = (bTxt ? strings.FallingVeryRapidly : -1);
            }
            return ret;
        },

        //
        // baroTrend() converts a pressure trend value into a localised string, or +1, 0, -1 depending on the value of bTxt
        //
        baroTrend = function (trend, units, bTxt) {
            var val = trend * 3,
                ret;
            // The terms below are the UK Met Office terms for a 3 hour change in hPa
            // trend is supplied as an hourly change, so multiply by 3
            if (units === 'inHg') {
                val *= 33.8639;
            } else if (units === 'kPa') {
                val *= 10;
                // assume everything else is hPa or mb, could be dangerous!
            }
            if (trend === -9999) {
                ret = (bTxt ? '--' : trend);
            } else if (val > 6.0) {
                ret = (bTxt ? strings.RisingVeryRapidly : 1);
            } else if (val > 3.5) {
                ret = (bTxt ? strings.RisingQuickly : 1);
            } else if (val > 1.5) {
                ret = (bTxt ? strings.Rising : 1);
            } else if (val > 0.1) {
                ret = (bTxt ? strings.RisingSlowly : 1);
            } else if (val >= -0.1) {
                ret = (bTxt ? strings.Steady : 0);
            } else if (val >= -1.5) {
                ret = (bTxt ? strings.FallingSlowly : -1);
            } else if (val >= -3.5) {
                ret = (bTxt ? strings.Falling : -1);
            } else if (val >= -6.0) {
                ret = (bTxt ? strings.FallingQuickly : -1);
            } else {
                ret = (bTxt ? strings.FallingVeryRapidly : -1);
            }
            return ret;
        },

        //
        // getMinTemp() returns the lowest temperature today for gauge scaling
        //
        getMinTemp = function () {
            return Math.min(
                extractDecimal(data.tempTL),
                extractDecimal(data.dewpointTL),
                extractDecimal(data.apptempTL),
                extractDecimal(data.wchillTL));
        },

        //
        // getMaxTemp() returns the highest temperature today for gauge scaling
        //
        getMaxTemp = function () {
            return Math.max(
                extractDecimal(data.tempTH),
                extractDecimal(data.apptempTH),
                extractDecimal(data.heatindexTH),
                extractDecimal(data.humidex));
        },

        // Celsius to Fahrenheit
        c2f = function (val) {
            return (extractDecimal(val) * 9 / 5 + 32).toFixed(1);
        },
        // Fahrenheit to Celsius
        f2c = function (val) {
            return ((extractDecimal(val) - 32) * 5 / 9).toFixed(1);
        },
        // mph to ms
        mph2ms = function (val) {
            return (extractDecimal(val) * 0.447).toFixed(1);
        },
        // knots to ms
        kts2ms = function (val) {
            return (extractDecimal(val) * 0.515).toFixed(1);
        },
        // kph to ms
        kmh2ms = function (val) {
            return (extractDecimal(val) * 0.2778).toFixed(1);
        },
        // ms to kts
        ms2kts = function (val) {
            return (extractDecimal(val) * 1.9426).toFixed(1);
        },
        // ms to mph
        ms2mph = function (val) {
            return (extractDecimal(val) * 2.237).toFixed(1);
        },
        // ms to kph
        ms2kmh = function (val) {
            return (extractDecimal(val) * 3.6).toFixed(1);
        },
        // mm to inches
        mm2in = function (val) {
            return (extractDecimal(val) / 25.4).toFixed(2);
        },
        // inches to mm
        in2mm = function (val) {
            return (extractDecimal(val) * 25.4).toFixed(1);
        },
        // miles to km
        miles2km = function (val) {
            return (extractDecimal(val) * 1.609344).toFixed(1);
        },
        // nautical miles to km
        nmiles2km = function (val) {
            return (extractDecimal(val) * 1.85200).toFixed(1);
        },
        // km to miles
        km2miles = function (val) {
            return (extractDecimal(val) / 1.609344).toFixed(1);
        },
        // km to nautical miles
        km2nmiles = function (val) {
            return (extractDecimal(val) / 1.85200).toFixed(1);
        },
        // hPa to inHg (@0°C)
        hpa2inhg = function (val, decimals) {
            return (extractDecimal(val) * 0.029528744).toFixed(decimals || 2);
        },
        // inHg to hPa (@0°C)
        inhg2hpa = function (val) {
            return (extractDecimal(val) / 0.029528744).toFixed(1);
        },
        // kPa to hPa
        kpa2hpa = function (val) {
            return (extractDecimal(val) * 10).toFixed(1);
        },
        // hPa to kPa
        hpa2kpa = function (val, decimals) {
            return (extractDecimal(val) / 10).toFixed(decimals || 2);
        },
        // m to ft
        m2ft = function (val) {
            return (val * 3.2808399).toFixed(0);
        },
        // ft to m
        ft2m = function (val) {
            return (val / 3.2808399).toFixed(0);
        },

        //
        // setCookie() writes the 'obj' in cookie 'name' for persistent storage
        //
        setCookie = function (name, obj) {
            var date = new Date(), expires;
            // cookies valid for 1 year
            date.setYear(date.getFullYear() + 1);
            expires = '; expires=' + date.toGMTString();
            document.cookie = name + '=' + encodeURIComponent(JSON.stringify(obj)) + expires;
        },

        //
        // getCookie() reads the value of cookie 'name' from persistent storage
        //
        getCookie = function (name) {
            var i, x, y, ret = null,
                arrCookies = document.cookie.split(';');

            for (i = arrCookies.length; i--;) {
                x = arrCookies[i].split('=');
                if (x[0].trim() === name) {
                    try {
                        y = decodeURIComponent(x[1]);
                    } catch (e) {
                        y = x[1];
                    }
                    ret = JSON.parse(unescape(y));
                    break;
                }
            }
            return ret;
        },

        //
        // setRadioCheck() sets the desired value of the HTML radio buttons to be selected
        //
        setRadioCheck = function (obj, val) {
            $('input:radio[name="' + obj + '"]').filter('[value="' + val + '"]').prop('checked', true);
        },

        //
        // convTempData() converts all the temperature values using the supplied conversion function
        //
        convTempData = function (convFunc) {
            data.apptemp = convFunc(data.apptemp);
            data.apptempTH = convFunc(data.apptempTH);
            data.apptempTL = convFunc(data.apptempTL);
            data.dew = convFunc(data.dew);
            data.dewpointTH = convFunc(data.dewpointTH);
            data.dewpointTL = convFunc(data.dewpointTL);
            data.heatindex = convFunc(data.heatindex);
            data.heatindexTH = convFunc(data.heatindexTH);
            data.humidex = convFunc(data.humidex);
            data.intemp = convFunc(data.intemp);
            data.temp = convFunc(data.temp);
            data.tempTH = convFunc(data.tempTH);
            data.tempTL = convFunc(data.tempTL);
            data.wchill = convFunc(data.wchill);
            data.wchillTL = convFunc(data.wchillTL);
            if (convFunc === c2f) {
                data.temptrend = (+extractDecimal(data.temptrend) * 9 / 5).toFixed(1);
                data.tempunit = '°F';
            } else {
                data.temptrend = (+extractDecimal(data.temptrend) * 5 / 9).toFixed(1);
                data.tempunit = '°C';
            }
        },

        //
        // convRainData() converts all the rain data units using the supplied conversion function
        //
        convRainData = function (convFunc) {
            data.rfall = convFunc(data.rfall);
            data.rrate = convFunc(data.rrate);
            data.rrateTM = convFunc(data.rrateTM);
            data.hourlyrainTH = convFunc(data.hourlyrainTH);
            data.rainunit = convFunc === mm2in ? 'in' : 'mm';
        },

        //
        // convWindData() converts all the wind values using the supplied conversion function
        //
        convWindData = function (from, to) {
            var fromFunc1, toFunc1,
                fromFunc2, toFunc2,
                dummy = function (val) {
                    return val;
                };

            // convert to m/s & km
            switch (from) {
            case 'mph':
                fromFunc1 = mph2ms;
                fromFunc2 = miles2km;
                break;
            case 'kts':
                fromFunc1 = kts2ms;
                fromFunc2 = nmiles2km;
                break;
            case 'km/h':
                fromFunc1 = kmh2ms;
                fromFunc2 = dummy;
                break;
            case 'm/s':
            /* falls through */
            default:
                fromFunc1 = dummy;
                fromFunc2 = dummy;
            }
            // conversion function from km to required units
            switch (to) {
            case 'mph':
                toFunc1 = ms2mph;
                toFunc2 = km2miles;
                _displayUnits.windrun = 'miles';
                break;
            case 'kts':
                toFunc1 = ms2kts;
                toFunc2 = km2nmiles;
                _displayUnits.windrun = 'n.miles';
                break;
            case 'km/h':
                toFunc1 = ms2kmh;
                toFunc2 = dummy;
                _displayUnits.windrun = 'km';
                break;
            case 'm/s':
            /* falls through */
            default:
                toFunc1 = dummy;
                toFunc2 = dummy;
                _displayUnits.windrun = 'km';
            }
            // do the conversions
            data.wgust = toFunc1(fromFunc1(data.wgust));
            data.wgustTM = toFunc1(fromFunc1(data.wgustTM));
            data.windTM = toFunc1(fromFunc1(data.windTM));
            data.windrun = toFunc2(fromFunc2(data.windrun));
            data.wlatest = toFunc1(fromFunc1(data.wlatest));
            data.wspeed = toFunc1(fromFunc1(data.wspeed));
            data.windunit = to;
        },

        //
        // convBaroData() converts all the pressure values using the supplied conversion function
        //
        convBaroData = function (from, to) {
            var fromFunc, toFunc,
                dummy = function (val) {
                    return val;
                };

            // convert to hPa
            switch (from) {
            case 'hPa':
            /* falls through */
            case 'mb':
                fromFunc = dummy;
                break;
            case 'inHg':
                fromFunc = inhg2hpa;
                break;
            case 'kPa':
                fromFunc = kpa2hpa;
                break;
            }
            // convert to required units
            switch (to) {
            case 'hPa':
            /* falls through */
            case 'mb':
                toFunc = dummy;
                break;
            case 'inHg':
                toFunc = hpa2inhg;
                break;
            case 'kPa':
                toFunc = hpa2kpa;
                break;
            }

            data.press = toFunc(fromFunc(data.press));
            data.pressH = toFunc(fromFunc(data.pressH));
            data.pressL = toFunc(fromFunc(data.pressL));
            data.pressTH = toFunc(fromFunc(data.pressTH));
            data.pressTL = toFunc(fromFunc(data.pressTL));
            data.presstrendval = toFunc(fromFunc(data.presstrendval), 3);
            data.pressunit = to;
        },

        //
        // convCloudBaseData() converts all the cloud base data units using the supplied conversion function
        //
        convCloudBaseData = function (convFunc) {
            data.cloudbasevalue = convFunc(data.cloudbasevalue);
            data.cloudbaseunit = convFunc === m2ft ? 'ft' : 'm';
        },

        //
        // setUnits() Main data conversion routine, calls all the setXXXX() sub-routines
        //
        setUnits = function (radio) {
            var sel = radio.value;

            _userUnitsSet = true;

            switch (sel) {
            // == Temperature ==
            case 'C':
                _displayUnits.temp = sel;
                if (data.tempunit[1] !== sel) {
                    setTempUnits(true);
                    convTempData(f2c);
                    if (gaugeTemp) { gaugeTemp.update(); }
                    if (gaugeDew)  { gaugeDew.update(); }
                }
                break;
            case 'F':
                _displayUnits.temp = sel;
                if (data.tempunit[1] !== sel) {
                    setTempUnits(false);
                    convTempData(c2f);
                    if (gaugeTemp) { gaugeTemp.update(); }
                    if (gaugeDew)  { gaugeDew.update(); }
                }
                break;
            // == Rainfall ==
            case 'mm':
                _displayUnits.rain = sel;
                if (data.rainunit !== sel) {
                    setRainUnits(true);
                    convRainData(in2mm);
                    if (gaugeRain)  { gaugeRain.update(); }
                    if (gaugeRRate) { gaugeRRate.update(); }
                }
                break;
            case 'in':
                _displayUnits.rain = sel;
                if (data.rainunit !== sel) {
                    setRainUnits(false);
                    convRainData(mm2in);
                    if (gaugeRain)  { gaugeRain.update(); }
                    if (gaugeRRate) { gaugeRRate.update(); }
                }
                break;
            // == Pressure ==
            case 'hPa':
            /* falls through */
            case 'inHg':
            /* falls through */
            case 'mb':
            /* falls through */
            case 'kPa':
                _displayUnits.press = sel;
                if (data.pressunit !== sel) {
                    convBaroData(data.pressunit, sel);
                    setBaroUnits(sel);
                    if (gaugeBaro) { gaugeBaro.update(); }
                }
                break;
            // == Wind speed ==
            case 'mph':
            /* falls through */
            case 'kts':
            /* falls through */
            case 'm/s':
            /* falls through */
            case 'km/h':
                _displayUnits.wind = sel;
                if (data.windunit !== sel) {
                    convWindData(data.windunit, sel);
                    setWindUnits(sel);
                    if (gaugeWind) { gaugeWind.update(); }
                    if (gaugeDir)  { gaugeDir.update(); }
                    if (gaugeRose) { gaugeRose.update(); }
                }
                break;
            // == CloudBase ==
            case 'm':
                _displayUnits.cloud = sel;
                if (data.cloudbaseunit !== sel) {
                    setCloudBaseUnits(true);
                    convCloudBaseData(ft2m);
                    if (gaugeCloud) { gaugeCloud.update(); }
                }
                break;
            case 'ft':
                _displayUnits.cloud = sel;
                if (data.cloudbaseunit !== sel) {
                    setCloudBaseUnits(false);
                    convCloudBaseData(m2ft);
                    if (gaugeCloud) { gaugeCloud.update(); }
                }
                break;
            }
            if (config.useCookies) {
                setCookie('units', _displayUnits);
            }
        },

        setTempUnits = function (celsius) {
            if (celsius) {
                data.tempunit = '°C';
                if (gaugeTemp) {
                    gaugeTemp.data.sections = createTempSections(true);
                    gaugeTemp.data.minValue = gaugeGlobals.tempScaleDefMinC;
                    gaugeTemp.data.maxValue = gaugeGlobals.tempScaleDefMaxC;
                }
                if (gaugeDew) {
                    gaugeDew.data.sections = createTempSections(true);
                    gaugeDew.data.minValue = gaugeGlobals.tempScaleDefMinC;
                    gaugeDew.data.maxValue = gaugeGlobals.tempScaleDefMaxC;
                }
            } else {
                data.tempunit = '°F';
                if (gaugeTemp) {
                    gaugeTemp.data.sections = createTempSections(false);
                    gaugeTemp.data.minValue = gaugeGlobals.tempScaleDefMinF;
                    gaugeTemp.data.maxValue = gaugeGlobals.tempScaleDefMaxF;
                }
                if (gaugeDew) {
                    gaugeDew.data.sections = createTempSections(false);
                    gaugeDew.data.minValue = gaugeGlobals.tempScaleDefMinF;
                    gaugeDew.data.maxValue = gaugeGlobals.tempScaleDefMaxF;
                }
            }
            if (gaugeTemp) {
                gaugeTemp.gauge.setUnitString(data.tempunit);
                gaugeTemp.gauge.setSection(gaugeTemp.data.sections);
            }
            if (gaugeDew) {
                gaugeDew.gauge.setUnitString(data.tempunit);
                gaugeDew.gauge.setSection(gaugeTemp.data.sections);
            }
        },

        setRainUnits = function (mm) {
            if (mm) {
                data.rainunit = 'mm';
                if (gaugeRain) {
                    gaugeRain.data.lcdDecimals = 1;
                    gaugeRain.data.scaleDecimals = 1;
                    gaugeRain.data.labelNumberFormat = gaugeGlobals.labelFormat;
                    gaugeRain.data.sections = (gaugeGlobals.rainUseSectionColours ? createRainfallSections(true) : []);
                    gaugeRain.data.maxValue = gaugeGlobals.rainScaleDefmm;
                    gaugeRain.data.grad = (gaugeGlobals.rainUseGradientColours ? createRainfallGradient(true) : null);
                }
                if (gaugeRRate) {
                    gaugeRRate.data.lcdDecimals = 1;
                    gaugeRRate.data.scaleDecimals = 0;
                    gaugeRRate.data.labelNumberFormat = gaugeGlobals.labelFormat;
                    gaugeRRate.data.sections = createRainRateSections(true);
                    gaugeRRate.data.maxValue = gaugeGlobals.rainRateScaleDefmm;
                }
            } else {
                data.rainunit = 'in';
                if (gaugeRain) {
                    gaugeRain.data.lcdDecimals = 2;
                    gaugeRain.data.scaleDecimals = gaugeGlobals.rainScaleDefIn < 1 ? 2 : 1;
                    gaugeRain.data.labelNumberFormat = steelseries.LabelNumberFormat.FRACTIONAL;
                    gaugeRain.data.sections = (gaugeGlobals.rainUseSectionColours ? createRainfallSections(false) : []);
                    gaugeRain.data.maxValue = gaugeGlobals.rainScaleDefIn;
                    gaugeRain.data.grad = (gaugeGlobals.rainUseGradientColours ? createRainfallGradient(false) : null);
                }
                if (gaugeRRate) {
                    gaugeRRate.data.lcdDecimals = 2;
                    gaugeRRate.data.scaleDecimals = gaugeGlobals.rainRateScaleDefIn < 1 ? 2 : 1;
                    gaugeRRate.data.labelNumberFormat = steelseries.LabelNumberFormat.FRACTIONAL;
                    gaugeRRate.data.sections = createRainRateSections(false);
                    gaugeRRate.data.maxValue = gaugeGlobals.rainRateScaleDefIn;
                }
            }
            if (gaugeRain) {
                gaugeRain.data.value = 0;
                gaugeRain.gauge.setUnitString(data.rainunit);
                gaugeRain.gauge.setSection(gaugeRain.data.sections);
                gaugeRain.gauge.setGradient(gaugeRain.data.grad);
                gaugeRain.gauge.setFractionalScaleDecimals(gaugeRain.data.scaleDecimals);
                gaugeRain.gauge.setLabelNumberFormat(gaugeRain.data.labelNumberFormat);
                gaugeRain.gauge.setLcdDecimals(gaugeRain.data.lcdDecimals);
            }
            if (gaugeRRate) {
                gaugeRRate.data.value = 0;
                gaugeRRate.gauge.setUnitString(data.rainunit + '/h');
                gaugeRRate.gauge.setSection(gaugeRRate.data.sections);
                gaugeRRate.gauge.setFractionalScaleDecimals(gaugeRRate.data.scaleDecimals);
                gaugeRRate.gauge.setLabelNumberFormat(gaugeRRate.data.labelNumberFormat);
                gaugeRRate.gauge.setLcdDecimals(gaugeRRate.data.lcdDecimals);
            }
        },

        setWindUnits = function (to) {
            var maxVal;
            if (!gaugeWind) {return;}

            // conversion function to required units
            switch (to) {
            case 'mph':
                maxVal = gaugeGlobals.windScaleDefMaxMph;
                break;
            case 'kts':
                maxVal = gaugeGlobals.windScaleDefMaxKts;
                break;
            case 'km/h':
                maxVal = gaugeGlobals.windScaleDefMaxKmh;
                break;
            case 'm/s':
                maxVal = gaugeGlobals.windScaleDefMaxMs;
            }
            // set the gauges
            data.windunit = to;
            gaugeWind.data.maxValue = maxVal;
            gaugeWind.gauge.setUnitString(data.windunit);
            gaugeWind.gauge.setValue(0);

        },

        setBaroUnits = function (to) {
            var minVal, maxVal;

            if (!gaugeBaro) {return;}

            // set to the required units
            switch (to) {
            case 'hPa':
            /* falls through */
            case 'mb':
                minVal = gaugeGlobals.baroScaleDefMinhPa;
                maxVal = gaugeGlobals.baroScaleDefMaxhPa;
                gaugeBaro.data.lcdDecimals = 1;
                gaugeBaro.data.scaleDecimals = 0;
                gaugeBaro.data.labelNumberFormat = gaugeGlobals.labelFormat;
                break;
            case 'inHg':
                minVal = gaugeGlobals.baroScaleDefMininHg;
                maxVal = gaugeGlobals.baroScaleDefMaxinHg;
                gaugeBaro.data.lcdDecimals = 2;
                gaugeBaro.data.scaleDecimals = 1;
                gaugeBaro.data.labelNumberFormat = steelseries.LabelNumberFormat.FRACTIONAL;
                break;
            case 'kPa':
                minVal = gaugeGlobals.baroScaleDefMinkPa;
                maxVal = gaugeGlobals.baroScaleDefMaxkPa;
                gaugeBaro.data.lcdDecimals = 2;
                gaugeBaro.data.scaleDecimals = 1;
                gaugeBaro.data.labelNumberFormat = steelseries.LabelNumberFormat.FRACTIONAL;
                break;
            }

            data.pressunit = to;
            gaugeBaro.gauge.setUnitString(to);
            gaugeBaro.gauge.setLcdDecimals(gaugeBaro.data.lcdDecimals);
            gaugeBaro.gauge.setFractionalScaleDecimals(gaugeBaro.data.scaleDecimals);
            gaugeBaro.gauge.setLabelNumberFormat(gaugeBaro.data.labelNumberFormat);
            gaugeBaro.data.minValue = minVal;
            gaugeBaro.data.maxValue = maxVal;
            gaugeBaro.data.value = gaugeBaro.data.minValue;
        },

        setCloudBaseUnits = function (m) {
            if (!gaugeCloud) {return;}

            if (m) {
                gaugeCloud.data.sections = createCloudBaseSections(true);
                gaugeCloud.data.maxValue = gaugeGlobals.cloudScaleDefMaxm;

            } else {
                gaugeCloud.data.sections = createCloudBaseSections(false);
                gaugeCloud.data.maxValue = gaugeGlobals.cloudScaleDefMaxft;
            }
            gaugeCloud.data.value = 0;
            gaugeCloud.gauge.setUnitString(m ? strings.metres : strings.feet);
            gaugeCloud.gauge.setSection(gaugeCloud.data.sections);
        },

        //
        // setLang() switches the HTML page language set, called by changeLang() in language.js
        //
        setLang = function (newLang) {
            // reset to the new language
            strings = newLang;

            // temperature
            if (gaugeTemp) {
                if ($('#rad_temp1').is(':checked')) {
                    gaugeTemp.data.title = strings.temp_title_out;
                } else {
                    gaugeTemp.data.title = strings.temp_title_in;
                }
                gaugeTemp.gauge.setTitleString(gaugeTemp.data.title);
                if (data.ver) { gaugeTemp.update(); }
            }
            if (gaugeDew) {
                switch ($('input[name="rad_dew"]:checked').val()) {
                case 'dew':
                    gaugeDew.data.title = strings.dew_title;
                    break;
                case 'app':
                    gaugeDew.data.title = strings.apptemp_title;
                    break;
                case 'wnd':
                    gaugeDew.data.title = strings.chill_title;
                    break;
                case 'hea':
                    gaugeDew.data.title = strings.heat_title;
                    break;
                case 'hum':
                    gaugeDew.data.title = strings.humdx_title;
                    break;
                }
                gaugeDew.gauge.setTitleString(gaugeDew.data.title);
                if (data.ver) { gaugeDew.update(); }
            }
            // rain
            if (gaugeRain) {
                gaugeRain.data.title = strings.rain_title;
                gaugeRain.gauge.setTitleString(gaugeRain.data.title);
                if (data.ver) { gaugeRain.update(); }
            }
            // rrate
            if (gaugeRRate) {
                gaugeRRate.data.title = strings.rrate_title;
                gaugeRRate.gauge.setTitleString(gaugeRRate.data.title);
                if (data.ver) { gaugeRRate.update(); }
             }
            // humidity
            if (gaugeHum) {
                if ($('#rad_hum1').is(':checked')) {
                    gaugeHum.data.title = strings.hum_title_out;
                } else {
                    gaugeHum.data.title = strings.hum_title_in;
                }
                gaugeHum.gauge.setTitleString(gaugeHum.data.title);
                if (data.ver) { gaugeHum.update(); }
            }
            // barometer
            if (gaugeBaro) {
                gaugeBaro.data.title = strings.baro_title;
                gaugeBaro.gauge.setTitleString(gaugeBaro.data.title);
                if (data.ver) { gaugeBaro.update(); }
            }
            // wind
            if (gaugeWind) {
                gaugeWind.data.title = strings.wind_title;
                gaugeWind.gauge.setTitleString(gaugeWind.data.title);
                if (data.ver) { gaugeWind.update(); }
            }
            if (gaugeDir) {
                gaugeDir.gauge.setPointSymbols(strings.compass);
                gaugeDir.data.titles = [strings.latest_web, strings.tenminavg_web];
                gaugeDir.gauge.setLcdTitleStrings(gaugeDir.data.titles);
                if (data.ver) { gaugeDir.update(); }
            }
            if (gaugeUV) {
                gaugeUV.gauge.setTitleString(strings.uv_title);
                if (data.ver) { gaugeUV.update(); }
            }
            if (gaugeSolar) {
                gaugeSolar.gauge.setTitleString(strings.solar_title);
                if (data.ver) { gaugeSolar.update(); }
            }
            if (gaugeRose) {
                gaugeRose.setTitle(strings.windrose);
                gaugeRose.setCompassString(strings.compass);
                if (data.ver) { gaugeRose.update(); }
            }
            if (gaugeCloud) {
                // Cloudbase
                gaugeCloud.data.units = data.cloudunit === 'm' ? strings.metres : strings.feet;
                gaugeCloud.gauge.setTitleString(strings.cloudbase_title);
                gaugeCloud.gauge.setUnitString(gaugeCloud.data.units);
                if (data.ver) { gaugeCloud.update(); }
            }
        },

        getWindrunUnits = function (spdUnits) {
            var retVal;
            switch (spdUnits) {
            case 'mph':
                retVal = 'miles';
                break;
            case 'kts':
                retVal = 'n.miles';
                break;
            case 'km/h':
            /* falls through */
            case 'm/s':
            /* falls through */
            default:
                retVal = 'km';
                break;
            }
            return retVal;
        },

        calcCloudbase = function (temp, tempunit, dew, cloudbaseunit) {
            var sprd = temp - dew;
            var cb = sprd * (tempunit[1] === 'C' ? 400 : 227.3); // cloud base in feet
            if (cloudbaseunit === 'm') {
                cb = ft2m(cb);
            }
            return cb;
        },

        gaugeShadow = function (size) {
            var offset = Math.floor(size * 0.015);
            return {
                    'box-shadow': offset + 'px ' + offset + 'px ' + offset + 'px ' + gaugeGlobals.shadowColour,
                    'border-radius': Math.floor(size / 2) + 'px'
                };
        },

        gradient = function (startCol, endCol, fraction) {
            var redOrigin, grnOrigin, bluOrigin,
                gradientSizeRed, gradientSizeGrn, gradientSizeBlu;

            redOrigin = parseInt(startCol.substr(0, 2), 16);
            grnOrigin = parseInt(startCol.substr(2, 2), 16);
            bluOrigin = parseInt(startCol.substr(4, 2), 16);

            gradientSizeRed = parseInt(endCol.substr(0, 2), 16)  - redOrigin; //Graduation Size Red
            gradientSizeGrn = parseInt(endCol.substr(2, 2), 16)  - grnOrigin;
            gradientSizeBlu = parseInt(endCol.substr(4, 2), 16)  - bluOrigin;

            return  (redOrigin + (gradientSizeRed * fraction)).toFixed(0) + ',' +
                    (grnOrigin + (gradientSizeGrn * fraction)).toFixed(0) + ',' +
                    (bluOrigin + (gradientSizeBlu * fraction)).toFixed(0);
        };


    // test for canvas support before we do anything else, especially reference steelseries which will cause the script to abort!
    if (!document.createElement('canvas').getContext) {
        // failed, no canvas support detected
        $('body').html(strings.canvasnosupport);
        setTimeout(function () {
            window.location = config.oldGauges;
        }, 3000);
        return false;
    } else {
        //
        // Called when the document object has loaded
        // This starts the whole script.
        //
        $(document).ready(function () {
            // Kick it all off - false for web page, true for dashboard
            init(config.dashboardMode);
        });
    }

    return {
        setLang:    setLang,
        setUnits:   setUnits,
        processData: processData
    };
}()),

// ===============================================================================================================================
// ===============================================================================================================================
// ===============================================================================================================================

/*! Image w/ description tooltip v2.0  -  For FF1+ IE6+ Opr8+
* Created: April 23rd, 2010. This notice must stay intact for usage
* Author: Dynamic Drive at http://www.dynamicdrive.com/
* Visit http://www.dynamicdrive.com/ for full source code
* Modified: M Crossley June 2011, January 2012
* v2.-
*/

ddimgtooltip = {
    tiparray : (function () {
        var style = {background: '#FFFFFF', color: 'black', border: '2px ridge darkblue'},
            i = 12,  // set to number of tooltips required
            tooltips = [];
        for (;i--;) {
            tooltips[i] = [null, ' ', style];
        }
        return tooltips;
    }()),

    tooltipoffsets : [20, -30], //additional x and y offset from mouse cursor for tooltips

    tipDelay : 1000,

    _delayTimer : 0,

    tipprefix : 'imgtip', //tooltip DOM ID prefixes

    createtip : function ($, tipid, tipinfo) {
        if ($('#' + tipid).length === 0) { //if this tooltip doesn't exist yet
            return $('<div id="' + tipid + '" class="ddimgtooltip" />')
                        .html(
                            ((tipinfo[1]) ? '<div class="tipinfo" id="' + tipid + '_txt">' + tipinfo[1] + '</div>' : '') +
                            (tipinfo[0] !== null ? '<div style="text-align:center"><img class="tipimg" id="' + tipid + '_img" src="' + tipinfo[0] + '" /></div>' : '')
                        )
                        .css(tipinfo[2] || {})
                        .appendTo(document.body);
        }
        return null;
    },

    positiontooltip : function ($, $tooltip, e) {
        var x = e.pageX + this.tooltipoffsets[0],
            y = e.pageY + this.tooltipoffsets[1],
            tipw = $tooltip.outerWidth(),
            tiph = $tooltip.outerHeight(),
            wHght = $(window).height(),
            dTop = $(document).scrollTop();

        x = (x + tipw > $(document).scrollLeft() + $(window).width()) ? x - tipw - (ddimgtooltip.tooltipoffsets[0] * 2) : x;
        y = (y + tiph > dTop + wHght) ? dTop + wHght - tiph - 10 : y;
        $tooltip.css({left: x, top: y});
    },

    delaybox : function ($, $tooltip) {
        if (this.showTips) {
            ddimgtooltip._delayTimer = setTimeout(function () {
                    ddimgtooltip.showbox($tooltip.selector);
                }, ddimgtooltip.tipDelay);
        }
    },

    showbox : function (tooltip) {
        if (this.showTips) {
            //$(tooltip).show();
            $(tooltip).fadeIn();
        }
    },

    hidebox : function ($, $tooltip) {
        clearTimeout(ddimgtooltip._delayTimer);
        //$tooltip.hide();
        $tooltip.fadeOut();
    },

    showTips : false,

    init : function (targetselector) {
        $(document).ready(function ($) {
            var tiparray = ddimgtooltip.tiparray,
                $targets = $(targetselector);

            if ($targets.length === 0) {
                return;
            }
            $targets.each(function () {
                var $target = $(this),
                    tipsuffix, tipid,
                    $tooltip;
                $target.attr('id').match(/_(\d+)/); //match d of attribute id='tip_d'
                tipsuffix = parseInt(RegExp.$1, 10); //get d as integer
                tipid = this._tipid = ddimgtooltip.tipprefix + tipsuffix; //construct this tip's ID value and remember it
                $tooltip = ddimgtooltip.createtip($, tipid, tiparray[tipsuffix]);

                $target.mouseenter(function (e) {
                    var $tooltip = $('#' + this._tipid);
                    //ddimgtooltip.showbox($, $tooltip, e);
                    ddimgtooltip.delaybox($, $tooltip, e);
                });
                $target.mouseleave(function () {
                    var $tooltip = $('#' + this._tipid);
                    ddimgtooltip.hidebox($, $tooltip);
                });
                $target.mousemove(function (e) {
                    var $tooltip = $('#' + this._tipid);
                    ddimgtooltip.positiontooltip($, $tooltip, e);
                });
                if ($tooltip) { //add mouseenter to this tooltip (only if event hasn't already been added)
                    $tooltip.mouseenter(function () {
                        ddimgtooltip.hidebox($, $(this));
                    });
                }
            });
        }); //end dom ready
    }
};

String.prototype.trim = String.prototype.trim || function trim() {
    return this.replace(/^\s+|\s+$/g, '');
};

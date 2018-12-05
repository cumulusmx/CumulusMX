// Configuration section
var useWebSockets = true; // set to false to use Ajax updating
var updateInterval = 3;   // update interval in seconds, if Ajax updating is used
// End of configuration section

window.onload = function() {
    var lastUpdateTimer, keepAliveTimer, ws;
    var cp = ['N', 'NNE', 'NE', 'ENE', 'E', 'ESE', 'SE', 'SSE', 'S', 'SSW', 'SW', 'WSW', 'W', 'WNW', 'NW', 'NNW'];

    $('[id$=Table]').DataTable({
        'paging': false,
        'searching': false,
        'info': false,
        'ordering': false
    });

    function OpenWebSocket(wsport) {

        if ('WebSocket' in window) {
            // Open the web socket
            ws = new WebSocket('ws://' + location.hostname + ':' + wsport);
            ws.onopen = function() {
                // start the timer that checks for the last update
                lastUpdateTimer = setTimeout(updateTimeout, 60000);

                keepAliveTimer = setInterval(keepAlive, 60000);
            };
            ws.onmessage = function(evt) {
                onMessage(evt);
            };

            ws.onclose = function(evt) {
                onClose(evt);
            };

        } else {
            // The browser doesn't support WebSocket
            alert('WebSocket NOT supported by your Browser!');
        }
    }

    function updateTimeout() {
        // Change the icon on the last update to show that there has been no update for a while
        $('#LastUpdateIcon').attr('src', 'img/down.png');
    }

    function keepAlive() {
        // send a message to stop the server timing out the connection
        ws.send('Keep alive');
    }


    function onMessage(evt) {
        var data = JSON.parse(evt.data);
        
        updateDisplay(data);
    }
    
    function updateDisplay(data) {
        // restart the timer that checks for the last update
        window.clearTimeout(lastUpdateTimer);
        lastUpdateTimer = setTimeout(updateTimeout, 60000);

        // Get the keys from the object and set
        // the element with the same id to the value
        Object.keys(data).forEach(function(key) {
            var id = '#' + key;
            if ($(id).length) {
                $(id).text(data[key]);
            }
        });

        $('#BearingCP').html(cp[Math.floor(((parseInt(data.Bearing) + 11) / 22.5) % 16)]);
        $('#AvgbearingCP').html(cp[Math.floor(((parseInt(data.Avgbearing) + 11) / 22.5) % 16)]);

        $('.WindUnit').text(data.WindUnit);
        $('.PressUnit').text(data.PressUnit);
        $('.TempUnit').text(data.TempUnit);
        $('.RainUnit').text(data.RainUnit);

        var lastupdatetime = new Date();
        var hours = lastupdatetime.getHours();
        var minutes = lastupdatetime.getMinutes();
        var seconds = lastupdatetime.getSeconds();

        if (hours < 10) {
            hours = '0' + hours;
        }
        if (minutes < 10) {
            minutes = '0' + minutes;
        }
        if (seconds < 10) {
            seconds = '0' + seconds;
        }

        var time = hours + ':' + minutes + ':' + seconds;

        $('#lastupdatetime').text(time);
    }

    function onClose() {
        // websocket is closed.
        alert('Connection is closed...');
    }
    
    function doAjaxUpdate() {
        $.ajax({
            url: "api/data/currentdata",
            dataType: "json",
            success: function (data) {
                updateDisplay(data);
            }
        });
    }

    if (useWebSockets) {
        // Obtain the websockets port and open the connection
        $.ajax({
            url: 'api/settings/wsport.json',
            dataType: 'json',
            success: function (result) {
                OpenWebSocket(result.wsport);
            }
        });
    } else {
        // use Ajax
        doAjaxUpdate();
        
        // start the timer that checks for the last update
        lastUpdateTimer = setTimeout(updateTimeout, 60000);
        
        // start the timer for the display updates
        setInterval(doAjaxUpdate, updateInterval * 1000);
    }
};
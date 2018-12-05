$(document).ready(function () {
    $.ajax({url: "api/settings/version.json", dataType:"json", success: function (result) {
        $('#Version').text(result.Version);
        $('#Build').text(result.Build);            
    }});
    
    var tempTable = $('#TempTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/todayyest/temp.json'
    });

    var rainTable = $('#RainTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/todayyest/rain.json'
    });

    var windTable = $('#WindTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/todayyest/wind.json'
    });

    var humidityTable = $('#HumidityTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/todayyest/hum.json'
    });

    var pressureTable = $('#PressureTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/todayyest/pressure.json'
    });

    var solarTable = $('#SolarTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/todayyest/solar.json'
    });

    setInterval(function () {
        tempTable.ajax.url('../api/todayyest/temp.json').load();
        rainTable.ajax.url('../api/todayyest/rain.json').load();
        windTable.ajax.url('../api/todayyest/wind.json').load();
        humidityTable.ajax.url('../api/todayyest/hum.json').load();
        pressureTable.ajax.url('../api/todayyest/pressure.json').load();
        solarTable.ajax.url('../api/todayyest/solar.json').load();
    }, 10000);

});



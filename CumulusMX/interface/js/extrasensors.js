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
        "ajax": '../api/extra/temp.json'
    });

    var humTable = $('#HumTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/extra/hum.json'
    });

    var dewTable = $('#DewTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/extra/dew.json'
    });

    var soiltempTable = $('#SoilTempTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/extra/soiltemp.json'
    });

    var soilmoistureTable = $('#SoilMoistureTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/extra/soilmoisture.json'
    });

    var leafTable = $('#LeafTable').DataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "ajax": '../api/extra/leaf.json'
    });

    setInterval(function () {
        tempTable.ajax.url('../api/extra/temp.json').load();
        humTable.ajax.url('../api/extra/hum.json').load();
        dewTable.ajax.url('../api/extra/dew.json').load();
        soiltempTable.ajax.url('../api/extra/soiltemp.json').load();
        soilmoistureTable.ajax.url('../api/extra/soilmoisture.json').load();
        leafTable.ajax.url('../api/extra/leaf.json').load();
    }, 10000);

});



$(document).ready(function () {
    var tempTable=$('#temperature').dataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "columns": [{"width": "50%"}, {"width": "20%"}, null],
        "ajax": '../api/records/alltime/temperature.json'
    });

    var humTable=$('#humidity').dataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "columns": [{"width": "50%"}, {"width": "20%"}, null],
        "ajax": '../api/records/alltime/humidity.json'
    });

    var pressTable=$('#pressure').dataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "columns": [{"width": "50%"}, {"width": "20%"}, null],
        "ajax": '../api/records/alltime/pressure.json'
    });

    var windTable=$('#wind').dataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "columns": [{"width": "50%"}, {"width": "20%"}, null],
        "ajax": '../api/records/alltime/wind.json'
    });

    var rainTable=$('#rain').dataTable({
        "paging": false,
        "searching": false,
        "info": false,
        "ordering": false,
        "columns": [{"width": "50%"}, {"width": "20%"}, null],
        "ajax": '../api/records/alltime/rain.json'
    });
    
    $.ajax({url: "api/settings/version.json", dataType:"json", success: function (result) {
        $('#Version').text(result.Version);
        $('#Build').text(result.Build);            
    }});

    $(document).ready(function () {
        $('.btn').change(function () {
            
            var myRadio = $('input[name=options]');
            var checkedValue = myRadio.filter(':checked').val();
            
            var urlPrefix;
            if (checkedValue === 'alltime') {
                urlPrefix = "../api/records/alltime/";
            } else if (checkedValue === 'thismonth') {
                urlPrefix = "../api/records/thismonth/";
            } else if (checkedValue === 'thisyear') {
                urlPrefix = "../api/records/thisyear/";
            } else {
                urlPrefix = "../api/records/month/"+checkedValue+"/";
            }
            
            tempTable.api().ajax.url( urlPrefix+'temperature.json' ).load();
            humTable.api().ajax.url( urlPrefix+'humidity.json' ).load();
            pressTable.api().ajax.url( urlPrefix+'pressure.json' ).load();
            windTable.api().ajax.url( urlPrefix+'wind.json' ).load();
            rainTable.api().ajax.url( urlPrefix+'rain.json' ).load();
        });
    });
});
     
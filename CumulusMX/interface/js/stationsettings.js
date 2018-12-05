$(document).ready(function () {

    $("#form").alpaca({
        "dataSource": "../api/settings/stationdata.json",
        "optionsSource": "../api/settings/stationoptions.json",
        "schemaSource": "../api/settings/stationschema.json",
        "ui": "bootstrap",
        "postRender": function (form) {
            $("#save-button").click(function () {
                if (form.isValid(true)) {
                    var json = form.getValue();
                    $.ajax({
                        type: "POST",
                        url: "../api/setsettings/updatestationconfig.json",
                        data: {json: JSON.stringify(json)},
                        dataType: "text",
                        success: function (msg) {
                            alert("Settings updated");
                        },
                        error: function (error) {
                            alert("error " + error);
                        }

                    });
                }
            });
        }
    });

});

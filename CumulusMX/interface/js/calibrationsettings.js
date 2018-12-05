$(document).ready(function() {

    $("#form").alpaca({
        "dataSource": "../api/settings/calibrationdata.json",
        "optionsSource": "../api/settings/calibrationoptions.json",
        "schemaSource": "../api/settings/calibrationschema.json",
        "ui": "bootstrap",
        "postRender": function (form) {
            $("#save-button").click(function () {
                if (form.isValid(true)) {
                    var json = form.getValue();
                    $.ajax({
                        type: "POST",
                        url: "../api/setsettings/updatecalibrationconfig.json",
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
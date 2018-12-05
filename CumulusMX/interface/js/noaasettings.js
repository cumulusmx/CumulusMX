$(document).ready(function() {

    $("#form").alpaca({
        "dataSource": "../api/settings/noaadata.json",
        "optionsSource": "../api/settings/noaaoptions.json",
        "schemaSource": "../api/settings/noaaschema.json",
        "ui": "bootstrap",
        "postRender": function (form) {
            $("#save-button").click(function () {
                if (form.isValid(true)) {
                    var json = form.getValue();
                    $.ajax({
                        type: "POST",
                        url: "../api/setsettings/updatenoaaconfig.json",
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
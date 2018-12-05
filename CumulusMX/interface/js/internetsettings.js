$(document).ready(function() {

    $("#form").alpaca({
        "dataSource": "../api/settings/internetdata.json",
        "optionsSource": "../api/settings/internetoptions.json",
        "schemaSource": "../api/settings/internetschema.json",
        "ui": "bootstrap",
        "postRender": function (form) {
            $("#save-button").click(function () {
                if (form.isValid(true)) {
                    var json = form.getValue();
                    $.ajax({
                        type: "POST",
                        url: "../api/setsettings/updateinternetconfig.json",
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
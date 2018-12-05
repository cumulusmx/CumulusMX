$(document).ready(function () {

    $("#form").alpaca({
        "dataSource": "../api/settings/mysqldata.json",
        "optionsSource": "../api/settings/mysqloptions.json",
        "schemaSource": "../api/settings/mysqlschema.json",
        "ui": "bootstrap",
        "postRender": function (form) {
            $("#save-button").click(function () {
                if (form.isValid(true)) {
                    var json = form.getValue();
                    $.ajax({
                        type: "POST",
                        url: "../api/setsettings/updatemysqlconfig.json",
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

            $("#createmonthly").click(function () {
                $("#results").text("Attempting create...");
                $.ajax({
                    type: "POST",
                    url: "../api/setsettings/createmonthlysql.json",
                    success: function (msg) {
                        $("#results").text(msg.result);
                    },
                    error: function (xhr, textStatus, error) {
                        $("#results").text(textStatus);
                    }
                });
            });

            $("#createdayfile").click(function () {
                $("#results").text("Attempting create...");
                $.ajax({
                    type: "POST",
                    url: "../api/setsettings/createdayfilesql.json",
                    success: function (msg) {
                        $("#results").text(msg.result);
                    },
                    error: function (xhr, textStatus, error) {
                        $("#results").text(textStatus);
                    }
                });
            });

            $("#createrealtime").click(function () {
                $("#results").text("Attempting create...");
                $.ajax({
                    type: "POST",
                    url: "../api/setsettings/createrealtimesql.json",
                    success: function (msg) {
                        $("#results").text(msg.result);
                    },
                    error: function (xhr, textStatus, error) {
                        $("#results").text(textStatus);
                    }
                });
            });
        }
    });
});
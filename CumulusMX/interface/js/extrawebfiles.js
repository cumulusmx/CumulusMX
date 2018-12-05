/*
 * examples/full/javascript/demo.js
 * 
 * This file is part of EditableGrid.
 * http://editablegrid.net
 *
 * Copyright (c) 2011 Webismymind SPRL
 * Dual licensed under the MIT or GPL Version 2 licenses.
 * http://editablegrid.net/license
 */

// create our editable grid
var editableGrid = new EditableGrid("DemoGridFull", {
	enableSort: false, // true is the default, set it to false if you don't want sorting to be enabled
	pageSize: 10
});

// helper function to get path of a demo image
function image(relativePath) {
	return "js/images/" + relativePath;
}

// register the function that will handle model changes
editableGrid.modelChanged = function(rowIndex, columnIndex, oldValue, newValue) {

    $.ajax({

        url: "/api/setsettings/updateextrawebfiles.html",

        type: 'POST',

        dataType: "text",

        data: {
            id: editableGrid.getRowId(rowIndex),
            column: columnIndex,
            value: newValue
        },

        success: function (response) {
            // reset old value if failed
            if (response != "success") editableGrid.setValueAt(rowIndex, columnIndex, oldValue);
            // here you could also highlight the updated row to give the user some feedback
        },

        error: function(XMLHttpRequest, textStatus, exception) {
            alert(XMLHttpRequest.responseText);
        }
    });
};


// this function will initialize our editable grid
EditableGrid.prototype.initializeGrid = function() 
{
	with (this) {

		
		
		// update paginator whenever the table is rendered (after a sort, filter, page change, etc.)
		tableRendered = function() { this.updatePaginator(); };

		
		
		// render the grid (parameters will be ignored if we have attached to an existing HTML table)
		renderGrid("tablecontent", "testgrid", "tableid");
		
		
	}
};


EditableGrid.prototype.onloadJSON = function(url) 
{
	// register the function that will be called when the XML has been fully loaded
	this.tableLoaded = function() { 
		
		this.initializeGrid();
	};

	// load JSON URL
	this.loadJSON(url);
};


// function to render the paginator control
EditableGrid.prototype.updatePaginator = function()
{
	var paginator = $("#paginator").empty();
	var nbPages = this.getPageCount();

	// get interval
	var interval = this.getSlidingPageInterval(20);
	if (interval == null) return;
	
	// get pages in interval (with links except for the current page)
	var pages = this.getPagesInInterval(interval, function(pageIndex, isCurrent) {
		if (isCurrent) return "" + (pageIndex + 1);
		return $("<a>").css("cursor", "pointer").html(pageIndex + 1).click(function(event) { editableGrid.setPageIndex(parseInt($(this).html()) - 1); });
	});
		
	// "first" link
	var link = $("<a>").html("<img src='" + image("gofirst.png") + "'/>&nbsp;");
	if (!this.canGoBack()) link.css({ opacity : 0.4, filter: "alpha(opacity=40)" });
	else link.css("cursor", "pointer").click(function(event) { editableGrid.firstPage(); });
	paginator.append(link);

	// "prev" link
	link = $("<a>").html("<img src='" + image("prev.png") + "'/>&nbsp;");
	if (!this.canGoBack()) link.css({ opacity : 0.4, filter: "alpha(opacity=40)" });
	else link.css("cursor", "pointer").click(function(event) { editableGrid.prevPage(); });
	paginator.append(link);

	// pages
	for (p = 0; p < pages.length; p++) paginator.append(pages[p]).append(" | ");
	
	// "next" link
	link = $("<a>").html("<img src='" + image("next.png") + "'/>&nbsp;");
	if (!this.canGoForward()) link.css({ opacity : 0.4, filter: "alpha(opacity=40)" });
	else link.css("cursor", "pointer").click(function(event) { editableGrid.nextPage(); });
	paginator.append(link);

	// "last" link
	link = $("<a>").html("<img src='" + image("golast.png") + "'/>&nbsp;");
	if (!this.canGoForward()) link.css({ opacity : 0.4, filter: "alpha(opacity=40)" });
	else link.css("cursor", "pointer").click(function(event) { editableGrid.lastPage(); });
	paginator.append(link);
};
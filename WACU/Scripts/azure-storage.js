(function (AzureStorage, $) {
    var maxBlockSize = 0; // Real value iitialized in initializeVariables() method
    var blockIds = new Array();
    var blockIdPrefix = "block-";
    var blockIdLength = 10;
    var bytesUploaded = 0;
    var currentFilePointer = 0;
    var totalBytesRemaining = 0;
    var selectedFile = null;
    var submitUri = null;
    var assetId = null;

    // Initializes AzureStorage variables
    function initializeVariables() {
        blockIds = new Array();
        maxBlockSize = 1024 * 1024; // Each file will be split into 1024 KB chunks.
        bytesUploaded = 0;
        currentFilePointer = 0;
        totalBytesRemaining = 0;
    }

    // Initializes DOM elements
    function initializeDOMElements() {
        $("#preparing-file").show();
        $("#upload-button").hide();
        $("#upload-progress-holder").hide();
        $("#upload-progress").text("0.00");
    }

    // Reads the file and finds out the number of blocks it needs to be split into
    AzureStorage.handleFileSelect = function (evt) {
        clearErrorMessage();
        initializeDOMElements();
        initializeVariables();

        selectedFile = evt.target.files[0];
        setFileInfoData(selectedFile);

        var fileSize = selectedFile.size;
        if (fileSize < maxBlockSize) {
            maxBlockSize = fileSize;
        }
        totalBytesRemaining = fileSize;
        setWAMSUri(selectedFile.name);
    }

    var fileReader = new FileReader();

    // Reads file chunks or commits block list if all chunks have been uploaded already
    AzureStorage.uploadFileInBlocks = function () {
        $("#upload-progress-holder").show();
        if (totalBytesRemaining > 0) {
            var fileContent = selectedFile.slice(currentFilePointer, currentFilePointer + maxBlockSize);
            var blockId = blockIdPrefix + addPaddingToBlockId(blockIds.length, blockIdLength);

            blockIds.push(btoa(blockId));
            fileReader.readAsArrayBuffer(fileContent);

            currentFilePointer += maxBlockSize;
            totalBytesRemaining -= maxBlockSize;
            if (totalBytesRemaining < maxBlockSize) {
                maxBlockSize = totalBytesRemaining;
            }
        }
        else {
            commitBlockList();
        }
    }

    // Uploads a chunk of a file
    fileReader.onloadend = function (evt) {
        if (evt.target.readyState == FileReader.DONE) { // DONE == 2
            var uri = submitUri + '&comp=block&blockid=' + blockIds[blockIds.length - 1];
            var requestData = new Uint8Array(evt.target.result);

            $.ajax({
                url: uri,
                type: "PUT",
                headers: { 'x-ms-blob-type': 'BlockBlob' },
                data: requestData,
                processData: false
            })
            .done(function (data, status) {
                bytesUploaded += requestData.length;
                var percentComplete = ((parseFloat(bytesUploaded) / parseFloat(selectedFile.size)) * 100).toFixed(2);
                $("#upload-progress").text(percentComplete);
                AzureStorage.uploadFileInBlocks();
            })
            .fail(function (xhr, status, err) {
                displayErrorMessage(err);
            });
        }
    };

    // Calls WAMS service CreateWAMSAsset which creates a new WAMS asset and returns the submitUri needed for upload start
    function setWAMSUri(fileName) {
        $.ajax({
            type: "POST",
            url: "/api/WAMS/CreateAsset",
            contentType: "application/json; charset=utf-8",
            data: JSON.stringify({ fileName: fileName }),
            dataType: "json"
        })
        .done(function (data, status) {
            assetId = data.asset.Id;
            var baseUri = data.asset.Uri;
            var indexOfQueryStart = baseUri.indexOf("?");
            submitUri = baseUri.substring(0, indexOfQueryStart) + '/' + selectedFile.name + baseUri.substring(indexOfQueryStart);
            $("#upload-button").show();
            $("#preparing-file").hide();
        })
        .fail(function (xhr, status, err) {
            displayErrorMessage(err);
        });
    }

    // Sets FileInfo data and show it
    function setFileInfoData(selectedFile) {
        $("#file-name").text(selectedFile.name);
        $("#file-size").text(selectedFile.size);
        $("#file-type").text(selectedFile.type);
        $("#file-info").show();
    }

    // Commits file block (chunk) list to Azure
    function commitBlockList() {
        var uri = submitUri + "&comp=blocklist";
        var requestBody = getCommitRequestBody();

        $.ajax({
            url: uri,
            type: "PUT",
            headers: { "x-ms-blob-content-type": selectedFile.type },
            data: requestBody
        })
        .done(function (data, status) {
            publishWAMSAsset();
        })
        .fail(function (xhr, status, err) {
            displayErrorMessage(err);
        });
    }

    // Creates commit block list request body
    function getCommitRequestBody() {
        var requestBody = "<?xml version='1.0' encoding='utf-8'?><BlockList>";
        for (var i = 0; i < blockIds.length; i++) {
            requestBody += "<Latest>" + blockIds[i] + "</Latest>";
        }
        requestBody += "</BlockList>";
        return requestBody;
    }

    // Adds zeros (padding) to blockId
    function addPaddingToBlockId(blockIdPosition, paddingLength) {
        var paddedBlockId = "" + blockIdPosition;
        while (paddedBlockId.length < paddingLength) {
            paddedBlockId = "0" + paddedBlockId;
        }
        return paddedBlockId;
    }

    // Calls WAMS service PublishWAMSAsset which sets the uploaded video
    // as default, creates video locator and returns video public link
    function publishWAMSAsset() {
        $.ajax({
            type: "POST",
            url: "/api/WAMS/PublishAsset",
            contentType: "application/json; charset=utf-8",
            data: JSON.stringify({ assetId: assetId, fileName: selectedFile.name }),
            dataType: "json"
        })
        .done(function (data, status) {
            displayVideos(data.wamsLocators);
        })
        .fail(function (xhr, status, err) {
            displayErrorMessage(err);
        });
    }

    // Clears error message holder
    function clearErrorMessage() {
        $("#error-message-holder").empty();
    }

    // Displays error message in error message holder
    function displayErrorMessage(errorMessage) {
        if (errorMessage != undefined && errorMessage != null && errorMessage.length != 0)
            $("#error-message-holder").text("Error: " + errorMessage);
    }

    // Displays uploaded videos
    function displayVideos(videoUris) {
        console.log(videoUris);
        $.each(videoUris, function (index, element) {
            console.log(decodeURIComponent(element));

            var video = $("<video/>", {
                controls: true
            });

            var source = $("<source/>", {
                src: decodeURIComponent(element)
            }).appendTo(video);

            $("#videos-holder").append(video);
        });
    }
}(window.AzureStorage = window.AzureStorage || {}, jQuery));



$(document).ready(function () {
    $("#file-info").hide();
    $("#upload-progress-holder").hide();

    $("#file").change(AzureStorage.handleFileSelect);
    $("#upload-button").click(AzureStorage.uploadFileInBlocks);

    if (window.File && window.FileReader && window.FileList && window.Blob) {
        // Great success! All the File APIs are supported.
    }
    else {
        alert("The File APIs are not fully supported in this browser.");
    }
});
(function (AzureStorage, $) {
    var maxBlockSize = 0; // Real value iitialized in initializeVariables() method
    var maxFileSize = 1073741824; // Bytes
    var blockIds = new Array();
    var blockIdPrefix = "block-";
    var blockIdLength = 10;
    var bytesUploaded = 0;
    var currentFilePointer = 0;
    var totalBytesRemaining = 0;
    var selectedFile = null;
    var submitUri = null;
    var assetId = null;
    var fileReader = null;

    (function () {
        if (window.File && window.FileReader && window.FileList && window.Blob) {
            fileReader = new FileReader();
            initializeFileReaderOnLoadEnd();
        }
    })();

    // Initializes DOM elements
    function initializeDOMElements() {
        displayNotificationMessage("Preparing the file for upload...");
        $("#file-select-button").prop('disabled', true);
        $("#upload-button").hide();
        $("#upload-progress-holder").hide();
        $("#upload-progress").text("0.00");
    }

    // Initializes AzureStorage variables
    function initializeVariables() {
        blockIds = new Array();
        maxBlockSize = 1024 * 512; // Each file will be split into 512 KB chunks.
        bytesUploaded = 0;
        currentFilePointer = 0;
        totalBytesRemaining = 0;
    }

    // Reads the file and finds out the number of blocks it needs to be split into
    AzureStorage.handleFileSelect = function (evt) {
        clearMessages();
        initializeDOMElements();
        initializeVariables();

        selectedFile = evt.target.files[0];
        setFileInfoData(selectedFile);

        var fileSize = selectedFile.size;
        if (fileSize < maxFileSize) {
            if (fileSize < maxBlockSize) {
                maxBlockSize = fileSize;
            }
            totalBytesRemaining = fileSize;
            setWAMSUri(selectedFile.name);
        }
        else {
            displayErrorMessage("File too big.");
        }
    }

    // Reads file chunks or commits block list if all chunks have been uploaded already
    AzureStorage.uploadFileInBlocks = function () {
        $("#file-upload").hide();
        $("#upload-button").hide();
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

    // Initializes FileReader onloadend event handler
    function initializeFileReaderOnLoadEnd() {
        // Uploads a chunk of a file on file loaded
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
    }

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
            $("#file-select-button").prop('disabled', false);
            $("#upload-button").show();
            clearNotificationMessage();
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
            displayNotificationMessage("Upload complete, encoding task started. This might take a while, please wait...");
            $("#upload-button").hide();
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
            clearNotificationMessage();
            displayVideos(data.wamsLocators);
        })
        .fail(function (xhr, status, err) {
            displayErrorMessage(err);
        });
    }

    // Clears notification message
    function clearNotificationMessage() {
        $("#notification").empty();
    }

    // Clears error message
    function clearErrorMessage() {
        $("#error-message").empty();
    }

    // Clears notification and error messages
    function clearMessages() {
        clearNotificationMessage();
        clearErrorMessage();
    }

    // Displays notification message
    function displayNotificationMessage(notificationMessage) {
        clearMessages();
        $("#notification").text(notificationMessage);
    }

    // Displays error message
    function displayErrorMessage(errorMessage) {
        clearMessages();
        if (errorMessage != undefined && errorMessage != null && errorMessage.length != 0)
            $("#error-message").text("Error: " + errorMessage);
    }

    // Displays uploaded videos
    function displayVideos(videoUris) {
        $.each(videoUris, function (index, element) {
            console.log(element.FullUrl);

            var video = $("<video/>", {
                controls: true
            });

            var source = $("<source/>", {
                src: element.FullUrl
            }).appendTo(video);

            $("#videos-holder").append(video);
        });
    }
}(window.AzureStorage = window.AzureStorage || {}, jQuery));



$(document).ready(function () {
    $("#file-info").hide();
    $("#upload-progress-holder").hide();

    if (window.File && window.FileReader && window.FileList && window.Blob) {
        // Great success! All the File APIs are supported.
        $("#file-select-button").change(AzureStorage.handleFileSelect);
        $("#upload-button").click(AzureStorage.uploadFileInBlocks);
    }
    else {
        alert("Not all File APIs are fully supported in this browser.");
    }
});
WAMS async CORS enabled video upload demo
======================

This is an ASP.NET MVC demo application created as a proof-of-concept on how to do async CORS enabled video upload to Azure Media Services. To create WAMS assets and locators and to run WAMS encoding job/tasks, a WebAPI service using [WAMS .net SDK v3](http://www.nuget.org/packages/windowsazure.mediaservices) was used. By doing all that stuff on the server, it's easier to keep everything secure since your Azure credentials are not public and you are the one in charge of creating all the assets, locators and jobs. WAMS REST API was used only to upload chunks of data directly to Azure. By uploading the data directly to Azure, both CPU load and bandwidth consumption of the server hosting the web application are lowered (just imagine a scenario where your web application is not hosted through Azure and all the blob data needs to go through it for each file upload).

## WAMS configuration
Your WAMS credentials need to be entered in web.config => appSettings ("wamsAccountName" and "wamsAccountKey") for the application to work.

## CORS rules setup

To set-up the CORS rules for your Azure account, you can use [azure-cors-rule-manager](https://github.com/pootzko/azure-cors-rule-manager).
You will need to create a CORS rule with the following settings for WACU to work:
* **Allowed origins:** http://yourdoman
* **Allowed methods:** PUT
* **Allowed headers:** content-type, accept, x-ms-*
* **Exposed headers:** x-ms-*
* **Max age (seconds):** 3600 (you can set a lower or higher value for this one)

## Credits

* A lot of the client-side stuff is based on the code Gaurav Mantri provided in his awesome blog post about [uploading large files to Azure blob storage using SAS ](http://gauravmantri.com/2013/02/16/uploading-large-files-in-windows-azure-blob-storage-using-shared-access-signature-html-and-javascript/)
* Nick Drouin also wrote a few very helpful posts about WAMS which I reccomend you take a look at:
  * [Creating a simple media asset](http://blog-ndrouin.azurewebsites.net/?p=1261)
  * [A simple scenario: Upload, Encode and Package, Stream](http://blog-ndrouin.azurewebsites.net/?p=1931)

======================

Made at: [Mono Software Ltd.](http://www.mono-software.com/)

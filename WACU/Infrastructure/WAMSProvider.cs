using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using WACU.Models;

namespace WACU.Infrastructure
{
    public class WAMSProvider
    {
        #region Fields
        private CloudMediaContext _cmContext = null;
        private static readonly string _encoderProcessorName = "Windows Azure Media Encoder";
        private static readonly string _packagerProcessorName = "Windows Azure Media Packager";
        private static readonly string _uploadAccessPolicyName = "Video Upload Access Policy";
        private static readonly string _uploadLocatorName = "Upload Locator";

        private static readonly string _encodingPresetSmall = "H264 Broadband SD 16x9";
        private static readonly string _encodingPresetMedium = "H264 Broadband 720p";

        private static readonly string _assetSizeSmall = "Small";
        private static readonly string _assetSizeMedium = "Medium";
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        public WAMSProvider()
        {
            _cmContext = GetContext();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets Azure Media Storage context.
        /// </summary>
        /// <returns></returns>
        private static CloudMediaContext GetContext()
        {
            return new CloudMediaContext(AppSettings.WamsAccountName, AppSettings.WamsAccountKey);
        }

        #region Access Policies
        /// <summary>
        /// Gets WAMS upload access policy if it already exists, if not - creates one.
        /// </summary>
        /// <returns>Access policy.</returns>
        protected virtual IAccessPolicy GetUploadAccessPolicy()
        {
            var accessPolicy = _cmContext.AccessPolicies.Where(p => p.Name == _uploadAccessPolicyName).FirstOrDefault();

            if (accessPolicy == null)
                return CreateUploadAccessPolicy();

            return accessPolicy;
        }

        /// <summary>
        /// Creates new WAMS upload access policy.
        /// </summary>
        /// <returns>Access policy.</returns>
        protected virtual IAccessPolicy CreateUploadAccessPolicy()
        {
            return _cmContext.AccessPolicies.Create(
                _uploadAccessPolicyName,
                TimeSpan.FromHours(AppSettings.WamsUploadLocatorValidFor),
                AccessPermissions.Write);
        }

        /// <summary>
        /// Creates an access policy for accessing video publically.
        /// </summary>
        /// <param name="assetName">Asset name.</param>
        /// <returns>IAccessPolicy entity.</returns>
        protected virtual IAccessPolicy CreateVideoAccessPolicy(String assetName)
        {
            return _cmContext.AccessPolicies.Create(assetName, TimeSpan.FromDays(AppSettings.WamsVideoAvailableFor), AccessPermissions.Read | AccessPermissions.List);
        }
        #endregion

        #region Upload
        /// <summary>
        /// Creates new WAMS asset with upload uri locator.
        /// </summary>
        /// <param name="fileName">FileName for the asset to be created.</param>
        /// <returns>WAMSAsset entity with upload uri and asset name.</returns>
        public WAMSAssetModel CreateWAMSAsset(string fileName)
        {
            // ResetWAMS(); // only for testing to keep storage clean and easy to navigate through the web interface

            if (!VideoFileTypeAllowed(fileName))
                throw new Exception("Unsupported file type.");

            var assetName = String.Format("videoOriginal - {0}", fileName);
            IAsset asset = _cmContext.Assets.Create(assetName, AssetCreationOptions.None);
            IAssetFile assetFile = asset.AssetFiles.Create(fileName);
            IAccessPolicy writePolicy = GetUploadAccessPolicy();
            ILocator uploadLocator = _cmContext.Locators.CreateSasLocator(asset, writePolicy, DateTime.UtcNow.AddMinutes(-5), _uploadLocatorName);

            var uri = new Uri(uploadLocator.Path).AbsoluteUri;
            return new WAMSAssetModel() { Uri = uri, Id = asset.Id };
        }

        /// <summary>
        /// Publishes a WAMS asset and returns locators for created assets (public links for their default asset blobs).
        /// </summary>
        /// <param name="assetId">Original asset Id.</param>
        /// <param name="fileName">Original blob file name.</param>
        /// <returns>WAMSJobLocators model entity.</returns>
        public WAMSJobLocatorsModel PublishWAMSAsset(string assetId, string fileName)
        {
            if (!VideoFileTypeAllowed(fileName))
                throw new Exception("Unsupported file type.");

            IAsset asset = _cmContext.Assets.Where(p => p.Id == assetId).FirstOrDefault();
            SetPrimaryAssetFile(asset, fileName);

            if (VideoFileSizeAllowed(asset, fileName))
            {
                SetPrimaryAssetFile(asset, fileName);
                RemoveUploadLocator(asset);
                var job = ProcessVideo(asset, fileName);

                return CreateWAMSJobLocators(fileName, asset, job);
            }
            else
            {
                throw new Exception("File too big.");
            }
        }
        #endregion

        #region Locators
        /// <summary>
        /// Sets a file as a primary file for an Azure asset.
        /// </summary>
        /// <param name="asset">Azure asset for which the primary file is to be set.</param>
        /// <param name="fileName">File name of the file to become primary.</param>
        protected virtual void SetPrimaryAssetFile(IAsset asset, string fileName)
        {
            var assetFile = asset.AssetFiles.Where(p => p.Name == fileName).FirstOrDefault();
            assetFile.IsPrimary = true;
            assetFile.Update();
        }

        /// <summary>
        /// Creates locators for all job assets.
        /// </summary>
        /// <param name="fileName">Original file name.</param>
        /// <param name="asset">Original video asset.</param>
        /// <param name="job">IJob entity.</param>
        /// <returns>WAMSJobLocators entity containing original and encoded video locators.</returns>
        protected virtual WAMSJobLocatorsModel CreateWAMSJobLocators(string fileName, IAsset asset, IJob job)
        {
            var wamsAssets = GetWAMSTaskAssets(job);

            // Add additional locators to this model if needed
            return new WAMSJobLocatorsModel()
            {
                OriginalVideo = CreateLocator(asset, fileName),
                EncodedVideoSmall = CreateVideoLocator(wamsAssets.EncodedVideoSmall),
                EncodedVideoMedium = CreateVideoLocator(wamsAssets.EncodedVideoMedium)
            };
        }

        /// <summary>
        /// Creates a locator for a video within an Azure asset.
        /// </summary>
        /// <param name="asset">Azure asset.</param>
        /// <returns>WAMSLocator which contains Uri params (Url base, path and query).</returns>
        protected virtual WAMSLocatorModel CreateVideoLocator(IAsset asset)
        {
            var assetFiles = asset.AssetFiles.ToList();
            var assetFileName = asset.AssetFiles.Where(p => p.Name.EndsWith(".mp4")).FirstOrDefault().Name;
            return CreateLocator(asset, assetFileName);
        }

        /// <summary>
        /// Creates a locator for a file within an Azure asset.
        /// </summary>
        /// <param name="asset">Azure asset.</param>
        /// <param name="fileName">File name of the file for which a locator is to be created.</param>
        /// <returns>WAMSLocator entity which contains Uri params (Url base, path and query).</returns>
        protected virtual WAMSLocatorModel CreateLocator(IAsset asset, string fileName)
        {
            var accessPolicy = CreateVideoAccessPolicy(asset.Name);
            var locator = _cmContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);
            var uri = new UriBuilder(locator.Path);

            return new WAMSLocatorModel()
            {
                UrlBase = String.Format("{0}://{1}", uri.Scheme, uri.Host),
                Path = String.Format("{0}/{1}", uri.Path, fileName),
                Query = uri.Query
            };
        }

        /// <summary>
        /// Removes SAS upload locator for an asset so no further uploads can be done against its upload Uri.
        /// </summary>
        /// <param name="asset">IAsset entity.</param>
        protected virtual void RemoveUploadLocator(IAsset asset)
        {
            asset.Locators.Where(p => p.Name == _uploadLocatorName).ToList().ForEach(p => p.Delete());
        }
        #endregion

        #region Video Processing
        /// <summary>
        /// Gets latest media encoder processor.
        /// </summary>
        /// <returns>MediaProcessor.</returns>
        private IMediaProcessor GetLatestMediaEncoderProcessor()
        {
            return GetLatestMediaProcessorByName(_encoderProcessorName);
        }

        /// <summary>
        /// Gets latest media packager processor.
        /// </summary>
        /// <returns>MediaProcessor.</returns>
        private IMediaProcessor GetLatestMediaPackagerProcessor()
        {
            return GetLatestMediaProcessorByName(_packagerProcessorName);
        }

        /// <summary>
        /// The possible strings that can be passed into the 
        /// method for the mediaProcessor parameter:
        ///     "Windows Azure Media Encoder"
        ///     "Windows Azure Media Packager"
        ///     "Windows Azure Media Encryptor"
        ///     "Storage Decryption"
        /// </summary>
        /// <param name="mediaProcessorName">Azure Media Processor name.</param>
        /// <returns>MediaProcessor.</returns>
        private IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
        {
            var processor = _cmContext.MediaProcessors
                .Where(p => p.Name == mediaProcessorName).ToList()
                .OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(String.Format("Unknown media processor: {0}", mediaProcessorName));

            return processor;
        }


        /// <summary>
        /// Processes (encoding/packaging) a video asset.
        /// Creates video encoding and thumbnails creation tasks, adds them to IJob and submits the job to Azure.
        /// </summary>
        /// <param name="asset">Original video asset to process.</param>
        /// <param name="fileName">Blob file name.</param>
        /// <returns>IJob entity.</returns>
        protected virtual IJob ProcessVideo(IAsset asset, string fileName)
        {
            IJob job = _cmContext.Jobs.Create(String.Format("Asset job: {0}", asset.Id));

            CreateVideoEncodingTasks(asset, fileName, job);
            job.StateChanged += new EventHandler<JobStateChangedEventArgs>(JobStateChanged);
            job.Submit();
            job.GetExecutionProgressTask(CancellationToken.None).Wait();

            return job;
        }

        /// <summary>
        /// Creates video encoding tasks for an asset and adds newly created video assets to taskAssets entity.
        /// </summary>
        /// <param name="asset">Original video asset to process.</param>
        /// <param name="fileName">Blob file name.</param>
        /// <param name="job">IJob entity.</param>
        /// <returns>Largest IAsset entity.</returns>
        protected virtual IAsset CreateVideoEncodingTasks(IAsset asset, string fileName, IJob job)
        {
            // Add additional encoding tasks here if needed
            var assetMedium = CreateVideoEncodingTask(asset, fileName, job, _assetSizeMedium, _encodingPresetMedium);
            var assetSmall = CreateVideoEncodingTask(assetMedium, fileName, job, _assetSizeSmall, _encodingPresetSmall);

            return assetMedium;
        }

        /// <summary>
        /// Creates a video encoding task.
        /// </summary>
        /// <param name="asset">Asset to encode.</param>
        /// <param name="fileName">Video file name.</param>
        /// <param name="job">IJob entity.</param>
        /// <param name="configuration">Encoding configuration preset.</param>
        /// <returns>Newly created video "encode" asset.</returns>
        protected virtual IAsset CreateVideoEncodingTask(IAsset asset, string fileName, IJob job, string assetType, string configuration)
        {
            IMediaProcessor encoder = GetLatestMediaEncoderProcessor();

            ITask task = job.Tasks.AddNew(
                String.Format("{0} encoding task", configuration),
                encoder,
                configuration,
                TaskOptions.None);

            task.InputAssets.Add(asset);
            return task.OutputAssets.AddNew(String.Format("video{0} - {1}", assetType, fileName), AssetCreationOptions.None);
        }

        /// <summary>
        /// Creates WAMSTaskAssets entity from job (fetches assets from job.OutputMediaAssets).
        /// </summary>
        /// <param name="job">IJob entity.</param>
        /// <returns>WAMSTaskAssets entity.</returns>
        protected virtual WAMSTaskAssetsModel GetWAMSTaskAssets(IJob job)
        {
            var outputAssets = job.OutputMediaAssets;

            // Add additional assets here if needed
            return new WAMSTaskAssetsModel()
            {
                EncodedVideoSmall = outputAssets.FirstOrDefault(p => p.Name.Contains(String.Format("video{0}", _assetSizeSmall))),
                EncodedVideoMedium = outputAssets.FirstOrDefault(p => p.Name.Contains(String.Format("video{0}", _assetSizeMedium)))
            };
        }

        /// <summary>
        /// Logs job state to debug (on state change).
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="evtArgs">Event args.</param>
        static void JobStateChanged(object sender, JobStateChangedEventArgs evtArgs)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} - State: {1};  Time: {2};", ((IJob)sender).Name, evtArgs.CurrentState, DateTime.UtcNow.ToString(@"yyyy_M_d__hh_mm_ss")));
        }
        #endregion
        

        #region Azure
        /// <summary>
        /// Gets Azure blob client.
        /// </summary>
        /// <returns>CloudBlobClient entity.</returns>
        private CloudBlobClient GetAzureClient()
        {
            var connectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", AppSettings.AzureAccountName, AppSettings.AzureAccountKey);
            var account = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(connectionString);
            return account.CreateCloudBlobClient();
        }

        /// <summary>
        /// Gets Azure blob container.
        /// </summary>
        /// <param name="client">Azure blob client entity.</param>
        /// <param name="containerName">Blob container name.</param>
        /// <returns>CloudBlobContainer entity.</returns>
        private CloudBlobContainer GetAzureContainer(CloudBlobClient client, string containerName)
        {
            return client.GetContainerReference(containerName);
        }
        #endregion

        #region Other
        /// <summary>
        /// Checks if file type is allowed.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <returns>Is allowed?</returns>
        protected virtual bool VideoFileTypeAllowed(string fileName)
        {
            var fileExtension = Path.GetExtension(fileName);
            var allowedExtensions = GetAllowedVideoFileExtensions();
            return allowedExtensions.Contains(fileExtension);
        }

        /// <summary>
        /// Gets a list of video file extensions which are allowed for upload.
        /// </summary>
        /// <returns>List of file extensions.</returns>
        protected virtual IList<string> GetAllowedVideoFileExtensions()
        {
            return AppSettings.WamsAllowedVideoFileExtensions.Split(',').ToList();
        }

        /// <summary>
        /// Strips a file name of all the invalid characters.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <returns>Sanitized file name.</returns>
        public string SanitizeFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
        }

        /// <summary>
        /// Checks if uploaded file (Azure blob) is of right size, if not also delete the whole container.
        /// </summary>
        /// <param name="asset">Original IAsset entity.</param>
        /// <param name="fileName">File name of blob in container.</param>
        /// <returns>File size allowed?</returns>
        public bool VideoFileSizeAllowed(IAsset asset, string fileName)
        {
            var client = GetAzureClient();
            var blobContainerName = asset.Uri.Segments[1];
            var container = GetAzureContainer(client, blobContainerName);
            var blob = container.GetBlobReferenceFromServer(fileName);

            if (blob != null)
            {
                var sizeAllowed = blob.Properties.Length < AppSettings.AzureMaxFileSize ? true : false;
                if (!sizeAllowed)
                    container.Delete();

                return sizeAllowed;
            }
            else
            {
                throw new Exception("Blob empty.");
            }
        }

        /// <summary>
        /// Clears Assets and AccessPolicies.
        /// </summary>
        private void ResetWAMS()
        {
            _cmContext.Assets.ToList().ForEach(p => p.Delete());
            _cmContext.AccessPolicies.ToList().ForEach(p => p.Delete());
            _cmContext.Jobs.ToList().ForEach(p => p.Delete());
            _cmContext.Locators.ToList().ForEach(p => p.Delete());
        }
        #endregion
        #endregion
    }
}
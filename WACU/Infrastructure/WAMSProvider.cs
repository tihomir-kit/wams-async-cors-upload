using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;

namespace WACU.Infrastructure
{
    public class WAMSProvider
    {
        #region Fields
        private CloudMediaContext _cmContext = null;
        private int _estimatedUploadMaxTime = 3600; // seconds
        private int _videoAvailableFor = 20000; // days
        private static readonly string _encoderProcessorName = "Windows Azure Media Encoder";
        private static readonly string _packagerProcessorName = "Windows Azure Media Packager";
        private static readonly string _uploadAccessPolicyName = "Video Upload Access Policy";
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        public WAMSProvider()
        {
            _cmContext = GetContext();
        }

        /// <summary>
        /// Creates WAMSProvider instance.
        /// </summary>
        /// <returns>WAMSProvider instance.</returns>
        public static WAMSProvider GetInstance()
        {
            return new WAMSProvider();
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
        /// Gets WAMS upload access policy.
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
        /// Creates WAMS upload access policy.
        /// </summary>
        /// <returns>Access policy.</returns>
        protected virtual IAccessPolicy CreateUploadAccessPolicy()
        {
            return _cmContext.AccessPolicies.Create(
                _uploadAccessPolicyName,
                TimeSpan.FromMinutes(_estimatedUploadMaxTime),
                AccessPermissions.Write); //AccessPermissions.Write | AccessPermissions.List);
        }
        #endregion

        #region Upload
        /// <summary>
        /// Creates new WAMS asset with upload uri locator.
        /// </summary>
        /// <param name="fileName">FileName for the asset to be created.</param>
        /// <returns>WAMSAsset object with upload uri and asset name.</returns>
        public WAMSAsset CreateWAMSAsset(string fileName)
        {
            // ResetWAMS(); // TODO: remove, only for testing to keep storage clean and easy to navigate

            if (!VideoFileTypeAllowed(fileName))
                throw new Exception("Unsupported file type.");

            var assetName = String.Format("videoOriginal - {0}", fileName);
            IAsset asset = _cmContext.Assets.Create(assetName, AssetCreationOptions.None);
            IAssetFile assetFile = asset.AssetFiles.Create(fileName);
            IAccessPolicy writePolicy = GetUploadAccessPolicy();
            ILocator destinationLocator = _cmContext.Locators.CreateSasLocator(asset, writePolicy, DateTime.UtcNow.AddMinutes(-5));

            var uri = new Uri(destinationLocator.Path).AbsoluteUri;
            return new WAMSAsset() { Uri = uri, Id = asset.Id };
        }

        /// <summary>
        /// Publishes a WAMS asset and returns locator (public link for the default asset blob).
        /// </summary>
        /// <param name="assetId">Asset id.</param>
        /// <param name="fileName">Blob file name.</param>
        /// <returns>WAMS locators dictionary.</returns>
        public IDictionary<string, string> PublishWAMSAsset(string assetId, string fileName)
        {
            if (!VideoFileTypeAllowed(fileName))
                throw new Exception("Unsupported file type.");

            IAsset asset = _cmContext.Assets.Where(p => p.Id == assetId).FirstOrDefault();
            SetPrimaryAssetFile(asset, fileName);

            var job = ProcessVideo(asset, fileName);
            var broadbandSDAsset = job.OutputMediaAssets.FirstOrDefault(p => p.Name.Contains("BroadbandSD"));
            var broadband720Asset = job.OutputMediaAssets.FirstOrDefault(p => p.Name.Contains("Broadband720"));

            var originalVideoLocator = CreateVideoLocator(asset, fileName);
            var broadbandSDVideoLocator = CreateVideoLocator(broadbandSDAsset, broadbandSDAsset.AssetFiles.Where(p => p.Name.EndsWith(".mp4")).FirstOrDefault().Name);
            var broadband720VideoLocator = CreateVideoLocator(broadband720Asset, broadband720Asset.AssetFiles.Where(p => p.Name.EndsWith(".mp4")).FirstOrDefault().Name);

            //// Smooth asset locator cration example
            //var abrAsset = job.OutputMediaAssets.FirstOrDefault(p => p.Name.Contains("Adaptive"));
            //var smoothAsset = job.OutputMediaAssets.FirstOrDefault(p => p.Name.Contains("Smooth"));
            //var smoothVideoLocator = CreateSmoothVideoLocator(smoothAsset);

            IDictionary<string, string> wamsLocators = new Dictionary<string, string>();
            wamsLocators.Add("originalVideoLocator", EncodeLocator(originalVideoLocator));
            wamsLocators.Add("broadbandSDVideoLocator", EncodeLocator(broadbandSDVideoLocator));
            wamsLocators.Add("broadband720VideoLocator", EncodeLocator(broadband720VideoLocator));

            return wamsLocators;
        }

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
        /// Creates a locator for a file within an Azure asset.
        /// </summary>
        /// <param name="asset">Azure asset.</param>
        /// <param name="fileName">File name of the file for which a locator is to be created.</param>
        /// <returns>WAMSLocator object which contains Uri params (Url base, path and query).</returns>
        protected virtual WAMSLocator CreateVideoLocator(IAsset asset, string fileName)
        {
            var assetFile = asset.AssetFiles.Where(p => p.Name == fileName).FirstOrDefault();
            var accessPolicy = _cmContext.AccessPolicies.Create(asset.Name, TimeSpan.FromDays(_videoAvailableFor), AccessPermissions.Read | AccessPermissions.List);
            var locator = _cmContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);
            var videoUri = new UriBuilder(locator.Path);

            return new WAMSLocator()
            {
                UrlBase = String.Format("{0}://{1}", videoUri.Scheme, videoUri.Host),
                Path = String.Format("{0}/{1}", videoUri.Path, fileName),
                Query = videoUri.Query
            };
        }

        /// <summary>
        /// Creates smooth package locator.
        /// Uri can be tested at: http://smf.cloudapp.net/healthmonitor
        /// </summary>
        /// <param name="asset">Azure asset.</param>
        /// <returns>WAMSLocator object which contains Uri params (Url base, path and query).</returns>
        protected virtual WAMSLocator CreateSmoothPackageLocator(IAsset asset)
        {
            var assetFile = asset.AssetFiles.Where(p => p.Name.EndsWith(".ism")).FirstOrDefault();
            var accessPolicy = _cmContext.AccessPolicies.Create(asset.Name, TimeSpan.FromDays(_videoAvailableFor), AccessPermissions.Read | AccessPermissions.List);
            var locator = _cmContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset, accessPolicy);
            var videoUri = new UriBuilder(String.Format("{0}{1}/manifest", locator.Path, assetFile.Name));

            return new WAMSLocator()
            {
                UrlBase = String.Format("{0}://{1}", videoUri.Scheme, videoUri.Host),
                Path = videoUri.Path,
                Query = videoUri.Query
            };
        }

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
        /// Clears Assets and AccessPolicies.
        /// </summary>
        private void ResetWAMS()
        {
            _cmContext.Assets.ToList().ForEach(p => p.Delete());
            _cmContext.AccessPolicies.ToList().ForEach(p => p.Delete());
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
        /// </summary>
        /// <param name="asset">Original video asset to process.</param>
        /// <param name="fileName">Blob file name.</param>
        /// <returns>IJob entity.</returns>
        protected virtual IJob ProcessVideo(IAsset asset, string fileName)
        {
            IJob job = _cmContext.Jobs.Create(String.Format("Asset job: {0}", asset.Id));

            var broadband720Asset = CreateVideoEncodingTask(asset, fileName, job, "Broadband720p", "H264 Broadband 720p");
            var broadbandSDAsset = CreateVideoEncodingTask(broadband720Asset, fileName, job, "BroadbandSD", "H264 Broadband SD 16x9");


            //// Adaptive to Smooth asset creation examples (chained job)
            //var abrAsset = CreateVideoEncodingTask(asset, fileName, job, "Adaptive", "H264 Adaptive Bitrate MP4 Set 720p");
            //var smoothAsset = CreateVideoSmoothPackagingTask(abrAsset, fileName, job);

            job.StateChanged += new EventHandler<JobStateChangedEventArgs>(JobStateChanged);
            job.Submit();
            job.GetExecutionProgressTask(CancellationToken.None).Wait();

            return job;
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
                String.Format("{0} encoding task for asset: {1}", configuration, asset.Id),
                encoder,
                configuration,
                TaskOptions.None);

            task.InputAssets.Add(asset);
            return task.OutputAssets.AddNew(String.Format("video{0} - {1}", assetType, fileName), AssetCreationOptions.None);
        }

        /// <summary>
        /// Creates a video packaging task (Smooth).
        /// </summary>
        /// <param name="asset">Asset to package (must be Adaptive Bitrate Mp4).</param>
        /// <param name="fileName">Video file name.</param>
        /// <param name="job">IJob entity.</param>
        /// <returns>Newly created video "smooth" asset.</returns>
        protected virtual IAsset CreateVideoSmoothPackagingTask(IAsset asset, string fileName, IJob job)
        {
            IMediaProcessor packager = GetLatestMediaPackagerProcessor();
            string smoothConfig = File.ReadAllText(HttpContext.Current.Server.MapPath("~/Configuration/MediaPackagers/MP4ToSmooth.xml"));

            ITask task = job.Tasks.AddNew(
                String.Format("Adaptive Bitrate to Smooth packaging task for asset: {0}", asset.Id),
                packager,
                smoothConfig,
                TaskOptions.None);

            task.InputAssets.Add(asset);
            return task.OutputAssets.AddNew(String.Format("videoSmooth - {0}", fileName), AssetCreationOptions.None);
        }

        /// <summary>
        /// Logs job state to debug (on state change).
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event args.</param>
        static void JobStateChanged(object sender, JobStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} - State: {1};  Time: {2};", ((IJob)sender).Name, e.CurrentState, DateTime.UtcNow.ToString(@"yyyy_M_d__hh_mm_ss")));
        }
        #endregion

        #region Other
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
        /// UrlEncodes a locator.
        /// </summary>
        /// <param name="locator">WAMS locator.</param>
        /// <returns>UrlEncoded locator URI.</returns>
        private string EncodeLocator(WAMSLocator locator)
        {
            var encodedQuery = HttpUtility.UrlEncode(locator.Query);
            return String.Format("{0}{1}", locator.UrlWithPath, encodedQuery);
        }
        #endregion
        #endregion
    }

    public class WAMSAsset
    {
        public string Uri { get; set; }
        public string Id { get; set; }
    }

    public class WAMSLocator
    {
        public string UrlBase { get; set; }
        public string Path { get; set; }
        public string Query { get; set; }
        public string UrlWithPath
        {
            get { return String.Format("{0}{1}", this.UrlBase, this.Path); }
        }
    }
}
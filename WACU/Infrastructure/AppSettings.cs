using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;

namespace WACU.Infrastructure
{
    public static class AppSettings
    {
        private static string _azureAccountName = null;
        /// <summary>
        /// Gets AzureAccountName.
        /// </summary>
        public static string AzureAccountName
        {
            get
            {
                if (_azureAccountName == null)
                    _azureAccountName = WebConfigurationManager.AppSettings["azureAccountName"];

                return _azureAccountName;
            }
        }

        private static string _azureAccountKey = null;
        /// <summary>
        /// Gets AzureAccountKey.
        /// </summary>
        public static string AzureAccountKey
        {
            get
            {
                if (_azureAccountKey == null)
                    _azureAccountKey = WebConfigurationManager.AppSettings["azureAccountKey"];

                return _azureAccountKey;
            }
        }

        private static int _azureMaxFileSize = 0;
        /// <summary>
        /// Gets AzureMaxFileSize.
        /// </summary>
        public static int AzureMaxFileSize
        {
            get
            {
                if (_azureMaxFileSize == 0)
                    int.TryParse(WebConfigurationManager.AppSettings["azureMaxFileSize"], out _azureMaxFileSize);

                return _azureMaxFileSize;
            }
        }

        #region Windows Azure Media Services
        private static string _wamsAccountName = null;
        /// <summary>
        /// Gets WamsAccountName.
        /// </summary>
        public static string WamsAccountName
        {
            get
            {
                if (_wamsAccountName == null)
                    _wamsAccountName = WebConfigurationManager.AppSettings["wamsAccountName"];

                return _wamsAccountName;
            }
        }

        private static string _wamsAccountKey = null;
        /// <summary>
        /// Gets WamsAccountKey.
        /// </summary>
        public static string WamsAccountKey
        {
            get
            {
                if (_wamsAccountKey == null)
                    _wamsAccountKey = WebConfigurationManager.AppSettings["wamsAccountKey"];

                return _wamsAccountKey;
            }
        }

        private static int _wamsUploadLocatorValidFor = 0;
        /// <summary>
        /// Gets WamsUploadLocatorValidFor.
        /// </summary>
        public static int WamsUploadLocatorValidFor
        {
            get
            {
                if (_wamsUploadLocatorValidFor == 0)
                    int.TryParse(WebConfigurationManager.AppSettings["wamsUploadLocatorValidFor"], out _wamsUploadLocatorValidFor);

                return _wamsUploadLocatorValidFor;
            }
        }

        private static int _wamsVideoAvailableFor = 0;
        /// <summary>
        /// Gets WamsVideoAvailableFor.
        /// </summary>
        public static int WamsVideoAvailableFor
        {
            get
            {
                if (_wamsVideoAvailableFor == 0)
                    int.TryParse(WebConfigurationManager.AppSettings["wamsVideoAvailableFor"], out _wamsVideoAvailableFor);

                return _wamsVideoAvailableFor;
            }
        }

        private static string _wamsAllowedVideoFileExtensions = null;
        /// <summary>
        /// Gets WamsAllowedVideoFileExtensions.
        /// </summary>
        public static string WamsAllowedVideoFileExtensions
        {
            get
            {
                if (_wamsAllowedVideoFileExtensions == null)
                    _wamsAllowedVideoFileExtensions = WebConfigurationManager.AppSettings["wamsAllowedVideoFileExtensions"];

                return _wamsAllowedVideoFileExtensions;
            }
        }
        #endregion
    }
}
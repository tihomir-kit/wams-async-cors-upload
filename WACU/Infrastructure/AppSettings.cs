using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;

namespace WACU.Infrastructure
{
    public static class AppSettings
    {
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
    }
}
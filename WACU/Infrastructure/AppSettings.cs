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
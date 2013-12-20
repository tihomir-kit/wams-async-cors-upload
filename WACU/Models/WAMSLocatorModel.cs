using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WACU.Models
{
    public class WAMSLocatorModel
    {
        /// <summary>
        /// Gets or sets url base for the uri.
        /// </summary>
        public string UrlBase { get; set; }

        /// <summary>
        /// Gets or sets path part of uri.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets query part of uri.
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Calculates url with path.
        /// </summary>
        public string UrlWithPath
        {
            get { return String.Format("{0}{1}", this.UrlBase, this.Path); }
        }
    }
}
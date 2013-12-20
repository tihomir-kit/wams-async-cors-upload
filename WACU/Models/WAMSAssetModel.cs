using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WACU.Models
{
    public class WAMSAssetModel
    {
        /// <summary>
        /// Gets or sets WAMS asset upload uri.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets WAMS asset Id.
        /// </summary>
        public string Id { get; set; }
    }
}
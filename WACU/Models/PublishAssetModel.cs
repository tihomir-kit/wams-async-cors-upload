using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WACU.Models
{
    public class PublishAssetModel
    {
        /// <summary>
        /// Gets or sets id of the asset to be published.
        /// </summary>
        public string AssetId { get; set; }

        /// <summary>
        /// Gets or sets blob file name.
        /// </summary>
        public string FileName { get; set; }
    }
}
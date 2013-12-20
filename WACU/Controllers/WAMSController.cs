using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.WebHost;
using WACU.Infrastructure;
using WACU.Models;


namespace WACU.Controllers
{
    public class WAMSController : ApiController
    {
        private WAMSProvider _wamsProvider = null;

        public WAMSController()
        {
            _wamsProvider = new WAMSProvider();
        }

        /// <summary>
        /// Creates a new WAMS asset for the file.
        /// </summary>
        /// <param name="model">CreateAsset model which contains blob file name..</param>
        /// <returns>WAMS asset.</returns>
        [HttpPost]
        public HttpResponseMessage CreateAsset(CreateAssetModel model)
        {
            var asset = new WAMSAssetModel();
            try
            {
                asset = _wamsProvider.CreateWAMSAsset(_wamsProvider.SanitizeFileName(model.FileName));
            }
            catch (Exception e)
            {
                return this.Request.CreateResponse(HttpStatusCode.BadRequest, new { errorMessage = e.Message });
            }
            return this.Request.CreateResponse(HttpStatusCode.OK, new { asset });
        }

        /// <summary>
        /// Publishes WAMS asset.
        /// </summary>
        /// <param name="model">PublishAsset model which contains id of the asset to be published and blob file name.</param>
        /// <returns>WAMS locators dictionary.</returns>
        [HttpPost]
        public HttpResponseMessage PublishAsset(PublishAssetModel model)
        {
            IDictionary<string, string> wamsLocators = new Dictionary<string, string>();
            try
            {
                wamsLocators = _wamsProvider.PublishWAMSAsset(model.AssetId, _wamsProvider.SanitizeFileName(model.FileName));
            }
            catch (Exception e)
            {
                return this.Request.CreateResponse(HttpStatusCode.BadRequest, new { errorMessage = e.Message });
            }
            return this.Request.CreateResponse(HttpStatusCode.OK, new { wamsLocators });
        }
    }
}

using System;
using System.IO;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Plugins;
using Nop.Plugin.Feed.Become.Models;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Feed.Become.Controllers
{
    [AdminAuthorize]
    public class FeedBecomeController : BasePluginController
    {
        #region Fields

        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly IPluginFinder _pluginFinder;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly BecomeSettings _becomeSettings;

        #endregion

        #region Ctor

        public FeedBecomeController(ICurrencyService currencyService,
            ILocalizationService localizationService, 
            IPluginFinder pluginFinder, 
            ILogger logger, 
            IWebHelper webHelper,
            ISettingService settingService,
            IStoreContext storeContext,
            BecomeSettings becomeSettings)
        {
            this._currencyService = currencyService;
            this._localizationService = localizationService;
            this._pluginFinder = pluginFinder;
            this._logger = logger;
            this._webHelper = webHelper;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._becomeSettings = becomeSettings;
        }

        #endregion

        #region Methods

        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new FeedBecomeModel
            {
                ProductPictureSize = _becomeSettings.ProductPictureSize,
                CurrencyId = _becomeSettings.CurrencyId
            };

            foreach (var c in _currencyService.GetAllCurrencies(false))
            {
                model.AvailableCurrencies.Add(new SelectListItem()
                    {
                         Text = c.Name,
                         Value = c.Id.ToString()
                    });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [ChildActionOnly]
        [FormValueRequired("save")]
        public ActionResult Configure(FeedBecomeModel model)
        {
            if (!ModelState.IsValid)
            {
                return Configure();
            }
            
            //save settings
            _becomeSettings.ProductPictureSize = model.ProductPictureSize;
            _becomeSettings.CurrencyId = model.CurrencyId;

            _settingService.SaveSetting(_becomeSettings);

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //redisplay the form
            foreach (var c in _currencyService.GetAllCurrencies(false))
            {
                model.AvailableCurrencies.Add(new SelectListItem()
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        [ChildActionOnly]
        [HttpPost, ActionName("Configure")]
        [FormValueRequired("generate")]
        public ActionResult GenerateFeed(FeedBecomeModel model)
        {
            if (!ModelState.IsValid)
            {
                return Configure();
            }

            try
            {
                var fileName = string.Format("become_{0}_{1}.csv", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"), CommonHelper.GenerateRandomDigitCode(4));
                var filePath = Path.Combine(Request.PhysicalApplicationPath, "content\\files\\exportimport", fileName);

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("PromotionFeed.Become");

                    if (pluginDescriptor == null)
                        throw new Exception("Cannot load the plugin");

                    //plugin
                    var plugin = pluginDescriptor.Instance() as BecomeService;

                    if (plugin == null)
                        throw new Exception("Cannot load the plugin");

                    plugin.GenerateFeed(fs, _storeContext.CurrentStore);
                }

                var clickhereStr = string.Format("<a href=\"{0}content/files/exportimport/{1}\" target=\"_blank\">{2}</a>", _webHelper.GetStoreLocation(false), fileName, _localizationService.GetResource("Plugins.Feed.Become.ClickHere"));
                var result = string.Format(_localizationService.GetResource("Plugins.Feed.Become.SuccessResult"), clickhereStr);

                model.GenerateFeedResult = result;
            }
            catch (Exception exc)
            {
                model.GenerateFeedResult = exc.Message;
                _logger.Error(exc.Message, exc);
            }

            foreach (var c in _currencyService.GetAllCurrencies(false))
            {
                model.AvailableCurrencies.Add(new SelectListItem()
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        #endregion
    }
}

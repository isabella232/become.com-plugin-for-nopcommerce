using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Plugins;
using Nop.Plugin.Feed.Become.Models;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Feed.Become.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
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
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IPermissionService _permissionService;

        #endregion

        #region Ctor

        public FeedBecomeController(ICurrencyService currencyService,
            ILocalizationService localizationService, 
            IPluginFinder pluginFinder, 
            ILogger logger, 
            IWebHelper webHelper,
            ISettingService settingService,
            IStoreContext storeContext,
            BecomeSettings becomeSettings,
            IHostingEnvironment hostingEnvironment,
            IPermissionService permissionService)
        {
            this._currencyService = currencyService;
            this._localizationService = localizationService;
            this._pluginFinder = pluginFinder;
            this._logger = logger;
            this._webHelper = webHelper;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._becomeSettings = becomeSettings;
            this._hostingEnvironment = hostingEnvironment;
            this._permissionService = permissionService;
        }

        #endregion

        #region Methods

        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            var model = new FeedBecomeModel
            {
                ProductPictureSize = _becomeSettings.ProductPictureSize,
                CurrencyId = _becomeSettings.CurrencyId
            };

            foreach (var c in _currencyService.GetAllCurrencies())
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
        [FormValueRequired("save")]
        public IActionResult Configure(FeedBecomeModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

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
            foreach (var c in _currencyService.GetAllCurrencies())
            {
                model.AvailableCurrencies.Add(new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("generate")]
        public IActionResult GenerateFeed(FeedBecomeModel model)
        {
            if (!ModelState.IsValid)
            {
                return Configure();
            }

            try
            {
                var fileName = $"become_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}_{CommonHelper.GenerateRandomDigitCode(4)}.csv";
                var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "files\\exportimport", fileName);

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

                var clickhereStr = $"<a href=\"{_webHelper.GetStoreLocation(false)}wwwroot/files/exportimport/{fileName}\" target=\"_blank\">{_localizationService.GetResource("Plugins.Feed.Become.ClickHere")}</a>";
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Stores;
using Nop.Core.Html;
using Nop.Core.Plugins;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Seo;

namespace Nop.Plugin.Feed.Become
{
    public class BecomeService : BasePlugin,  IMiscPlugin
    {
        #region Fields

        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IPictureService _pictureService;
        private readonly ICurrencyService _currencyService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly BecomeSettings _becomeSettings;
        private readonly CurrencySettings _currencySettings;

        #endregion

        #region Ctor
        public BecomeService(IProductService productService,
            ICategoryService categoryService, 
            IManufacturerService manufacturerService, 
            IPictureService pictureService,
            ICurrencyService currencyService, 
            IWebHelper webHelper,
            ISettingService settingService,
            BecomeSettings becomeSettings, 
            CurrencySettings currencySettings)
        {
            this._productService = productService;
            this._categoryService = categoryService;
            this._manufacturerService = manufacturerService;
            this._pictureService = pictureService;
            this._currencyService = currencyService;
            this._webHelper = webHelper;
            this._settingService = settingService;
            this._becomeSettings = becomeSettings;
            this._currencySettings = currencySettings;
        }

        #endregion

        #region Utilities

        private Currency GetUsedCurrency()
        {
            var currency = _currencyService.GetCurrencyById(_becomeSettings.CurrencyId);

            if (currency == null || !currency.Published)
                currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);

            return currency;
        }

        private static string RemoveSpecChars(string s)
        {
            if (String.IsNullOrEmpty(s))
                return s;

            s = s.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

            return s;
        }

        private IList<Category> GetCategoryBreadCrumb(Category category)
        {
            if (category == null)
                throw new ArgumentNullException("category");

            var breadCrumb = new List<Category>();

            while (category != null //category is not null
                && !category.Deleted //category is not deleted
                && category.Published) //category is published
            {
                breadCrumb.Add(category);

                category = _categoryService.GetCategoryById(category.ParentCategoryId);
            }

            breadCrumb.Reverse();

            return breadCrumb;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "FeedBecome";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Feed.Become.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Generate a feed
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>Generated feed</returns>
        public void GenerateFeed(Stream stream, Store store)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (store == null)
                throw new ArgumentNullException("store");

            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("UPC;Mfr Part #;Manufacturer;Product URL;Image URL;Product Title;Product Description;Category;Price;Condition;Stock Status");

                var products1 = _productService.SearchProducts(storeId: store.Id, visibleIndividuallyOnly: true);

                foreach (var product1 in products1)
                {
                    var productsToProcess = new List<Product>();

                    switch (product1.ProductType)
                    {
                        case ProductType.SimpleProduct:
                            {
                                //simple product doesn't have child products
                                productsToProcess.Add(product1);
                            }
                            break;
                        case ProductType.GroupedProduct:
                            {
                                //grouped products could have several child products

                                var associatedProducts = _productService.GetAssociatedProducts(product1.Id, store.Id);

                                productsToProcess.AddRange(associatedProducts);
                            }
                            break;
                        default:
                            continue;
                    }
                    foreach (var product in productsToProcess)
                    {

                        var sku = product.Id.ToString("000000000000");
                        var productManufacturers = _manufacturerService.GetProductManufacturersByProductId(product.Id, false);
                        var manufacturerName = productManufacturers.Count > 0 ? productManufacturers[0].Manufacturer.Name : String.Empty;
                        var manufacturerPartNumber = product.ManufacturerPartNumber;
                        var productTitle = product.Name;
                        //TODO add a method for getting product URL (e.g. SEOHelper.GetProductUrl)
                        var productUrl = string.Format("{0}{1}", _webHelper.GetStoreLocation(false), product.GetSeName());

                        var imageUrl = string.Empty;
                        var pictures = _pictureService.GetPicturesByProductId(product.Id, 1);

                        //always use HTTP when getting image URL
                        imageUrl = pictures.Count > 0 
                            ? _pictureService.GetPictureUrl(pictures[0], _becomeSettings.ProductPictureSize, storeLocation: store.Url) 
                            : _pictureService.GetDefaultPictureUrl(_becomeSettings.ProductPictureSize, storeLocation: store.Url);

                        var description = product.FullDescription;
                        var currency = GetUsedCurrency();
                        var price = _currencyService.ConvertFromPrimaryStoreCurrency(product.Price, currency).ToString(new CultureInfo("en-US", false).NumberFormat);
                        var stockStatus = product.StockQuantity > 0 ? "In Stock" : "Out of Stock";
                        var category = "no category";

                        if (String.IsNullOrEmpty(description))
                        {
                            description = product.ShortDescription;
                        }

                        if (String.IsNullOrEmpty(description))
                        {
                            description = product.Name;
                        }

                        var productCategories = _categoryService.GetProductCategoriesByProductId(product.Id);

                        if (productCategories.Count > 0)
                        {
                            var firstCategory = productCategories[0].Category;

                            if (firstCategory != null)
                            {
                                var sb = new StringBuilder();

                                foreach (var cat in GetCategoryBreadCrumb(firstCategory))
                                {
                                    sb.AppendFormat("{0}>", cat.Name);
                                }

                                sb.Length -= 1;
                                category = sb.ToString();
                            }
                        }

                        productTitle = CommonHelper.EnsureMaximumLength(productTitle, 80);
                        productTitle = RemoveSpecChars(productTitle);

                        manufacturerPartNumber = RemoveSpecChars(manufacturerPartNumber);
                        manufacturerName = RemoveSpecChars(manufacturerName);

                        description = HtmlHelper.StripTags(description);
                        description = CommonHelper.EnsureMaximumLength(description, 250);
                        description = RemoveSpecChars(description);

                        category = RemoveSpecChars(category);

                        writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};New;{9}",
                            sku,
                            manufacturerPartNumber,
                            manufacturerName,
                            productUrl,
                            imageUrl,
                            productTitle,
                            description,
                            category,
                            price,
                            stockStatus);
                    }
                }
            }
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new BecomeSettings()
            {
                ProductPictureSize = 125
            };

            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.ClickHere", "Click here");
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.Currency", "Currency");
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.Currency.Hint", "Select the default currency that will be used to generate the feed.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.Generate", "Generate feed");
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.ProductPictureSize", "Product thumbnail image size");
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.ProductPictureSize.Hint", "The default size (pixels) for product thumbnail images.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Feed.Become.SuccessResult", "Become.com feed has been successfully generated. {0} to see generated feed");

            base.Install();
        }
        
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<BecomeSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Feed.Become.ClickHere");
            this.DeletePluginLocaleResource("Plugins.Feed.Become.Currency");
            this.DeletePluginLocaleResource("Plugins.Feed.Become.Currency.Hint");
            this.DeletePluginLocaleResource("Plugins.Feed.Become.Generate");
            this.DeletePluginLocaleResource("Plugins.Feed.Become.ProductPictureSize");
            this.DeletePluginLocaleResource("Plugins.Feed.Become.ProductPictureSize.Hint");
            this.DeletePluginLocaleResource("Plugins.Feed.Become.SuccessResult");

            base.Uninstall();
        }

        #endregion
    }
}

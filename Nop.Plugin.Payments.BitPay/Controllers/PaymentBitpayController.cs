using BitPayAPI;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Bitpay.Models;
using Nop.Plugin.Payments.BitPay;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.Bitpay.Controllers
{
    public class PaymentBitpayController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly BitpayPaymentSettings _bitpaySettings;
        private readonly HttpContextBase _httpContext;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;

        public PaymentBitpayController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            ILocalizationService localizationService, BitpayPaymentSettings bitpaySettings,
            HttpContextBase httpContext, IOrderService orderService, IOrderProcessingService orderProcessingService)
        {
            _workContext = workContext;
            _storeService = storeService;
            _settingService = settingService;
            _localizationService = localizationService;
            _bitpaySettings = bitpaySettings;
            _httpContext = httpContext;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var bitpaySettings = _settingService.LoadSetting<BitpayPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.UseSandbox = bitpaySettings.UseSandbox;
            model.TransactionSpeed = bitpaySettings.TransactionSpeed.ToSelectList();
            model.PairingCode = bitpaySettings.PairingCode;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(bitpaySettings, x => x.UseSandbox, storeScope);
                model.TransactionSpeed_OverrideForStore = _settingService.SettingExists(bitpaySettings, x => x.TransactionSpeed, storeScope);
                model.PairingCode_OverrideForStore = _settingService.SettingExists(bitpaySettings, x => x.PairingCode, storeScope);
            }

            return View("~/Plugins/Payments.Bitpay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var bitpaySettings = _settingService.LoadSetting<BitpayPaymentSettings>(storeScope);

            // pair client with server
            if (!model.PairingCode.Equals(bitpaySettings.PairingCode, StringComparison.InvariantCultureIgnoreCase))
            {
                var bitpay = new BitPayAPI.BitPay(envUrl: BitpayHelper.GetEnvironmentUrl(bitpaySettings));
                if (!bitpay.clientIsAuthorized(BitPayAPI.BitPay.FACADE_POS))
                {
                    bitpay.authorizeClient(model.PairingCode);
                }
            }

            //save settings
            bitpaySettings.PairingCode = model.PairingCode;
            bitpaySettings.UseSandbox = model.UseSandbox;
            bitpaySettings.TransactionSpeed = (TransactionSpeed)model.TransactionSpeedId;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            _settingService.SaveSettingOverridablePerStore(bitpaySettings, x => x.PairingCode, model.PairingCode_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bitpaySettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bitpaySettings, x => x.TransactionSpeed, model.TransactionSpeed_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.Bitpay/Views/PaymentInfo.cshtml");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        public ActionResult IPNHandler()
        {
            var stream = new StreamReader(_httpContext.Request.InputStream);
            var invoice = JsonConvert.DeserializeObject<Invoice>(stream.ReadToEnd());
            if (invoice == null)
                return Content("");

            var id = invoice.Id;

            var bitpay = new BitPayAPI.BitPay(envUrl: BitpayHelper.GetEnvironmentUrl(_bitpaySettings));
            invoice = bitpay.getInvoice(id);

            var order = _orderService.GetOrderById(int.Parse(invoice.PosData));
            if (order == null)
                return Content("");

            order.OrderNotes.Add(new OrderNote
            {
                CreatedOnUtc = DateTime.UtcNow,
                DisplayToCustomer = false,
                Note = $"Bitpay IPN handler. Incoming status is: {invoice.Status}"
            });
            _orderService.UpdateOrder(order);

            switch (invoice.Status)
            {
                case "new":
                    break;
                case "paid":
                case "confirmed":
                case "complete":
                    if (_orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        _orderProcessingService.MarkOrderAsPaid(order);
                    }
                    break;
                default:
                    break;
            }

            return Content("");
        }
    }
}
using BitPayAPI;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Bitpay.Controllers;
using Nop.Plugin.Payments.BitPay;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;

namespace Nop.Plugin.Payments.Bitpay
{
    public class BitpayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly BitpayPaymentSettings _bitpaySettings;
        private readonly HttpContextBase _httpContext;
        private readonly CurrencySettings _currencySettings;
        private readonly ICurrencyService _currencyService;
        private readonly IWebHelper _webHelper;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public BitpayPaymentProcessor(ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            BitpayPaymentSettings bitpaySettings, HttpContextBase httpContext,
            CurrencySettings currencySettings, ICurrencyService currencyService, IWebHelper webHelper,
            IOrderService orderService, ILogger logger)
        {
            _localizationService = localizationService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _settingService = settingService;
            _bitpaySettings = bitpaySettings;
            _httpContext = httpContext;
            _currencySettings = currencySettings;
            _currencyService = currencyService;
            _webHelper = webHelper;
            _orderService = orderService;
            _logger = logger;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var invoice = new Invoice
            {
                BuyerName = $"{postProcessPaymentRequest.Order.BillingAddress.FirstName} {postProcessPaymentRequest.Order.BillingAddress.LastName}",
                BuyerEmail = postProcessPaymentRequest.Order.BillingAddress.Email,
                BuyerAddress1 = postProcessPaymentRequest.Order.BillingAddress.Address1,
                BuyerAddress2 = postProcessPaymentRequest.Order.BillingAddress.Address2,
                BuyerCity = postProcessPaymentRequest.Order.BillingAddress.City,
                TransactionSpeed = _bitpaySettings.TransactionSpeed.ToString().ToLowerInvariant(),
                OrderId = postProcessPaymentRequest.Order.Id.ToString(),
                PosData = postProcessPaymentRequest.Order.Id.ToString(),
                Currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode,
                NotificationURL = $"{_webHelper.GetStoreLocation()}Plugins/PaymentBitpay/IPNHandler",
                RedirectURL = $"{_webHelper.GetStoreLocation()}orderdetails/{postProcessPaymentRequest.Order.Id}",
                FullNotifications = true,
                Price = Convert.ToDouble(postProcessPaymentRequest.Order.OrderTotal)
            };

            if (postProcessPaymentRequest.Order.BillingAddress.StateProvince != null)
                invoice.BuyerState = postProcessPaymentRequest.Order.BillingAddress.StateProvince.Abbreviation;
            if (postProcessPaymentRequest.Order.BillingAddress.Country != null)
                invoice.BuyerCountry = postProcessPaymentRequest.Order.BillingAddress.Country.TwoLetterIsoCode;

            var bitpay = new BitPayAPI.BitPay(envUrl: BitpayHelper.GetEnvironmentUrl(_bitpaySettings));
            
            try
            {
                invoice = bitpay.createInvoice(invoice);

                if (!string.IsNullOrEmpty(invoice.Id))
                {
                    postProcessPaymentRequest.Order.OrderNotes.Add(new OrderNote
                    {
                        CreatedOnUtc = DateTime.UtcNow,
                        DisplayToCustomer = false,
                        Note = $"Invoice initiated successfully.  Id: {invoice.Id}, Status: {invoice.Status}"
                    });

                    postProcessPaymentRequest.Order.AuthorizationTransactionId = invoice.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error creating invoice", ex);

                postProcessPaymentRequest.Order.OrderNotes.Add(new OrderNote
                {
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false,
                    Note = $"Error creating invoice: {ex.Message}"
                });
            }

            _orderService.UpdateOrder(postProcessPaymentRequest.Order);

            if (!string.IsNullOrEmpty(invoice.Id))
            {
                _httpContext.Response.Redirect(invoice.Url);
            }
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0.0M;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Not supported transaction type");

            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //always success
            return new CancelRecurringPaymentResult();
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentBitpay";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Bitpay.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentBitpay";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Bitpay.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Get the type of controller
        /// </summary>
        /// <returns>Type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentBitpayController);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new BitpayPaymentSettings
            {
                UseSandbox = true,
                TransactionSpeed = TransactionSpeed.High
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.Fields.PairingCode", "Pairing Code");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.Fields.PairingCode.Hint", "Pairing code to connect client to server. Found at bitpay.com/tokens");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.Fields.UseSandbox.Hint", "Go against the test environment");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.Fields.TransactionSpeed", "Transaction Speed");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.Fields.TransactionSpeed.Hint", "Speed at which the transaction gets confirmed");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Bitpay.PaymentMethodDescription", "Pay by using Bitcoin");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<BitpayPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.Fields.PairingCode");
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.Fields.PairingCode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.Fields.TransactionSpeed");
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.Fields.TransactionSpeed.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Bitpay.PaymentMethodDescription");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.Bitpay.PaymentMethodDescription"); }
        }

        #endregion

    }
}

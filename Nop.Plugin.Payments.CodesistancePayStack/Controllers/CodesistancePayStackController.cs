using Nop.Web.Framework.Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Services.Payments;
using System.Web.Routing;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.CodesistancePayStack.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Logging;
using PayStack.Net;

namespace Nop.Plugin.Payments.CodesistancePayStack.Controllers
{
    public class CodesistancePayStackController : BasePlugin, IPaymentMethod
    {
        #region Fields & Properties

        private readonly ISettingService _settingService;
        private readonly ICustomerService _customerService;
        private readonly HttpContextBase _httpContext;
        private readonly ILogger _logger;

        public bool SupportCapture => true;
        public bool SupportPartiallyRefund { get; }
        public bool SupportRefund { get; }
        public bool SupportVoid { get; }
        public RecurringPaymentType RecurringPaymentType { get; }
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;
        public bool SkipPaymentInfo => false;
        public override PluginDescriptor PluginDescriptor { get; set; }
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        #endregion

        public CodesistancePayStackController(ISettingService settingService, ICustomerService customerService, HttpContextBase httpContext, ILogger logger)
        {
            _settingService = settingService;
            _customerService = customerService;
            _httpContext = httpContext;
            _logger = logger;
        }

        #region Implemented Methods

        public override void Install()
        {
            var settings = new CodesistancePayStackSettings
            {
                PayStackApiKey = ConfigurationManager.AppSettings["PSSK"] ?? "sk_test_e4607c39d035a62381e96f5c5cb817bac9251786"
            };

            _settingService.SaveSetting(settings);
            base.Install();
        }

        public override void Uninstall()
        {
            _settingService.DeleteSetting<CodesistancePayStackSettings>();
            base.Uninstall();
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "ConfigurePayment";
            controllerName = "PaymentEngine";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.CodesistancePayStack.Controllers" }, { "area", null } };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentEngine";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.CodesistancePayStack.Controllers" }, { "area", null } };
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
            return result;
        }

        private PayStack.Net.TransactionInitializeResponse GetPaymentUrl(Order order)
        {
            var setting = _settingService.LoadSetting<CodesistancePayStackSettings>();

            var customer = _customerService.GetCustomerById(order.CustomerId);
            if (customer == null)
                throw new Exception("Customer cannot be loaded");

            var api = new PayStackApi(setting.PayStackApiKey);
            var transactionReference = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var customFields = new List<CustomField>
            {
                new CustomField("OrderGuid", "Order ID", order.OrderGuid.ToString()),
                new CustomField("OrderRef", "Order Reference", transactionReference + order.Id)
            };

            var callBackUrl =
                $"{_httpContext.Request.Url.Scheme}://{_httpContext.Request.Url.Authority}/Plugins/PaymentEngine/ValidatePaymentStatus";

            // Initializing a transaction
            var payStackResponse = api.Transactions.Initialize(new TransactionInitializeRequest()
            {
                AmountInKobo = (int) (order.OrderTotal*100),
                Email = order.Customer.Email,
                Reference = transactionReference,
                CustomFields = customFields,
                CallbackUrl = callBackUrl
            });

            _logger.InsertLog(payStackResponse.Status ? LogLevel.Information : LogLevel.Warning,
                "PayStack Initialization Status", payStackResponse.Message);

            if (payStackResponse.Status)
                return payStackResponse;

            throw new Exception(payStackResponse.Message);
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var urlInfo = GetPaymentUrl(postProcessPaymentRequest.Order);

            if (urlInfo.Status)
                _httpContext.Response.Redirect(urlInfo.Data.AuthorizationUrl);
        }

        #endregion Implemented Methods

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0.0m;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult();
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult();
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult();
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult();
        }

        public bool CanRePostProcessPayment(Order order)
        {
            return false;
        }

        public Type GetControllerType()
        {
            return typeof(PaymentEngineController);
        }
    }
}

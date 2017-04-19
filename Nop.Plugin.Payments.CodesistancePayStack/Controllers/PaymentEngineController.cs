using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.CodesistancePayStack.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using PayStack.Net;

namespace Nop.Plugin.Payments.CodesistancePayStack.Controllers
{
    public class PaymentEngineController: BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly PaymentSettings _paymentSettings;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;

        public PaymentEngineController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILogger logger,
            PaymentSettings paymentSettings,
            ILocalizationService localizationService, IWebHelper webHelper)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._logger = logger;
            this._paymentSettings = paymentSettings;
            this._localizationService = localizationService;
            _webHelper = webHelper;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult ConfigurePayment()
        {
            var model = _settingService.LoadSetting<CodesistancePayStackSettings>();
            return View("~/Plugins/Payments.CodesistancePayStack/Views/PaymentEngine/ConfigurePayment.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult ConfigurePayment(CodesistancePayStackSettings model)
        {
            var previousModel = _settingService.LoadSetting<CodesistancePayStackSettings>();

            if (string.IsNullOrEmpty(model.PayStackApiKey))
                return ConfigurePayment();

            _settingService.DeleteSetting(previousModel, x => x.PayStackApiKey);

            previousModel.PayStackApiKey = model.PayStackApiKey;

            _settingService.SaveSetting(previousModel);

            //now clear settings cache
            _settingService.ClearCache();

            return ConfigurePayment();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.CodesistancePayStack/Views/PaymentEngine/PaymentInfo.cshtml");
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

        [ValidateInput(false)]
        public ActionResult ValidatePaymentStatus()
        {
            var transactionRef = _webHelper.QueryString<string>("trxref");

            var setting = _settingService.LoadSetting<CodesistancePayStackSettings>();
            var payStackApi = new PayStackApi(setting.PayStackApiKey);
            var transaction = payStackApi.Transactions.Verify(transactionRef);

            if (transaction.Status)
            {
                var orderGuid = ((dynamic)transaction.Data.Metadata["custom_fields"])[0].value.ToString();
                if (orderGuid != null)
                {
                    var order = _orderService.GetOrderByGuid(new Guid(orderGuid));
                    order.OrderNotes.Add(new OrderNote()
                    {
                        Note = $"Transaction Returned with Message: {transaction.Message} | Reference: {transaction.Data.Reference}",
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                    order.AuthorizationTransactionId = transaction.Data.Authorization.AuthorizationCode;
                    order.OrderNotes.Add(new OrderNote()
                    {
                        Note = Newtonsoft.Json.JsonConvert.SerializeObject(transaction.Data.Authorization),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                    _orderService.UpdateOrder(order);

                    _orderProcessingService.MarkOrderAsPaid(order);
                    return RedirectToRoute("CheckoutCompleted", new {orderId = order.Id});
                }
                else
                    throw new Exception("Order Not Found");
            }
            else
                throw new Exception(transaction.Message);
        }
    }
}

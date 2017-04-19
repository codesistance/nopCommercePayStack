using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.CodesistancePayStack
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.CodesistancePayStack.ValidatePaymentStatus",
                 "Plugins/PaymentEngine/ValidatePaymentStatus",
                 new { controller = "PaymentEngine", action = "ValidatePaymentStatus" },
                 new[] { "Nop.Plugin.Payments.CodesistancePayStack.Controllers" }
            );
        }

        public int Priority => 0;
    }
}

using Nop.Web.Framework.Mvc.Routes;
using System.Web.Mvc;
using System.Web.Routing;

namespace Nop.Plugin.Payments.BitPay
{
    public class RouteProvider : IRouteProvider
    {
        public int Priority => 1;

        public void RegisterRoutes(RouteCollection routes)
        {
            //IPN
            routes.MapRoute("Plugin.Payments.Bitpay.IPNHandler",
                 "Plugins/PaymentBitpay/IPNHandler",
                 new { controller = "PaymentBitpay", action = "IPNHandler" },
                 new[] { "Nop.Plugin.Payments.BitPay.Controllers" }
            );
        }
    }
}

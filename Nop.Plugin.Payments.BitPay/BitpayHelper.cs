using Nop.Plugin.Payments.Bitpay;

namespace Nop.Plugin.Payments.BitPay
{
    public static class BitpayHelper
    {
        public static string GetEnvironmentUrl(BitpayPaymentSettings settings)
        {
            return string.IsNullOrEmpty(settings.CustomUrl)
                ? settings.UseSandbox ? "https://test.bitpay.com/" : "https://bitpay.com/"
                : settings.CustomUrl;
        }
    }
}

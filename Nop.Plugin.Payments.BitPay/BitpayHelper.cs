using Nop.Plugin.Payments.Bitpay;

namespace Nop.Plugin.Payments.BitPay
{
    public static class BitpayHelper
    {
        public static string GetEnvironmentUrl(BitpayPaymentSettings settings)
        {
            return settings.UseSandbox ? "https://test.bitpay.com/" : "https://bitpay.com/";
        }
    }
}

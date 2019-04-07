using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Bitpay
{
    public class BitpayPaymentSettings : ISettings
    {
        //public string ApiKey { get; set; }
        //public string ApiPub { get; set; }
        //public string ApiSin { get; set; }
        //public string ApiToken { get; set; }
        public string PairingCode { get; set; }
        public TransactionSpeed TransactionSpeed { get; set; }
        public bool UseSandbox { get; set; }
        public string CustomUrl { get; set; }
    }
}
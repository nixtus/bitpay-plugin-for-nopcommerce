using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Bitpay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        public int TransactionSpeedId { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Bitpay.Fields.TransactionSpeed")]
        public SelectList TransactionSpeed { get; set; }
        public bool TransactionSpeed_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Bitpay.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Bitpay.Fields.PairingCode")]
        public string PairingCode { get; set; }
        public bool PairingCode_OverrideForStore { get; set; }
    }
}
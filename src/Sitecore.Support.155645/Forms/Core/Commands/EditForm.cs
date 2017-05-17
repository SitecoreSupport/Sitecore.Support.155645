using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Web.UI.Sheer;
using System;
namespace Sitecore.Support.Forms.Core.Commands
{
    [Serializable]
    public class EditForm : Sitecore.Forms.Core.Commands.EditForm
    {
        protected new void CheckChanges(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            string lang = args.Parameters["contentlanguage"];
            if (!string.IsNullOrEmpty(lang))
            {
                using(new LanguageSwitcher(Language.Parse(lang)))
                {
                    base.CheckChanges(args);
                }
            }
        }
    }
}

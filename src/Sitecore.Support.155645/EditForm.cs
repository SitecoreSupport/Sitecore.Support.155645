using Newtonsoft.Json;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Commands.FormDesigner;
using Sitecore.Forms.Core.Data;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Shell.Applications.WebEdit.Commands;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using Sitecore.WFFM.Abstractions.Dependencies;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Xml;

namespace Sitecore.Forms.Core.Commands
{
    [Serializable]
    public class EditForm : Command
    {
        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            NameValueCollection nameValueCollection = new NameValueCollection();
            string value = context.Parameters["id"];
            bool flag = false;
            if (context.Items.Length > 0)
            {
                flag = FormItem.IsForm(context.Items[0]);
            }
            string formValue = Sitecore.Web.WebUtil.GetFormValue("scLayout");
            nameValueCollection["sclayout"] = formValue;
            if (!flag && !string.IsNullOrEmpty(formValue))
            {
                XmlDocument xmlDocument = JsonConvert.DeserializeXmlNode(formValue);
                if (xmlDocument.DocumentElement != null)
                {
                    string outerXml = xmlDocument.DocumentElement.OuterXml;
                    string text = Sitecore.Web.WebUtil.GetFormValue("scDeviceID");
                    ShortID shortID;
                    if (ShortID.TryParse(text, out shortID))
                    {
                        text = shortID.ToID().ToString();
                    }
                    RenderingDefinition renderingByUniqueId = LayoutDefinition.Parse(outerXml).GetDevice(text).GetRenderingByUniqueId(context.Parameters["referenceId"]);
                    if (renderingByUniqueId != null)
                    {
                        Sitecore.Web.WebUtil.SetSessionValue(StaticSettings.Mode, StaticSettings.DesignMode);
                        if (!string.IsNullOrEmpty(renderingByUniqueId.Parameters))
                        {
                            NameValueCollection nameValueCollection2 = StringUtil.ParseNameValueCollection(renderingByUniqueId.Parameters, '&', '=');
                            value = System.Web.HttpUtility.UrlDecode(nameValueCollection2["FormID"]);
                        }
                    }
                }
            }
            XmlDocument xmlDocument2 = JsonConvert.DeserializeXmlNode(formValue);
            string key = "PageDesigner";
            string outerXml2 = xmlDocument2.DocumentElement.OuterXml;
            Sitecore.Web.WebUtil.SetSessionValue(key, outerXml2);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            nameValueCollection["referenceid"] = context.Parameters["referenceId"];
            nameValueCollection["formId"] = value;
            nameValueCollection["checksave"] = (context.Parameters["checksave"] ?? "1");
            if (context.Items.Length > 0)
            {
                nameValueCollection["contentlanguage"] = context.Items[0].Language.ToString();
            }
            ClientPipelineArgs args = new ClientPipelineArgs(nameValueCollection);
            Context.ClientPage.Start(this, "CheckChanges", args);
        }

        protected void CheckChanges(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (args.IsPostBack)
            {
                if (args.Result == "yes")
                {
                    args.Parameters["save"] = "1";
                    args.IsPostBack = false;
                    Context.ClientPage.Start(this, "Run", args);
                    return;
                }
            }
            else
            {
                bool flag = false;
                if (args.Parameters["checksave"] != "0")
                {
                    FormItem form = FormItem.GetForm(args.Parameters["formId"]);
                    IEnumerable<PageEditorField> modifiedFields = this.GetModifiedFields(form);
                    if (modifiedFields.Count<PageEditorField>() > 0)
                    {
                        flag = true;
                        SheerResponse.Confirm(DependenciesManager.ResourceManager.Localize("ONE_OR_MORE_ITEMS_HAVE_BEEN_CHANGED"));
                        args.WaitForPostBack();
                    }
                }
                if (!flag)
                {
                    args.IsPostBack = false;
                    Context.ClientPage.Start(this, "Run", args);
                }
            }
        }

        protected void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!args.IsPostBack)
            {
                FormItem form = FormItem.GetForm(args.Parameters["formId"]);
                if (form == null)
                {
                    return;
                }
                if (!args.HasResult)
                {
                    if (args.Parameters["save"] == "1")
                    {
                        this.SaveFields(form);
                    }
                    string value = args.Parameters["referenceId"];
                    NameValueCollection nameValueCollection = new NameValueCollection();
                    nameValueCollection["formid"] = form.ID.ToString();
                    nameValueCollection["mode"] = StaticSettings.DesignMode;
                    nameValueCollection["db"] = form.Database.Name;
                    nameValueCollection["vs"] = form.Version.ToString();
                    nameValueCollection["referenceId"] = form.Version.ToString();
                    nameValueCollection["la"] = (args.Parameters["contentlanguage"] ?? form.Language.Name);
                    if (args.Parameters["referenceId"] != null)
                    {
                        nameValueCollection["hdl"] = value;
                    }
                    FormDialog formDialog = new FormDialog(form, DependenciesManager.ResourceManager);
                    formDialog.ShowModalDialog(nameValueCollection);
                    SheerResponse.DisableOutput();
                    args.WaitForPostBack();
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(args.Parameters["scLayout"]))
            {
                SheerResponse.SetAttribute("scLayoutDefinition", "value", args.Parameters["scLayout"]);
                string text = args.Parameters["referenceId"];
                if (!string.IsNullOrEmpty(text))
                {
                    text = "r_" + ID.Parse(text).ToShortID();
                }
                string text2 = args.Parameters["formId"];
                ID iD;
                if (ID.TryParse(text2, out iD))
                {
                    text2 = iD.ToShortID().ToString();
                }
                SheerResponse.Eval("window.parent.Sitecore.PageModes.ChromeManager.fieldValuesContainer.children().each(function(e){ if( window.parent.$sc('#form_" + text2.ToUpper() + "').find('#' + this.id + '_edit').size() > 0 ) { window.parent.$sc(this).remove() }});");
                SheerResponse.Eval("window.parent.Sitecore.PageModes.ChromeManager.handleMessage('chrome:rendering:propertiescompleted', {controlId : '" + text + "'});");
            }
        }

        private IEnumerable<PageEditorField> GetFields(NameValueCollection form)
        {
            Assert.ArgumentNotNull(form, "form");
            List<PageEditorField> list = new List<PageEditorField>();
            foreach (string text in form.Keys)
            {
                if (!string.IsNullOrEmpty(text) && (text.StartsWith("fld_", StringComparison.InvariantCulture) || text.StartsWith("flds_", StringComparison.InvariantCulture)))
                {
                    string text2 = text;
                    string text3 = form[text];
                    int num = text2.IndexOf('$');
                    if (num >= 0)
                    {
                        text2 = StringUtil.Left(text2, num);
                    }
                    string[] array = text2.Split(new char[]
                    {
                        '_'
                    });
                    ID iD = ShortID.DecodeID(array[1]);
                    ID fieldID = ShortID.DecodeID(array[2]);
                    Language language = Language.Parse(array[3]);
                    Sitecore.Data.Version version = Sitecore.Data.Version.Parse(array[4]);
                    string revision = array[5];
                    Item item = Client.ContentDatabase.GetItem(iD);
                    if (item != null)
                    {
                        Field field = item.Fields[fieldID];
                        if (text.StartsWith("flds_", StringComparison.InvariantCulture))
                        {
                            System.Web.UI.Page page = System.Web.HttpContext.Current.Handler as System.Web.UI.Page;
                            text3 = (string)Sitecore.Web.WebUtil.GetSessionValue(page.Request.Form[text]);
                            if (string.IsNullOrEmpty(text3))
                            {
                                text3 = field.Value;
                            }
                        }
                        string typeKey;
                        if ((typeKey = field.TypeKey) != null)
                        {
                            if (!(typeKey == "html") && !(typeKey == "rich text"))
                            {
                                if (!(typeKey == "text"))
                                {
                                    if (typeKey == "multi-line text" || typeKey == "memo")
                                    {
                                        text3 = StringUtil.RemoveTags(text3.Replace("<br>", "\r\n").Replace("<br/>", "\r\n").Replace("<br />", "\r\n").Replace("<BR>", "\r\n").Replace("<BR/>", "\r\n").Replace("<BR />", "\r\n"));
                                    }
                                }
                                else
                                {
                                    text3 = StringUtil.RemoveTags(text3);
                                }
                            }
                            else
                            {
                                text3 = text3.TrimEnd(new char[]
                                {
                                    ' '
                                });
                            }
                        }
                        PageEditorField item2 = new PageEditorField
                        {
                            ControlId = text2,
                            FieldID = fieldID,
                            ItemID = iD,
                            Language = language,
                            Revision = revision,
                            Value = text3,
                            Version = version
                        };
                        list.Add(item2);
                    }
                }
            }
            return list;
        }

        private IEnumerable<PageEditorField> GetModifiedFields(FormItem form)
        {
            List<PageEditorField> list = new List<PageEditorField>();
            if (form != null)
            {
                IEnumerable<PageEditorField> fields = this.GetFields(Context.ClientPage.Request.Form);
                foreach (PageEditorField current in fields)
                {
                    Item item = StaticSettings.ContextDatabase.GetItem(current.ItemID);
                    if (form.GetField(current.ItemID) != null || item.ID == form.ID)
                    {
                        string text = item[current.FieldID];
                        string value = current.Value;
                        if (string.Compare(value, text, StringComparison.OrdinalIgnoreCase) != 0 && string.CompareOrdinal(value.TrimWhiteSpaces(), text.TrimWhiteSpaces()) != 0)
                        {
                            list.Add(current);
                        }
                    }
                }
            }
            return list;
        }

        private void SaveFields(FormItem form)
        {
            IEnumerable<PageEditorField> modifiedFields = this.GetModifiedFields(form);
            foreach (PageEditorField current in modifiedFields)
            {
                Item item = StaticSettings.ContextDatabase.GetItem(current.ItemID);
                item.Editing.BeginEdit();
                item[current.FieldID] = current.Value;
                item.Editing.EndEdit();
            }
        }
    }
}
using Newtonsoft.Json;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Commands.FormDesigner;
using Sitecore.Forms.Core.Data;
using Sitecore.Layouts;
using Sitecore.Shell.Applications.WebEdit.Commands;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Web.UI.Sheer;
using Sitecore.WFFM.Abstractions.Dependencies;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Xml;
namespace Sitecore.Support.Forms.Core.Commands
{
  [Serializable]
  public class EditForm : WebEditCommand
  {
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
        }
      }
      else
      {
        bool flag = false;
        if (args.Parameters["checksave"] != "0")
        {
          //modified part of code:
          FormItem form = GetFormItem(args);
          //end of the modified part
          if (this.GetModifiedFields(form).Count<PageEditorField>() > 0)
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

    public override void Execute(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      NameValueCollection parameters = new NameValueCollection();
      string str = context.Parameters["id"];
      bool flag = false;
      if (context.Items.Length > 0)
      {
        flag = FormItem.IsForm(context.Items[0]);
      }
      string formValue = Web.WebUtil.GetFormValue("scLayout");
      parameters["sclayout"] = formValue;
      if (!flag && !string.IsNullOrEmpty(formValue))
      {
        ShortID tid;
        string xml = JsonConvert.DeserializeXmlNode(formValue).DocumentElement.OuterXml;
        string str4 = Web.WebUtil.GetFormValue("scDeviceID");
        if (ShortID.TryParse(str4, out tid))
        {
          str4 = tid.ToID().ToString();
        }
        RenderingDefinition renderingByUniqueId = LayoutDefinition.Parse(xml).GetDevice(str4).GetRenderingByUniqueId(context.Parameters["referenceId"]);
        if (renderingByUniqueId != null)
        {
          Web.WebUtil.SetSessionValue(StaticSettings.Mode, StaticSettings.DesignMode);
          if (!string.IsNullOrEmpty(renderingByUniqueId.Parameters))
          {
            str = HttpUtility.UrlDecode(StringUtil.ParseNameValueCollection(renderingByUniqueId.Parameters, '&', '=')["FormID"]);
          }
        }
      }
      XmlDocument document2 = JsonConvert.DeserializeXmlNode(formValue);
      string key = "PageDesigner";
      string outerXml = document2.DocumentElement.OuterXml;
      Web.WebUtil.SetSessionValue(key, outerXml);
      if (!string.IsNullOrEmpty(str))
      {
        parameters["referenceid"] = context.Parameters["referenceId"];
        parameters["formId"] = str;
        parameters["checksave"] = context.Parameters["checksave"] ?? "1";
        if (context.Items.Length > 0)
        {
          parameters["contentlanguage"] = context.Items[0].Language.ToString();
        }
        ClientPipelineArgs args = new ClientPipelineArgs(parameters);
        Context.ClientPage.Start(this, "CheckChanges", args);
      }
    }

    private IEnumerable<PageEditorField> GetModifiedFields(FormItem form)
    {
      List<PageEditorField> list = new List<PageEditorField>();
      if (form != null)
      {
        foreach (PageEditorField field in WebEditCommand.GetFields(Context.ClientPage.Request.Form))
        {
          //modified part of code: form.Language has been added to take into consideration language version
          Item item = StaticSettings.ContextDatabase.GetItem(field.ItemID, form.Language);
          //end of the modified part
          if ((form.GetField(field.ItemID) != null) || (item.ID == form.ID))
          {
            string strB = item[field.FieldID];
            string strA = field.Value;
            if ((string.Compare(strA, strB, true) != 0) && (string.Compare(strA.TrimWhiteSpaces(), strB.TrimWhiteSpaces()) != 0))
            {
              list.Add(field);
            }
          }
        }
      }
      return list;
    }

    protected void Run(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      if (!args.IsPostBack)
      {
        //modified part of code
        FormItem form = GetFormItem(args);
        //end of the modified part
        if ((form != null) && !args.HasResult)
        {
          if (args.Parameters["save"] == "1")
          {
            this.SaveFields(form);
          }
          string str = args.Parameters["referenceId"];
          NameValueCollection urlParameters = new NameValueCollection();
          urlParameters["formid"] = form.ID.ToString();
          urlParameters["mode"] = StaticSettings.DesignMode;
          urlParameters["db"] = form.Database.Name;
          urlParameters["vs"] = form.Version.ToString();
          urlParameters["referenceId"] = form.Version.ToString();
          urlParameters["la"] = args.Parameters["contentlanguage"] ?? form.Language.Name;
          if (args.Parameters["referenceId"] != null)
          {
            urlParameters["hdl"] = str;
          }
          new FormDialog(form, DependenciesManager.ResourceManager).ShowModalDialog(urlParameters);
          SheerResponse.DisableOutput();
          args.WaitForPostBack();
        }
      }
      else if (!string.IsNullOrEmpty(args.Parameters["scLayout"]))
      {
        ID id;
        SheerResponse.SetAttribute("scLayoutDefinition", "value", args.Parameters["scLayout"]);
        string str2 = args.Parameters["referenceId"];
        if (!string.IsNullOrEmpty(str2))
        {
          str2 = "r_" + ID.Parse(str2).ToShortID();
        }
        string str3 = args.Parameters["formId"];
        if (ID.TryParse(str3, out id))
        {
          str3 = id.ToShortID().ToString();
        }
        SheerResponse.Eval("window.parent.Sitecore.PageModes.ChromeManager.fieldValuesContainer.children().each(function(e){ if( window.parent.$sc('#form_" + str3.ToUpper() + "').find('#' + this.id + '_edit').size() > 0 ) { window.parent.$sc(this).remove() }});");
        // modified part of code: line has been commented out due to the "NS_ERROR_UNEXPECTED" error in a browser console. 
        //SheerResponse.Eval("window.parent.Sitecore.PageModes.ChromeManager.handleMessage('chrome:rendering:propertiescompleted', {controlId : '" + str2 + "'});");
        // Page reload() has been added to overcome the issue with Context.ClientPage.Request.Form when checking modified fields
        SheerResponse.Eval("window.parent.location.reload();");
        //end of the modified part
      }
    }

    private void SaveFields(FormItem form)
    {
      foreach (PageEditorField field in this.GetModifiedFields(form))
      {
        //modified part of code: form.Language has been added to take into consideration language version
        Item item = StaticSettings.ContextDatabase.GetItem(field.ItemID, form.Language);
        //end of the modified part
        item.Editing.BeginEdit();
        item[field.FieldID] = field.Value;
        item.Editing.EndEdit();
      }
    }

    //modified part of code: GetFormItem method has been implemented to get FormItem in a correct language version
    private FormItem GetFormItem(ClientPipelineArgs args)
    {
      FormItem form = null;
      Globalization.Language contentLanguage = null;

      string tmpFormId = args.Parameters["formId"];
      string tmpLanguage = args.Parameters["contentlanguage"];

      if (!string.IsNullOrEmpty(tmpLanguage) && Globalization.Language.TryParse(tmpLanguage, out contentLanguage))
      {
        if (!string.IsNullOrEmpty(tmpFormId))
        {
          form = new FormItem(StaticSettings.ContextDatabase.GetItem(tmpFormId, contentLanguage));
        }
      }

      if (form == null)
      {
        form = FormItem.GetForm(tmpFormId);
      }
      return form;
    }
  }
}

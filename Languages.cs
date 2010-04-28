using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Winterdom.VisualStudio.Extensions.Text {
  interface IMarkupLanguage {
    bool IsDelimiter(String tagName);
    bool IsName(String tagName);
    bool IsAttribute(String tagName);
  }
  class XmlMarkup : IMarkupLanguage {
    public bool IsDelimiter(String tagName) {
      return tagName == "XML Delimiter";
    }
    public bool IsName(String tagName) {
      return tagName == "XML Name";
    }
    public bool IsAttribute(String tagName) {
      return tagName == "XML Attribute";
    }
  }
  class XamlMarkup : IMarkupLanguage {
    public bool IsDelimiter(String tagName) {
      return tagName == "XAML Delimiter";
    }
    public bool IsName(String tagName) {
      return tagName == "XAML Name";
    }
    public bool IsAttribute(String tagName) {
      return tagName == "XAML Attribute";
    }
  }
  class HtmlMarkup : IMarkupLanguage {
    public bool IsDelimiter(String tagName) {
      return tagName == "HTML Tag Delimiter" || tagName == "HTML Operator";
    }
    public bool IsName(String tagName) {
      return tagName == "HTML Element Name";
    }
    public bool IsAttribute(String tagName) {
      return tagName == "HTML Attribute Name";
    }
  }
}

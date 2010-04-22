﻿using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Winterdom.VisualStudio.Extensions.Text {
  internal static class Constants {
    public const string CT_XML = "XML";
    public const string CT_XAML = "XAML";
    public const string XML_CLOSING = "XMLCloseTag";
    public const string XML_PREFIX = "XMLPrefix";
  }
  internal static class XmlClosingTagClassificationDefinition {
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(Constants.XML_CLOSING)]
    internal static ClassificationTypeDefinition XmlClosingType = null;
  }
  internal static class XmlPrefixClassificationDefinition {
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(Constants.XML_PREFIX)]
    internal static ClassificationTypeDefinition XmlPrefixType = null;
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = Constants.XML_CLOSING)]
  [Name(Constants.XML_CLOSING)]
  [UserVisible(true)]
  [Order(Before = Priority.High)]
  internal sealed class XmlClosingTagFormat : ClassificationFormatDefinition {
    public XmlClosingTagFormat() {
      this.DisplayName = "XML Closing Tag";
      this.ForegroundColor = Colors.LightBlue;
    }
  }
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = Constants.XML_PREFIX)]
  [Name(Constants.XML_PREFIX)]
  [UserVisible(true)]
  [Order(Before = Priority.High)]
  internal sealed class XmlPrefixFormat : ClassificationFormatDefinition {
    public XmlPrefixFormat() {
      this.DisplayName = "XML Prefix";
      this.ForegroundColor = Colors.ForestGreen;
    }
  }
}

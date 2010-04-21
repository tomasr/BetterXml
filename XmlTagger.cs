using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Winterdom.VisualStudio.Extensions.Text {

  [Export(typeof(IViewTaggerProvider))]
  [ContentType("XML")]
  [TagType(typeof(ClassificationTag))]
  public class XmlTaggerProvider : IViewTaggerProvider {
    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null;
    [Import]
    internal IBufferTagAggregatorFactoryService Aggregator = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
      return new XmlTagger(
         ClassificationRegistry,
         Aggregator.CreateTagAggregator<ClassificationTag>(buffer)
      ) as ITagger<T>;
    }
  }

  class XmlTagger : ITagger<ClassificationTag> {
    private ClassificationTag xmlCloseTagClassification;
    private ClassificationTag xmlPrefixClassification;
    private ITagAggregator<ClassificationTag> aggregator;
#pragma warning disable 67
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

    internal XmlTagger(
        IClassificationTypeRegistryService registry,
        ITagAggregator<ClassificationTag> aggregator) {
      xmlCloseTagClassification =
         new ClassificationTag(registry.GetClassificationType(Constants.XML_CLOSING));
      xmlPrefixClassification =
         new ClassificationTag(registry.GetClassificationType(Constants.XML_PREFIX));
      this.aggregator = aggregator;
    }
    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      if ( spans.Count == 0 ) {
        yield break;
      }
      ITextSnapshot snapshot = spans[0].Snapshot;

      bool foundClosingTag = false;

      foreach ( var tagSpan in aggregator.GetTags(spans) ) {
        String tagName = tagSpan.Tag.ClassificationType.Classification;
        var cs = tagSpan.Span.GetSpans(snapshot)[0];
        if ( tagName == "XML Delimiter" ) {
          if ( cs.GetText().EndsWith("</") ) {
            foundClosingTag = true;
          }
        } else if ( tagName == "XML Name" ) {
          String text = cs.GetText();
          int colon = text.IndexOf(':');
          if ( colon < 0 && foundClosingTag ) {
            yield return new TagSpan<ClassificationTag>(cs, xmlCloseTagClassification);
          } else if ( colon > 0 ) {
            string prefix = text.Substring(0, colon);
            string name = text.Substring(colon);
            yield return new TagSpan<ClassificationTag>(
              new SnapshotSpan(cs.Start, prefix.Length), xmlPrefixClassification);
            if ( foundClosingTag ) {
              yield return new TagSpan<ClassificationTag>(new SnapshotSpan(
                cs.Start.Add(prefix.Length), name.Length), xmlCloseTagClassification);
            }
          }
          foundClosingTag = false;
        }
      }
    }
  }
}

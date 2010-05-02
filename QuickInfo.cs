using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Winterdom.VisualStudio.Extensions.Text {
  [Export(typeof(IQuickInfoSourceProvider))]
  [Name("BetterXml QuickInfo Provider")]
  [Order(Before = "Default Quick Info Presenter")]
  [ContentType(Constants.CT_XML)]
  [ContentType(Constants.CT_XAML)]
  internal class XmlQuickInfoSourceProvider : IQuickInfoSourceProvider {
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }
    [Import]
    internal IViewTagAggregatorFactoryService AggregatorFactory { get; set; }

    public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
      return new XmlQuickInfoSource(textBuffer, this);
    }
  }

  internal class XmlQuickInfoSource : IQuickInfoSource {
    private ITextBuffer textBuffer;
    private XmlQuickInfoSourceProvider provider;

    public XmlQuickInfoSource(ITextBuffer buffer, XmlQuickInfoSourceProvider provider) {
      this.textBuffer = buffer;
      this.provider = provider;
    }
    public void AugmentQuickInfoSession(
        IQuickInfoSession session, IList<object> quickInfoContent,
        out ITrackingSpan applicableToSpan) {

      applicableToSpan = null;
      SnapshotPoint? subjectTriggerPoint =
        session.GetTriggerPoint(textBuffer.CurrentSnapshot);
      if ( !subjectTriggerPoint.HasValue ) {
        return;
      }
      ITextSnapshot currentSnapshot = subjectTriggerPoint.Value.Snapshot;
      SnapshotSpan querySpan = new SnapshotSpan(subjectTriggerPoint.Value, 0);

      var tagAggregator = GetAggregator(session);
      ITextStructureNavigator navigator =
        provider.NavigatorService.GetTextStructureNavigator(textBuffer);
      TextExtent extent = navigator.GetExtentOfWord(subjectTriggerPoint.Value);

      if ( CheckForPrefixTag(tagAggregator, extent.Span) ) {
        string text = extent.Span.GetText();
        string url = "unknown";
        applicableToSpan = currentSnapshot.CreateTrackingSpan(
          extent.Span, SpanTrackingMode.EdgeInclusive
        );
        quickInfoContent.Add(String.Format("{0}: {1}", text, url));
      }
    }

    private bool CheckForPrefixTag(
        ITagAggregator<ClassificationTag> tagAggregator,
        SnapshotSpan span) {
      foreach ( var tagSpan in tagAggregator.GetTags(span) ) {
        String tagName = tagSpan.Tag.ClassificationType.Classification;
        if ( tagName == Constants.XML_PREFIX ) {
          return true;
        }
      }
      return false;
    }

    private ITagAggregator<ClassificationTag> GetAggregator(IQuickInfoSession session) {
      return provider.AggregatorFactory.CreateTagAggregator<ClassificationTag>(
        session.TextView
      );
    }

    public void Dispose() {
    }
  }
}

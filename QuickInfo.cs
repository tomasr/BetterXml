﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Xml;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.IO;

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
        string url = FindNSUri(extent.Span, GetDocText(extent.Span));
        applicableToSpan = currentSnapshot.CreateTrackingSpan(
          extent.Span, SpanTrackingMode.EdgeInclusive
        );
        quickInfoContent.Add(String.Format("{0}: {1}", text, url));
      }
    }

    private String FindNSUri(SnapshotSpan span, String docText) {
      String subtext = docText;
      int endElem = docText.IndexOf('>', span.Span.End);
      if ( endElem > 0 && endElem < docText.Length - 1) {
        subtext = docText.Substring(0, endElem+1); 
      }
      StringReader sr = new StringReader(subtext);
      XmlReaderSettings settings = new XmlReaderSettings();
      settings.ConformanceLevel = ConformanceLevel.Fragment;
      XmlReader reader = XmlReader.Create(sr, settings);
      String thisPrefix = span.GetText();
      String lastUriForPrefix = null;
      try {
        while ( reader.Read() ) {
          if ( reader.Prefix == thisPrefix ) {
            lastUriForPrefix = reader.NamespaceURI;
          }
        }
      } catch ( Exception ex ) {
      }
      return String.IsNullOrEmpty(lastUriForPrefix) ? "unknown" : lastUriForPrefix;
    }
    private String GetDocText(SnapshotSpan span) {
      return span.Snapshot.GetText();
    }
    private bool CheckForPrefixTag(
        ITagAggregator<ClassificationTag> tagAggregator,
        SnapshotSpan span) {
      foreach ( var tagSpan in tagAggregator.GetTags(span) ) {
        String tagName = tagSpan.Tag.ClassificationType.Classification;
        if ( tagName == Constants.XML_PREFIX ) {
          String text = span.GetText();
          if ( text.StartsWith("<") || text.Contains(":") ) {
            return false;
          }
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

  internal class CountingStringReader : StringReader {
    public int Position { get; private set; }
    public CountingStringReader(String text) : base(text) {
    }
    public override int Read() {
      int val = base.Read();
      if ( val >= 0 ) Position++;
      return val;
    }
    public override int Read(char[] buffer, int index, int count) {
      int read = base.Read(buffer, index, count);
      Position += read;
      return read;
    }
  }
}

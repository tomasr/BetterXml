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
using System.Xml;

namespace Winterdom.VisualStudio.Extensions.Text {

  [Export(typeof(IViewTaggerProvider))]
  [ContentType(Constants.CT_XML)]
  [TagType(typeof(TextMarkerTag))]
  public class XmlTagMatchingTaggerProvider : IViewTaggerProvider {
    [Import]
    internal IBufferTagAggregatorFactoryService Aggregator = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
      if ( textView == null ) return null;
      if ( textView.TextBuffer != buffer ) return null;
      return new XmlTagMatchingTagger(
          textView, buffer,
          Aggregator.CreateTagAggregator<IClassificationTag>(buffer)
        ) as ITagger<T>;
    }
  }

  public class XmlTagMatchingTagger : ITagger<TextMarkerTag> {
    private ITextView theView;
    private ITextBuffer theBuffer;
    private SnapshotSpan? currentSpan;
    private ITagAggregator<IClassificationTag> aggregator;
    private IMarkupLanguage language;

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public XmlTagMatchingTagger(ITextView textView, ITextBuffer buffer, ITagAggregator<IClassificationTag> aggregator) {
      this.theView = textView;
      this.theBuffer = buffer;
      this.aggregator = aggregator;
      this.currentSpan = null;

      this.language = new XmlMarkup();

      this.theView.Caret.PositionChanged += CaretPositionChanged;
      this.theView.LayoutChanged += ViewLayoutChanged;
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      if ( spans.Count == 0 ) yield break;

      if ( !currentSpan.HasValue ) yield break;

      SnapshotSpan current = currentSpan.Value;

      if ( current.Snapshot != spans[0].Snapshot ) {
        current = current.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgePositive);
      }

      SnapshotSpan currentTag = CompleteTag(current);
      String text = currentTag.GetText();
      // avoid processing statements
      if ( text.Contains('?') ) yield break;

      String searchFor = null;
      SnapshotSpan? complementTag = null;
      if ( text.StartsWith("</") ) {
        searchFor = "<" + current.GetText();
      } else {
        searchFor = "</" + current.GetText() + ">";
        currentTag = ExtendOpeningTag(currentTag);
        complementTag = FindClosingTag(current.Snapshot, currentTag.Start, searchFor);
      }

      yield return new TagSpan<TextMarkerTag>(currentTag, new TextMarkerTag("bracehighlight"));

      if ( complementTag.HasValue ) {
        yield return new TagSpan<TextMarkerTag>(complementTag.Value, new TextMarkerTag("bracehighlight"));
      }
    }

    private SnapshotSpan ExtendOpeningTag(SnapshotSpan currentTag) {
      var snapshot = currentTag.Snapshot;
      int end = -1;
      String currentQuote = null;
      for ( int i = currentTag.Start; i < snapshot.Length; i++ ) {
        String ch = snapshot.GetText(i, 1);
        if ( currentQuote == null ) {
          if ( ch == "\"" || ch == "'" ) {
            currentQuote = ch;
          } else {
            if ( ch == ">" ) {
              end = i;
              break;
            }
          }
        } else {
          if ( ch == currentQuote ) {
            currentQuote = null;
          }
        }
      }
      if ( end > currentTag.Start ) {
        return new SnapshotSpan(snapshot, currentTag.Start, end - currentTag.Start + 1);
      }
      return currentTag;
    }

    private SnapshotSpan? FindClosingTag(ITextSnapshot snapshot, int searchStart, string searchFor) {
      String textToSearch = snapshot.GetText(searchStart, snapshot.Length - searchStart);

      CountingStringReader csr = new CountingStringReader(textToSearch);
      XmlReaderSettings settings = new XmlReaderSettings();
      settings.ConformanceLevel = ConformanceLevel.Fragment;
      settings.IgnoreWhitespace = false;
      using ( XmlReader reader = XmlReader.Create(csr, settings) ) {
        try {
          reader.Read();
          if ( !reader.IsEmptyElement ) {
            reader.Skip();
            int wsae = 0;
            for ( int i = csr.Position - 2; i > 0 && Char.IsWhiteSpace(textToSearch[i]); i-- ) {
              wsae++;
            }
            return new SnapshotSpan(snapshot, searchStart + csr.Position - searchFor.Length - wsae - 1, searchFor.Length);
          } else {
            return new SnapshotSpan(snapshot, searchStart + csr.Position - 2, 2);
          }
        } catch {
        }
      }
      return null;
    }

    private SnapshotSpan CompleteTag(SnapshotSpan current) {
      var snapshot = current.Snapshot;
      int start = current.Start - 1;
      int end = current.End + 1;
      while ( snapshot.GetText(start, 1) != "<" ) {
        start--;
      }

      return new SnapshotSpan(snapshot, start, end - start);
    }


    private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
      if ( e.NewSnapshot != e.OldSnapshot ) {
        UpdateAtCaretPosition(theView.Caret.Position);
      }
    }

    private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
      UpdateAtCaretPosition(e.NewPosition);
    }

    private void UpdateAtCaretPosition(CaretPosition caretPosition) {
      var point = caretPosition.Point.GetPoint(theBuffer, caretPosition.Affinity);
      if ( !point.HasValue )
        return;

      // get the tag beneath our position:
      this.currentSpan = GetTagAtPoint(point.Value);

      var tempEvent = TagsChanged;
      if ( tempEvent != null )
        tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(theBuffer.CurrentSnapshot, 0,
            theBuffer.CurrentSnapshot.Length)));
    }

    private SnapshotSpan? GetTagAtPoint(SnapshotPoint point) {
      SnapshotSpan testSpan = new SnapshotSpan(point.Snapshot, new Span(point.Position - 1, 2));

      foreach ( var tagSpan in aggregator.GetTags(testSpan) ) {
        String tagName = tagSpan.Tag.ClassificationType.Classification;
        if ( !language.IsName(tagName) ) continue;
        foreach ( var span in tagSpan.Span.GetSpans(point.Snapshot.TextBuffer) ) {
          if ( span.Contains(point.Position) ) {
            return span;
          }
        }
      }
      return null;
    }
  }

}
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
using Sgml;
using System.IO;
using System.Diagnostics;

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
      // avoid processing statements or xml declarations
      if ( text.Contains('?') ) yield break;

      String searchFor = null;
      SnapshotSpan? complementTag = null;
      if ( text.StartsWith("</") ) {
        searchFor = "<" + current.GetText();
        // TODO: search for the opening tag
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

    // Extend the opening tag all the way to the >, even if
    // it has multiple attributes and what not.
    private SnapshotSpan ExtendOpeningTag(SnapshotSpan currentTag) {
      var snapshot = currentTag.Snapshot;
      int end = -1;
      String currentQuote = null;
      for ( int i = currentTag.Start; i < snapshot.Length; i++ ) {
        String ch = snapshot.GetText(i, 1);
        if ( currentQuote == null ) {
          if ( ch == "\"" || ch == "'" ) {
            currentQuote = ch;
          } else if ( ch == ">" ) {
            end = i;
            break;
          }
        } else if ( ch == currentQuote ) {
          currentQuote = null;
        }
      }
      if ( end > currentTag.Start ) {
        return new SnapshotSpan(snapshot, currentTag.Start, end - currentTag.Start + 1);
      }
      return currentTag;
    }

    private SnapshotSpan? FindClosingTag(ITextSnapshot snapshot, int searchStart, string searchFor) {
      String textToSearch = snapshot.GetText(searchStart, snapshot.Length - searchStart);

      using ( SgmlReader reader = new SgmlReader() ) {
        reader.InputStream = new StringReader(textToSearch);
        reader.WhitespaceHandling = WhitespaceHandling.All;
        try {
          reader.Read();
          if ( !reader.IsEmptyElement ) {
            // skip all the internal nodes, until the end
            while ( reader.Read() ) {
              if ( reader.NodeType == XmlNodeType.EndElement && reader.Depth == 1 )
                break;
            }
            // calculate the new position based on the number of lines
            // read in the SgmlReader + the position within that line.
            // Note that if there is whitespace after the closing tag
            // we'll be positioned on it, so we need to keep track of that.
            var origLine = snapshot.GetLineFromPosition(searchStart);
            int startOffset = searchStart - origLine.Start.Position;
            int newStart = 0;
            // tag is on same position as the opening one
            if ( reader.LineNumber == 1 ) {
              var line = snapshot.GetLineFromPosition(searchStart);
              newStart = line.Start.Position + startOffset + reader.LinePosition - 2;
            } else {
                int newLineNum = origLine.LineNumber + reader.LineNumber - 1;
                var newLine = snapshot.GetLineFromLineNumber(newLineNum);
                newStart = newLine.Start.Position + reader.LinePosition - 1;
            }
            newStart -= reader.Name.Length + 3; // </ + element + >

            SnapshotSpan? newSpan = new SnapshotSpan(snapshot, newStart, searchFor.Length);
            if ( newSpan.Value.GetText() != searchFor ) {
              Trace.WriteLine(String.Format("Searching for '{0}', but found '{1}'.", searchFor, newSpan.Value.GetText()));
              //newSpan = null;
            }
            return newSpan;
          }
        } catch (Exception ex) {
          Trace.WriteLine(String.Format("Exception while parsing document: {0}.", ex.ToString()));
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
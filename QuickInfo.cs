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
    internal ITextBufferFactoryService TextBufferFactoryService { get; set; }
    
    public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
      return new XmlQuickInfoSource(textBuffer, NavigatorService);
    }
  }

  [Export(typeof(IIntellisenseControllerProvider))]
  [Name("BetterXml QuickInfo Controller")]
  [ContentType(Constants.CT_XML)]
  [ContentType(Constants.CT_XAML)]
  internal class XmlQuickInfoControllerProvider : IIntellisenseControllerProvider {
    [Import]
    internal IQuickInfoBroker QuickInfoBroker { get; set; }
  
    public IIntellisenseController TryCreateIntellisenseController(
        ITextView textView, IList<ITextBuffer> subjectBuffers) {
      return new XmlQuickInfoController(textView, subjectBuffers, QuickInfoBroker);
    }
  }

  internal class XmlQuickInfoController : IIntellisenseController {
    private ITextView textView;
    private IList<ITextBuffer> textBuffers;
    private IQuickInfoSession session;
    private IQuickInfoBroker quickInfoBroker;

    internal XmlQuickInfoController(
        ITextView textView, IList<ITextBuffer> textBuffers,
        IQuickInfoBroker quickInfoBroker) {
      this.textView = textView;
      this.textBuffers = textBuffers;
      this.quickInfoBroker = quickInfoBroker;

      textView.MouseHover += OnTextViewMouseHover;
    }
    public void Detach(ITextView textView) {
      if ( this.textView == textView ) {
        textView.MouseHover -= this.OnTextViewMouseHover;
        textView = null;
      }
    }
    public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
      textBuffers.Add(subjectBuffer);
    }

    public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
      textBuffers.Remove(subjectBuffer);
    }
    void OnTextViewMouseHover(object sender, MouseHoverEventArgs e) {
      SnapshotPoint? point = textView.BufferGraph.MapDownToFirstMatch(
        new SnapshotPoint(textView.TextSnapshot, e.Position),
        PointTrackingMode.Positive,
        snapshot => textBuffers.Contains(snapshot.TextBuffer),
        PositionAffinity.Predecessor
      );
      if ( point != null ) {
        ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(
          point.Value.Position, PointTrackingMode.Positive);
        if ( quickInfoBroker.IsQuickInfoActive(textView) ) {
          session = quickInfoBroker.TriggerQuickInfo(textView, triggerPoint, true);
        }
      }
    }
  }

  internal class XmlQuickInfoSource : IQuickInfoSource {
    private ITextBuffer textBuffer;
    private ITextStructureNavigatorSelectorService navSelector;

    public XmlQuickInfoSource(ITextBuffer buffer, ITextStructureNavigatorSelectorService nav) {
      this.textBuffer = buffer;
      this.navSelector = nav;
    }
    public void AugmentQuickInfoSession(
        IQuickInfoSession session, IList<object> quickInfoContent, 
        out ITrackingSpan applicableToSpan) {

      SnapshotPoint? subjectTriggerPoint = 
        session.GetTriggerPoint(textBuffer.CurrentSnapshot);
      if ( !subjectTriggerPoint.HasValue ) {
        applicableToSpan = null;
        return;
      }
      ITextSnapshot currentSnapshot = subjectTriggerPoint.Value.Snapshot;
      SnapshotSpan querySpan = new SnapshotSpan(subjectTriggerPoint.Value, 0);

      ITextStructureNavigator navigator = navSelector.GetTextStructureNavigator(textBuffer);
      TextExtent extent = navigator.GetExtentOfWord(subjectTriggerPoint.Value);
      string searchText = extent.Span.GetText();

      applicableToSpan = currentSnapshot.CreateTrackingSpan(
        extent.Span, SpanTrackingMode.EdgeInclusive
      );
      quickInfoContent.Add("This is a tooltip");
    }

    public void Dispose() {
    }
  }
}

# BetterXml VS2010 Extension

This is a sample extension for Visual Studio 2010 that improves
the XML editing experience.

## Syntax Highlighting

1. Highlighting XML closing tags with a format different
from opening tags. Adjust the "XML Closing Tag" classification
 format to configure it.

2. Highlighting XML prefixes on attributes and tags with a new format,
configured using the "XML Prefix" classification format.

Both features are supported on XML, XAML and HTML documents.

![classification sample](http://github.com/tomasr/BetterXml/raw/master/preview.png)

## Namespace Tooltips

Tooltips showing the namepace URI associated with a prefix when you hover
the mouse over the prefix.

![tooltips sample](http://github.com/tomasr/BetterXml/raw/master/img/tooltips.png)

This can be a bit slow at times because right now it requires parsing
the document partially. This feature is only supported for XML and XAML
documents.

## Matching Element Tags

When the caret is over an XML element, the element opening and closing tags will be highlighted.

![highlight sample](http://github.com/tomasr/BetterXml/raw/master/img/tag_hilight.png)

## Installation

1. Install the Visual Studio 2010 SDK. You'll need it for building custom
   extensions.
2. Open the solution in Visual Studio 2010 and build it.
3. Close all open VS2010 instances.
4. Using Windows Explorer, navigate to the project's output folder and double
   click on the BetterXml.vsix generated.

That's it!

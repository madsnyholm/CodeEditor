using System.Linq;
using CodeEditor.Composition;

namespace CodeEditor.Text.UI.Unity.Engine.Implementation
{
	[Export(typeof(ITextViewMarginsFactory))]
	class TextViewMarginsFactory : ITextViewMarginsFactory
	{
		[ImportMany]
		public ITextViewMarginProvider[] _textViewMarginProviders {get; set;}

		public ITextViewMargins MarginsFor(ITextView textView)
		{
			return new TextViewMargins(_textViewMarginProviders.Select(p => p.MarginFor(textView)).Where(m => m!=null));
		}
	}
}

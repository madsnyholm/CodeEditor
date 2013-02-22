using CodeEditor.Composition;
using UnityEngine;

namespace CodeEditor.Text.UI.Unity.Engine.Implementation
{
	[Export(typeof(ITextViewAdornments))]
	class TextViewAdornments : ITextViewAdornments
	{
		[ImportMany] 
		public ITextViewAdornment[] _adornments {get; set;}

		public void Draw(ITextViewLine line, Rect lineRect)
		{
			foreach (var adornment in _adornments)
				adornment.Draw(line, lineRect);
		}
	}
}

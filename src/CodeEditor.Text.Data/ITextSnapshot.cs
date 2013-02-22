namespace CodeEditor.Text.Data
{
	public interface ITextSnapshot
	{
		char this[int position] { get; }
		string Text { get; }
		int Length { get; }
		ITextBuffer Buffer { get; }
		ITextSnapshotLines Lines { get; }

		string GetText(Span span);
		TextSpan GetTextSpan(int position, int length);
		int LineNumberForPosition(int position);
		string GetText(int start, int length);
	}

	public static class TextSnapshotExtensions
	{
		public static TextSpan GetTextSpan(this ITextSnapshot snapshot, Span span)
		{
			return snapshot.GetTextSpan(span.Start, span.Length);
		}

		public static TextSpan GetTextSpan(this ITextSnapshot snapshot)
		{
			return snapshot.GetTextSpan(0, snapshot.Length);
		}
	}
}

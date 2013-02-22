using System;

namespace CodeEditor.Text.Data
{
	public interface ITextBuffer : IPropertyOwner
	{
		IContentType ContentType { get; }
		ITextSnapshot CurrentSnapshot { get; }

		void Insert(int position, string text);
		void Delete(int start, int length);

		event TextChange Changed;
		void RevertTo(ITextSnapshot textSnapshot);
	}

	public static class ITextBufferExtensions
	{
		public static void Append(this ITextBuffer buffer, string text)
		{
			buffer.Insert(buffer.CurrentSnapshot.Length, text);
		}
	}
}

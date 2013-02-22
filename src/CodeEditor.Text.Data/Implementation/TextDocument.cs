using CodeEditor.IO;

namespace CodeEditor.Text.Data.Implementation
{
	internal class TextDocument : ITextDocument
	{
		private readonly IFile _file;
		private readonly ITextBuffer _buffer;

		public TextDocument(IFile file, IContentType contentType)
		{
			_file = file;
			_buffer = new TextBuffer(file.ReadAllText(), contentType);
		}

		public IFile File
		{
			get { return _file; }
		}

		public ITextBuffer Buffer
		{
			get { return _buffer; }
		}

		public void Save()
		{
			_file.WriteAllText(_buffer.CurrentSnapshot.Text);
		}

		public void Undo()
		{
			new UndoServiceFactory().ForTextBuffer(_buffer).Undo();
		}
	}

	public class UndoServiceFactory
	{
		public IUndoService ForTextBuffer(ITextBuffer textBuffer)
		{
			return textBuffer.Properties.GetOrCreateSingletonProperty<IUndoService>(() => (IUndoService)new UndoService(textBuffer));
		}

		private class UndoService : IUndoService
		{
			private readonly ITextBuffer _textBuffer;
			private ITextSnapshot _lastSnapshot;

			public UndoService(ITextBuffer TextBuffer)
			{
				_textBuffer = TextBuffer;
				_textBuffer.Changed += new TextChange(_textBuffer_Changed);
			}

			void _textBuffer_Changed(object sender, TextChangeArgs args)
			{
				_lastSnapshot = args.OldSnapshot;
			}

			public void Undo()
			{
				_textBuffer.RevertTo(_lastSnapshot);
			}
		}
	}

	public interface IUndoService
	{
		void Undo();
	}
}

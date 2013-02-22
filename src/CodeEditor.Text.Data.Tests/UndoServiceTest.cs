using System.Collections.Generic;
using CodeEditor.Testing;
using CodeEditor.Text.Data.Implementation;
using NUnit.Framework;

namespace CodeEditor.Text.Data.Tests
{
	[TestFixture]
	public class UndoServiceTest : MockBasedTest
	{
		[Test]
		public void UndoInsertion()
		{
			var textBuffer = new TextBuffer("", MockFor<IContentType>().Object);
			
			var undoServiceFactory = new UndoServiceFactory();
			var undoService = undoServiceFactory.ForTextBuffer(textBuffer);

			var oldSnapshot = textBuffer.CurrentSnapshot;
			textBuffer.Insert(0, "text");

			var changes = new List<TextChangeArgs>();
			textBuffer.Changed += (sender, args) => changes.Add(args);
			undoService.Undo();
			Assert.AreEqual(1, changes.Count);

			Assert.AreSame(oldSnapshot, textBuffer.CurrentSnapshot);
		}

		[Test]
		public void OneServicePerBuffer()
		{
			var textBuffer = new TextBuffer("", MockFor<IContentType>().Object);
			
			var undoServiceFactory = new UndoServiceFactory();
			var undoService = undoServiceFactory.ForTextBuffer(textBuffer);
			Assert.AreSame(undoService, undoServiceFactory.ForTextBuffer(textBuffer));	
		}
	}
}

using System.Collections;
using System.Globalization;
using System.Linq;
using CodeEditor.Text.Data;
using CodeEditor.Text.UI.Implementation;
using CodeEditor.Text.UI.Unity.Engine;
using UnityEngine;
using UnityEditor;

namespace CodeEditor.Text.UI.Unity.Editor.Implementation
{
	internal partial class CodeView
	{
		private CompletionSession m_Session;
		private int m_KeyboardControlID;
		private bool m_GrabKeyboardControl = true;

		private readonly CodeEditorWindow m_Owner;
		private readonly ITextViewDocument _document;
		private readonly ITextView _textView;
		private readonly Font _font;
		private readonly ITextStructureNavigator _navigator;

		public CodeView(CodeEditorWindow owner, ITextView textView)
		{
			m_Owner = owner;
			_textView = textView;
			_document = _textView.Document;
			var textFont = _textView.Appearance.Text.font;
			_font = textFont ? textFont : GUI.skin.font;
			_navigator = new DefaultTextStructureNavigator();
			Caret.Moved += EnsureCursorIsVisible;
			_textView.DoubleClicked = DoubleClickedDocument;
		}

		public void Repaint()
		{
			m_Owner.Repaint();
		}

		public void OnGUI(Rect rect)
		{
			SetKeyboardControl(rect);
			HandleCommandEvents();
			_textView.ViewPort = rect;
			_textView.OnGUI();
			HandleCompletionSession();
			HandleKeyboard(true);
		}

		private void SetKeyboardControl(Rect rect)
		{
			m_KeyboardControlID = GUIUtility.GetControlID(FocusType.Keyboard);

			if(m_GrabKeyboardControl || Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
			{
				m_GrabKeyboardControl = false;
				GUIUtility.keyboardControl = m_KeyboardControlID;
				Repaint();
			}
		}

		private void HandleCompletionSession()
		{
			if(m_Session == null)
				return;

			var state = new CodeViewPopUp.PopUpState();
			state.m_ListElements = m_Session.Completions.ToList();
			state.m_OnSelectCallback += CodeCompletionCallback;
			state.m_SelectedCompletion = -1; // Use -1 for invisible marker when showing
			state.m_CodeView = this;
			var spanRect = _textView.SpanForCurrentCharacter();
			var screenPos = new Vector2(spanRect.x, spanRect.y + _textView.LineHeight);
			m_Session = null;
			CodeViewPopUp.ShowAtPosition(screenPos, state);
		}

		public void HandleKeyboard(bool requireKeyboardFocus)
		{
			//if (Event.current.GetTypeForControl (m_KeyboardControlID) != EventType.keyDown)
			if(Event.current.type != EventType.keyDown)
				return;

			if(requireKeyboardFocus && (GUIUtility.keyboardControl != m_KeyboardControlID || !GUI.enabled))
				return;

			if(HandleKeyEvent(Event.current))
			{
				Event.current.Use();
				return;
			}

			var c = Event.current.character;
			if(!_font.HasCharacter(c) && c != '\t' && c != '\n')
			{
				Event.current.Use();
				return;
			}

			if(HasSelection())
				DeleteSelection();

			_document.Insert(_document.CurrentLine.Start + Caret.Column, c.ToString(CultureInfo.InvariantCulture));
			if(c == '\n')
				Caret.SetPosition(Caret.Row + 1, 0);
			else
				Caret.MoveRight();

			Event.current.Use();

			// On tab we auto cycle to next guicontrol here we ensure to grab it back 
			if(c == '\t')
				m_GrabKeyboardControl = true;
		}

		private void CodeCompletionCallback(string selectedText, int selectedIndex)
		{
			m_GrabKeyboardControl = true;
			var line = _document.CurrentLine;
			_document.Buffer.Insert(line.Start + Caret.Column, selectedText.Substring(1));
			Caret.SetPosition(Caret.Row, Caret.Column + selectedText.Length);
			m_Owner.Focus();
		}

		public void StartCompletionSession(CompletionSession session)
		{
			// To be able to calculate the screen pos for the popup we need to do it from an OnGUI code path so
			// we save the session and request a repaint to get an OnGUI event (see LineGUI for handling)
			m_Session = session;
			Repaint();
		}

		private ICaret Caret
		{
			get { return _document.Caret; }
		}
	}

	internal partial class CodeView
	{
		private static Hashtable s_Keyactions;

		private enum TextEditOp
		{
			MoveLeft,
			MoveRight,
			MoveUp,
			MoveDown,
			MoveLineStart,
			MoveLineEnd,
			MoveTextStart,
			MoveTextEnd,
			MovePageUp,
			MovePageDown,
			MoveGraphicalLineStart,
			MoveGraphicalLineEnd,
			MoveWordLeft,
			MoveWordRight,
			MoveParagraphForward,
			MoveParagraphBackward,
			MoveToStartOfNextWord,
			MoveToEndOfPreviousWord,
			SelectLeft,
			SelectRight,
			SelectUp,
			SelectDown,
			SelectTextStart,
			SelectTextEnd,
			SelectPageUp,
			SelectPageDown,
			ExpandSelectGraphicalLineStart,
			ExpandSelectGraphicalLineEnd,
			SelectGraphicalLineStart,
			SelectGraphicalLineEnd,
			SelectWordLeft,
			SelectWordRight,
			SelectToEndOfPreviousWord,
			SelectToStartOfNextWord,
			SelectParagraphBackward,
			SelectParagraphForward,
			Delete,
			Backspace,
			DeleteWordBack,
			DeleteWordForward,
			Cut,
			Copy,
			Paste,
			SelectAll,
			SelectNone,
			ScrollStart,
			ScrollEnd,
			ScrollPageUp,
			ScrollPageDown, 
			Undo,
			Redo
		};

		private bool HandleKeyEvent(Event e)
		{
			InitKeyActions();
			var m = e.modifiers;
			e.modifiers &= ~EventModifiers.CapsLock;
			if(s_Keyactions.Contains(e))
			{
				var op = (TextEditOp) s_Keyactions[e];
				PerformOperation(op);
				e.modifiers = m;
				return true;
			}
			e.modifiers = m;
			return false;
		}

		private void Backspace()
		{
			if(HasSelection())
			{
				DeleteSelection();
				return;
			}

			if(AtTopLeft())
				return;

			Caret.MoveLeft();
			Delete();
		}

		private void Delete()
		{
			if(HasSelection())
			{
				DeleteSelection();
				return;
			}

			var line = _document.CurrentLine;
			_document.Delete(line.Start + Caret.Column, 1);
		}

		private void DeleteSelection()
		{
			int pos, length;
			if(_textView.GetSelectionInDocument(out pos, out length))
			{
				int row, column;
				if(_textView.GetSelectionStart(out row, out column))
					Caret.SetPosition(row, column);

				_document.Delete(pos, length);
				ClearSelection();
			}
		}

		private bool HasSelection()
		{
			return _textView.HasSelection;
		}

		private void SetSelectionAnchorIfNeeded()
		{
			if(!HasSelection())
			{
				_textView.SelectionAnchor = new Position(Caret.Row, Caret.Column);
			}
		}

		private void ClearSelection()
		{
			_textView.SelectionAnchor = new Position(-1, -1);
		}

		private void PreviousWord()
		{
			var span = _navigator.GetSpanFor(AbsoluteCaretPosition, CurrentSnapshot);
			if(span.Start == AbsoluteCaretPosition)
				span = _navigator.GetPreviousSpanFor(span);
			MoveToPosition(span.Start);
		}

		private void NextWord()
		{
			var span = _navigator.GetSpanFor(AbsoluteCaretPosition, CurrentSnapshot);
			var next = _navigator.GetNextSpanFor(span);
			MoveToPosition(next.Start);
		}

		private void DoubleClickedDocument(int row, int column)
		{
			PreviousWord();
			SetSelectionAnchorIfNeeded();
			NextWord();
		}

		private void MoveToPosition(int position)
		{
			var line = LineNumberForPosition(position);
			Caret.SetPosition(line, position - LineStart(line));
		}

		private int AbsoluteCaretPosition
		{
			get { return CurrentLineStart + Caret.Column; }
		}

		private int LineStart(int line)
		{
			return CurrentSnapshot.Lines[line].Start;
		}

		private int LineNumberForPosition(int position)
		{
			return CurrentSnapshot.LineNumberForPosition(position);
		}

		private int CurrentLineStart
		{
			get { return LineStart(Caret.Row); }
		}

		private void EnsureCursorIsVisible()
		{
			_textView.EnsureCursorIsVisible();
		}

		private int VisibleLines
		{
			get { return _textView.VisibleLines(); }
		}

		private bool AtTopLeft()
		{
			return Caret.Row == 0 && Caret.Column == 0;
		}

		private ITextSnapshot CurrentSnapshot
		{
			get { return _document.Buffer.CurrentSnapshot; }
		}

		private void MoveCaretToBeginingOfSelectionAndClear()
		{
			int row, column;
			if(_textView.GetSelectionStart(out row, out column))
				Caret.SetPosition(row, column);
			ClearSelection();
		}

		private void MoveCaretToEndOfSelectionAndClear()
		{
			int row, column;
			if(_textView.GetSelectionEnd(out row, out column))
				Caret.SetPosition(row, column);
			ClearSelection();
		}

		private void PerformOperation(TextEditOp operation)
		{
			switch(operation)
			{
				case TextEditOp.MoveLeft:
					if(HasSelection())
						MoveCaretToBeginingOfSelectionAndClear();
					else
						Caret.MoveLeft();
					break;
				case TextEditOp.MoveRight:
					if(HasSelection())
						MoveCaretToEndOfSelectionAndClear();
					else
						Caret.MoveRight();
					break;
				case TextEditOp.MoveUp:
					if(HasSelection())
						MoveCaretToBeginingOfSelectionAndClear();
					else
						Caret.MoveUp(1);
					break;
				case TextEditOp.MoveDown:
					if(HasSelection())
						MoveCaretToEndOfSelectionAndClear();
					else
						Caret.MoveDown(1);
					break;
				case TextEditOp.MoveLineStart:
					ClearSelection();
					Caret.MoveToRowStart();
					break;
				case TextEditOp.MoveLineEnd:
					ClearSelection();
					Caret.MoveToRowEnd();
					break;
					//case TextEditOp.MoveWordRight: MoveWordRight(); break;
				case TextEditOp.MoveToStartOfNextWord:
					ClearSelection();
					NextWord();
					break;
				case TextEditOp.MoveToEndOfPreviousWord:
					ClearSelection();
					PreviousWord();
					break;
					//case TextEditOp.MoveWordLeft: MoveWordLeft(); break;
				case TextEditOp.MoveTextStart:
					ClearSelection();
					Caret.MoveToStart();
					break;
				case TextEditOp.MoveTextEnd:
					ClearSelection();
					Caret.MoveToEnd();
					break;
					//case TextEditOp.MoveParagraphForward: MoveParagraphForward(); break;
					//case TextEditOp.MoveParagraphBackward: MoveParagraphBackward(); break;
				case TextEditOp.MovePageUp:
					ClearSelection();
					Caret.MoveUp(VisibleLines - 2);
					break;
				case TextEditOp.MovePageDown:
					ClearSelection();
					Caret.MoveDown(VisibleLines - 2);
					break;
				case TextEditOp.MoveGraphicalLineStart:
					ClearSelection();
					Caret.MoveToRowStart();
					break;
				case TextEditOp.MoveGraphicalLineEnd:
					ClearSelection();
					Caret.MoveToRowEnd();
					break;
				case TextEditOp.SelectLeft:
					SetSelectionAnchorIfNeeded();
					Caret.MoveLeft();
					break;
				case TextEditOp.SelectRight:
					SetSelectionAnchorIfNeeded();
					Caret.MoveRight();
					break;
				case TextEditOp.SelectUp:
					SetSelectionAnchorIfNeeded();
					Caret.MoveUp(1);
					break;
				case TextEditOp.SelectDown:
					SetSelectionAnchorIfNeeded();
					Caret.MoveDown(1);
					break;
				case TextEditOp.SelectToStartOfNextWord:
					SetSelectionAnchorIfNeeded();
					NextWord();
					break;
				case TextEditOp.SelectToEndOfPreviousWord:
					SetSelectionAnchorIfNeeded();
					PreviousWord();
					break;
				case TextEditOp.SelectTextStart:
					SetSelectionAnchorIfNeeded();
					Caret.MoveToStart();
					break;
				case TextEditOp.SelectTextEnd:
					SetSelectionAnchorIfNeeded();
					Caret.MoveToEnd();
					break;
				case TextEditOp.SelectPageUp:
					SetSelectionAnchorIfNeeded();
					Caret.MoveUp(VisibleLines - 2);
					break;
				case TextEditOp.SelectPageDown:
					SetSelectionAnchorIfNeeded();
					Caret.MoveDown(VisibleLines - 2);
					break;
				case TextEditOp.SelectGraphicalLineStart:
					SetSelectionAnchorIfNeeded();
					Caret.MoveToRowStart();
					break;
				case TextEditOp.SelectGraphicalLineEnd:
					SetSelectionAnchorIfNeeded();
					Caret.MoveToRowEnd();
					break;

					//case TextEditOp.ExpandSelectGraphicalLineStart: ExpandSelectGraphicalLineStart(); break;
					//case TextEditOp.ExpandSelectGraphicalLineEnd: ExpandSelectGraphicalLineEnd(); break;
					//case TextEditOp.SelectParagraphForward: SelectParagraphForward(); break;
					//case TextEditOp.SelectParagraphBackward: SelectParagraphBackward(); break;
				case TextEditOp.Delete:
					Delete();
					return;
				case TextEditOp.Backspace:
					Backspace();
					return;
					//case TextEditOp.Cut: return Cut();
				case TextEditOp.Copy:
					Copy();
					return;
				case TextEditOp.Paste:
					Paste();
					return;
					//case TextEditOp.SelectAll: SelectAll(); break;
					//case TextEditOp.SelectNone: SelectNone(); break;
					//case TextEditOp.ScrollStart: return ScrollStart(); break;
					//case TextEditOp.ScrollEnd: return ScrollEnd(); break;
					//case TextEditOp.ScrollPageUp: return ScrollPageUp(); break;
					//case TextEditOp.ScrollPageDown: return ScrollPageDown(); break;
					//case TextEditOp.DeleteWordBack: return DeleteWordBack(); // break; // The uncoditional return makes the "break;" issue a warning about unreachable code
					//case TextEditOp.DeleteWordForward: return DeleteWordForward(); // break; // The uncoditional return makes the "break;" issue a warning about unreachable code
				case TextEditOp.Undo:
					Undo();
					break;
				//case TextEditOp.Redo:
					/*Redo();*/
					//break;

				default:
					Debug.Log("Unimplemented: " + operation);
					break;
			}
		}

		private static void MapKey(string key, TextEditOp action)
		{
			s_Keyactions[Event.KeyboardEvent(key)] = action;
		}

		// Set up a platform independant keyboard->Edit action map. This varies depending on whether we are on mac or windows.
		// Info: # is shift, % is command, ^ is control, & is alt
		private void InitKeyActions()
		{
			if(s_Keyactions != null)
				return;
			s_Keyactions = new Hashtable();

			// key mappings shared by the platforms
			MapKey("left", TextEditOp.MoveLeft);
			MapKey("right", TextEditOp.MoveRight);
			MapKey("up", TextEditOp.MoveUp);
			MapKey("down", TextEditOp.MoveDown);

			MapKey("#left", TextEditOp.SelectLeft);
			MapKey("#right", TextEditOp.SelectRight);
			MapKey("#up", TextEditOp.SelectUp);
			MapKey("#down", TextEditOp.SelectDown);

			MapKey("delete", TextEditOp.Delete);
			MapKey("backspace", TextEditOp.Backspace);
			MapKey("#backspace", TextEditOp.Backspace);
			MapKey("^u", TextEditOp.Undo);
			MapKey("#^u", TextEditOp.Redo);

			if(Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsWebPlayer &&
			   Application.platform != RuntimePlatform.WindowsEditor)
			{
				// Keyboard mappings for mac
				MapKey("home", TextEditOp.ScrollStart);
				MapKey("end", TextEditOp.ScrollEnd);
				// TODO		MapKey ("page up", TextEditOp.ScrollPageUp);
				// TODO		MapKey ("page down", TextEditOp.ScrollPageDown);

				MapKey("^left", TextEditOp.MoveGraphicalLineStart);
				MapKey("^right", TextEditOp.MoveGraphicalLineEnd);
				// TODO		MapKey ("^up", TextEditOp.ScrollPageUp);
				// TODO		MapKey ("^down", TextEditOp.ScrollPageDown);

				MapKey("&left", TextEditOp.MoveWordLeft);
				MapKey("&right", TextEditOp.MoveWordRight);
				MapKey("&up", TextEditOp.MoveParagraphBackward);
				MapKey("&down", TextEditOp.MoveParagraphForward);

				MapKey("%left", TextEditOp.MoveGraphicalLineStart);
				MapKey("%right", TextEditOp.MoveGraphicalLineEnd);
				MapKey("%up", TextEditOp.MoveTextStart);
				MapKey("%down", TextEditOp.MoveTextEnd);
				MapKey("&home", TextEditOp.MoveTextStart);
				MapKey("&end", TextEditOp.MoveTextEnd);

				MapKey("#home", TextEditOp.SelectTextStart);
				MapKey("#end", TextEditOp.SelectTextEnd);
				// TODO			MapKey ("#page up", TextEditOp.SelectPageUp);
				// TODO			MapKey ("#page down", TextEditOp.SelectPageDown);

				MapKey("#^left", TextEditOp.ExpandSelectGraphicalLineStart);
				MapKey("#^right", TextEditOp.ExpandSelectGraphicalLineEnd);
				MapKey("#^up", TextEditOp.SelectParagraphBackward);
				MapKey("#^down", TextEditOp.SelectParagraphForward);

				MapKey("#&left", TextEditOp.SelectWordLeft);
				MapKey("#&right", TextEditOp.SelectWordRight);
				MapKey("#&up", TextEditOp.SelectParagraphBackward);
				MapKey("#&down", TextEditOp.SelectParagraphForward);

				MapKey("#%left", TextEditOp.ExpandSelectGraphicalLineStart);
				MapKey("#%right", TextEditOp.ExpandSelectGraphicalLineEnd);
				MapKey("#%up", TextEditOp.SelectTextStart);
				MapKey("#%down", TextEditOp.SelectTextEnd);

				MapKey("%a", TextEditOp.SelectAll);
				MapKey("%x", TextEditOp.Cut);
				MapKey("%c", TextEditOp.Copy);
				MapKey("%v", TextEditOp.Paste);

				// emacs-like keybindings
				MapKey("^d", TextEditOp.Delete);
				MapKey("^h", TextEditOp.Backspace);
				MapKey("^b", TextEditOp.MoveLeft);
				MapKey("^f", TextEditOp.MoveRight);
				MapKey("^a", TextEditOp.MoveLineStart);
				MapKey("^e", TextEditOp.MoveLineEnd);
				// Can't be bothered to do these
				// MapKey ("^o", TextEditOp.InsertNewlineRight);
				// MapKey ("^t", TextEditOp.TransposeCharacters);

				MapKey("&delete", TextEditOp.DeleteWordForward);
				MapKey("&backspace", TextEditOp.DeleteWordBack);
			}
			else
			{
				// Windows keymappings
				MapKey("home", TextEditOp.MoveGraphicalLineStart);
				MapKey("end", TextEditOp.MoveGraphicalLineEnd);
				MapKey("page up", TextEditOp.MovePageUp);
				MapKey("page down", TextEditOp.MovePageDown);
				MapKey("^home", TextEditOp.MoveTextStart);
				MapKey("^end", TextEditOp.MoveTextEnd);

				MapKey("%left", TextEditOp.MoveWordLeft);
				MapKey("%right", TextEditOp.MoveWordRight);
				MapKey("%up", TextEditOp.MoveParagraphBackward);
				MapKey("%down", TextEditOp.MoveParagraphForward);

				MapKey("^left", TextEditOp.MoveToEndOfPreviousWord);
				MapKey("^right", TextEditOp.MoveToStartOfNextWord);
				MapKey("^up", TextEditOp.MoveParagraphBackward);
				MapKey("^down", TextEditOp.MoveParagraphForward);

				MapKey("#^left", TextEditOp.SelectToEndOfPreviousWord);
				MapKey("#^right", TextEditOp.SelectToStartOfNextWord);
				MapKey("#^up", TextEditOp.SelectParagraphBackward);
				MapKey("#^down", TextEditOp.SelectParagraphForward);

				MapKey("#home", TextEditOp.SelectGraphicalLineStart);
				MapKey("#end", TextEditOp.SelectGraphicalLineEnd);
				MapKey("#^home", TextEditOp.SelectTextStart);
				MapKey("#^end", TextEditOp.SelectTextEnd);

				// TODO			MapKey ("#page up", TextEditOp.SelectPageUp);
				// TODO			MapKey ("#page down", TextEditOp.SelectPageDown);

				MapKey("^delete", TextEditOp.DeleteWordForward);
				MapKey("^backspace", TextEditOp.DeleteWordBack);

				MapKey("^a", TextEditOp.SelectAll);
				MapKey("^x", TextEditOp.Cut);
				MapKey("^c", TextEditOp.Copy);
				MapKey("^v", TextEditOp.Paste);
				MapKey("#delete", TextEditOp.Cut);
				MapKey("^insert", TextEditOp.Copy);
				MapKey("#insert", TextEditOp.Paste);
			}
		}

		private string GetText(int pos, int length)
		{
			// TODO: Document should have interface for getting text... (like it has for insert)
			return _document.Buffer.CurrentSnapshot.GetText(pos, length);
		}

		private void Copy()
		{
			int pos, length;
			if(_textView.GetSelectionInDocument(out pos, out length))
				EditorGUIUtility.systemCopyBuffer = GetText(pos, length);
		}

		private void Cut()
		{
			int pos, length;
			if(_textView.GetSelectionInDocument(out pos, out length))
			{
				EditorGUIUtility.systemCopyBuffer = GetText(pos, length);
				DeleteSelection();
			}
		}

		private void Paste()
		{
			string pasteText = EditorGUIUtility.systemCopyBuffer;
			if(pasteText != "")
			{
				ReplaceSelection(pasteText);

				// We need interface for moving the caret
				for(int i = 0; i < pasteText.Length; ++i)
					Caret.MoveRight();
			}
		}

		private void Undo()
		{
			_document.Undo();
		}

		public void ReplaceSelection(string replace)
		{
			DeleteSelection();
			_document.Insert(_document.CurrentLine.Start + Caret.Column, replace);
		}


		private void HandleCommandEvents()
		{
			Event evt = Event.current;
			switch(evt.GetTypeForControl(m_KeyboardControlID))
			{
				case EventType.ValidateCommand:
					if(GUIUtility.keyboardControl == m_KeyboardControlID)
					{
						switch(evt.commandName)
						{
							case "Cut":
							case "Copy":
								if(HasSelection())
									evt.Use();
								break;
							case "Paste":
								evt.Use();
								break;
							case "SelectAll":
								evt.Use();
								break;
							case "UndoRedoPerformed":
								Debug.Log("Not implemented: (UndoRedoPerformed)");
								evt.Use();
								break;
						}
					}
					break;
				case EventType.ExecuteCommand:
					if(GUIUtility.keyboardControl == m_KeyboardControlID)
					{
						switch(evt.commandName)
						{
							case "Cut":
								if(HasSelection())
									Cut();
								evt.Use();
								break;
							case "Copy":
								if(HasSelection())
									Copy();
								evt.Use();
								break;
							case "Paste":
								Paste();
								evt.Use();
								break;
							case "SelectAll":
								//SelectAll ();
								evt.Use();
								break;
						}
					}
					break;
			}
		}
	}
}

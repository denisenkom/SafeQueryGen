using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * Код для формирования кода для компилятора C++
 * 
 * Код формируется с учетом отступов
 */

namespace SafeQueryGen
{
	enum AccessModifierType
	{
		None,
		Public,
		Private,
		Protected,
	}

	enum CurrentPositionType
	{
		AtBegining,
		Idented,
		Inline,
		AtSingleLineComment,
	}

	class CppWriter
	{
		private StreamWriter _stream;
		private Int32 _ident = 0;
		private CurrentPositionType _position = CurrentPositionType.AtBegining;
		private Stack<String> _classesStack = new Stack<String>();

		public CppWriter(String outputFileName, Encoding encoding)
		{
			_stream = new StreamWriter(outputFileName, false, encoding);
		}

		private void NewLine()
		{
			if (_position != CurrentPositionType.AtBegining)
				_stream.WriteLine();
		}

		private void NewLineWithIdent()
		{
			NewLine();
			WriteIdent();
		}

		private void WriteIdent()
		{
			Debug.Assert(_position == CurrentPositionType.AtBegining, "Should be at begining");
			if (_ident > 0)
				_stream.Write(new String('\t', _ident));
		}

		private String ModifierString(AccessModifierType accessModifier)
		{
			switch (accessModifier)
			{
				case AccessModifierType.Private:
					return "private";
				case AccessModifierType.Protected:
					return "protected";
				case AccessModifierType.Public:
					return "public";
				default:
					throw new ArgumentOutOfRangeException("modifier", accessModifier.ToString(), "Unknown enumeration");
			}
		}

		public void AppendComment(String comment)
		{
			if (_position == CurrentPositionType.AtBegining)
			{
				WriteIdent();
			}
			if (_position != CurrentPositionType.AtBegining &&
				_position != CurrentPositionType.Idented &&
				_position != CurrentPositionType.AtSingleLineComment)
			{
				_stream.Write(' ');
			}
			if (_position != CurrentPositionType.AtSingleLineComment)
			{
				_stream.Write("// ");
			}
			_stream.Write(comment);
			_position = CurrentPositionType.AtSingleLineComment;
		}

		public void BeginClass(String className)
		{
			BeginClass(className, null, AccessModifierType.None);
		}

		public void BeginClass(String className, String baseName)
		{
			BeginClass(className, baseName, AccessModifierType.Public);
		}

		public void BeginClass(String className, String baseName, AccessModifierType access)
		{
			NewLineWithIdent();
			if (baseName != null)
			{
				_stream.Write("class {0} : {1} {2} {{", className, ModifierString(access), baseName);
			}
			else
			{
				_stream.Write("class {0} {{", className);
			}
			_ident++;
			_position = CurrentPositionType.Inline;
			_classesStack.Push(className);
		}

		public void BeginAccessSection(AccessModifierType modifier)
		{
			NewLine();
			_stream.Write(ModifierString(modifier));
			_stream.Write(':');
			_position = CurrentPositionType.Inline;
		}

		public void EndClass()
		{
			_classesStack.Pop();
			_ident--;
			NewLineWithIdent();
			_stream.Write("}");
			_position = CurrentPositionType.Inline;
		}

		/*struct ParameterItem
		{
			public String Type;
			public String Name;
		}

		struct InitializationItem
		{
			public String What;
			public String Expression;
		}

		private void BeginConstructor(ParameterItem[] parameters, InitializationItem[] initializations)
		{
			NewLineWithIdent();
			_stream.WriteLine("{0}({1})", _classesStack.Peek());
		}*/
	}
}

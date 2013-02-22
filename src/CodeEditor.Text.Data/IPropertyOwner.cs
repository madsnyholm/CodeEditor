using System;

namespace CodeEditor.Text.Data
{
	public interface IPropertyOwner
	{
		IPropertyCollection Properties { get; }
	}

	public interface IPropertyCollection
	{
		T GetOrCreateSingletonProperty<T>(Func<T> func);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using EnvDTE;
using EnvDTE80;

namespace BuildOnSave
{
	class ErrorListHelper
	{
		private readonly ErrorListProvider _errorListProvider;

		private static ErrorListHelper mInstance = null;

		public ErrorListHelper(IServiceProvider serviceProvider)
		{
			_errorListProvider = new ErrorListProvider(serviceProvider);
		}

		public static void Init(IServiceProvider ServiceProvider)
		{	
			if (mInstance == null)
				mInstance = new ErrorListHelper(ServiceProvider);
		}

		public static ErrorListHelper Instance()
		{
			return mInstance;
		}

		public void ShowErrorList()
		{
			_errorListProvider.Show();
		}

		public void AddError(TaskCategory taskCategory, TaskErrorCategory errorCategory, string message, string filePath, int line, int column)
		{
			// Create a new error task item
			ErrorTask errorTask = new ErrorTask
			{
				Category = taskCategory,
				ErrorCategory = errorCategory,
				Text = message,
				Document = filePath,
				Line = line - 1,  // Line index is zero-based
				Column = column - 1
			};

			// Set navigation action when the user double-clicks the error
			errorTask.Navigate += (s, e) =>
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				NavigateToFile(filePath, line, column);
			};

			// Add the error task to the provider
			_errorListProvider.Tasks.Add(errorTask);
		}

		public void ClearAllTasks()
		{
			_errorListProvider.Tasks.Clear();
		}

		public void ClearTasksByCategory(TaskCategory taskCategory)
		{
			for (int Index = 0; Index < _errorListProvider.Tasks.Count; Index++)
			{
				if (_errorListProvider.Tasks[Index].Category == taskCategory)
				{
					_errorListProvider.Tasks.RemoveAt(Index);
					--Index;
				}
			}
		}

		public void ClearTasksByFilepath(string filePath)
		{
			for (int Index = 0; Index < _errorListProvider.Tasks.Count; Index++)
			{
				if (_errorListProvider.Tasks[Index].Document == filePath)
				{
					_errorListProvider.Tasks.RemoveAt(Index);
					--Index;
				}
			}
		}

		public void ClearTasksExceptForFilepaths(HashSet<string> filePaths)
		{
			for (int Index = 0; Index < _errorListProvider.Tasks.Count; Index++)
			{
				if (!filePaths.Contains(_errorListProvider.Tasks[Index].Document))
				{
					_errorListProvider.Tasks.RemoveAt(Index);
					--Index;
				}
			}
		}

		private void NavigateToFile(string filePath, int line, int column)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Ensure file exists
			if (!System.IO.File.Exists(filePath))
				return;

			// Open the document in Visual Studio
			IVsUIHierarchy hierarchy;
			uint itemID;
			IVsWindowFrame windowFrame;
			VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, filePath, Guid.Empty, out hierarchy, out itemID, out windowFrame);

			if (windowFrame != null)
			{Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread("onBeforeSaveAsCommand");
				windowFrame.Show(); // Bring the document to the foreground

				// Get the IVsTextView to move the cursor
				MoveCursorWithDTE(filePath, line, column);
			}
		}
		private static void MoveCursorWithDTE(string filePath, int line, int column)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			try
			{
				DTE2 dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;

				// Get the active document selection
				TextSelection selection = (TextSelection)dte.ActiveDocument.Selection;
				if (selection != null)
				{
					selection.MoveToLineAndOffset(line, column + 1);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("MoveCursorWithDTE failed: " + ex.Message);
			}
		}
	}
}
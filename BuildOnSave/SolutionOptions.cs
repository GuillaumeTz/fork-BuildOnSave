using System.Collections.Generic;
using System;

namespace BuildOnSave
{
	enum BuildType
	{
		Solution,
		StartupProject,
	}

	sealed class SolutionOptions
	{
		public bool Enabled;
		public BuildType BuildType;
		public bool DisableWhenDebugging;
		public bool RelaunchNewBuildWhenSaved;
		public List<string> DoNotRunIfProcessExistList = new List<string>();
	}
}

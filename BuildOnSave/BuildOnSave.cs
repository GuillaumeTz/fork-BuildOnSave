using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;

namespace BuildOnSave
{
	sealed class BuildOnSave
	{
		const int CommandId = 0x0100;
		const int TopMenuCommandId = 0x1021;
		const int BuildTypeSolutionCommandId = 0x101;
		const int BuildTypeStartupProjectCommandId = 0x102;
		const int DisableWhenDebuggingCommandId = 0x103;
		const int RelaunchNewBuildWhenSavedCommandId = 0x104;
		static readonly Guid CommandSet = new Guid("e2f191eb-1c5a-4d3c-adfb-d5b14dc47078");

		readonly DTE _dte;
		readonly MenuCommand _topMenu;
		readonly MenuCommand _menuItem;
		readonly MenuCommand _buildTypeSolution;
		readonly MenuCommand _buildTypeStartupProject;
		readonly MenuCommand _disableWhenDebugging;
		readonly MenuCommand _relaunchNewBuildWhenSaved;

		readonly Window _outputWindow;
		readonly OutputWindowPane _outputPane;

		// stored to prevent GC from collecting
		readonly Events _events;
		readonly DocumentEvents _documentEvents;
		readonly BuildEvents _buildEvents;
		readonly CommandEvents _buildSolutionEvent;
		readonly DebuggerEvents _debuggerEvents;


		// state
		SolutionOptions _solutionOptions;
		Driver _driver_;

		public BuildOnSave(Package package)
		{
			IServiceProvider serviceProvider = package;
			_dte = serviceProvider.GetService(typeof(DTE)) as DTE;
			_events = _dte.Events;
			_documentEvents = _events.DocumentEvents;
			_buildEvents = _events.BuildEvents;
			_debuggerEvents = _events.DebuggerEvents;
			var guid = typeof(VSConstants.VSStd97CmdID).GUID.ToString("B");
			_buildSolutionEvent = _dte.Events.CommandEvents[guid, (int)VSConstants.VSStd97CmdID.BuildSln];

			var commandService = serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

			_topMenu = new MenuCommand(delegate { }, 
					new CommandID(CommandSet, TopMenuCommandId));
			_menuItem = new MenuCommand(enableDisableBuildOnSave, 
					new CommandID(CommandSet, CommandId));
			_disableWhenDebugging = new MenuCommand(enableDisableWhenDebugging,
					new CommandID(CommandSet, DisableWhenDebuggingCommandId));
			_relaunchNewBuildWhenSaved = new MenuCommand(enableRelaunchNewBuildWhenSaved,
				new CommandID(CommandSet, RelaunchNewBuildWhenSavedCommandId));
			_buildTypeSolution = 
					new MenuCommand(setBuildTypeToSolution, 
						new CommandID(CommandSet, BuildTypeSolutionCommandId));
			_buildTypeStartupProject = 
					new MenuCommand(setBuildTypeToStartupProject, 
						new CommandID(CommandSet, BuildTypeStartupProjectCommandId));

			commandService.AddCommand(_topMenu);
			commandService.AddCommand(_menuItem);
			commandService.AddCommand(_disableWhenDebugging);
			commandService.AddCommand(_relaunchNewBuildWhenSaved);
			commandService.AddCommand(_buildTypeSolution);
			commandService.AddCommand(_buildTypeStartupProject);

			// create the output pane.

			_outputWindow = _dte.Windows.Item(Constants.vsWindowKindOutput);
			_outputPane = ((OutputWindow)_outputWindow.Object).OutputWindowPanes.Add("BuildOnSave");

			_topMenu.Visible = true;
			_solutionOptions = DefaultOptions;

			Log.I("BuildOnSave initialized");

			syncOptions();
		}

		void setBuildTypeToSolution(object sender, EventArgs e)
		{
			setBuildTypeTo(BuildType.Solution);
		}

		void setBuildTypeToStartupProject(object sender, EventArgs e)
		{
			setBuildTypeTo(BuildType.StartupProject);
		}

		void setBuildTypeTo(BuildType buildType)
		{
			_solutionOptions.BuildType = buildType;
			syncOptions();
		}

		public void solutionOpened()
		{
			_topMenu.Visible = true;
		}

		public void solutionClosed()
		{
			SolutionOptions = DefaultOptions;
			_topMenu.Visible = false;
		}

		void enableDisableBuildOnSave(object sender, EventArgs e)
		{
			_solutionOptions.Enabled = !_solutionOptions.Enabled;
			syncOptions();
		}
		
		void enableDisableWhenDebugging(object sender, EventArgs e)
		{
			_solutionOptions.DisableWhenDebugging = !_solutionOptions.DisableWhenDebugging;
			syncOptions();
		}

		void enableRelaunchNewBuildWhenSaved(object sender, EventArgs e)
		{
			_solutionOptions.RelaunchNewBuildWhenSaved = !_solutionOptions.RelaunchNewBuildWhenSaved;
			syncOptions();
		}

		void syncOptions()
		{
			var options = _solutionOptions;

			if (options.Enabled)
				connectDriver(options);
			else
				disconnectDriver();

			if (_driver_ != null && _driver_._options != options)
			{
				disconnectDriver();
				connectDriver(options);
			}

			_menuItem.Checked = _driver_ != null;
			_buildTypeSolution.Checked = options.BuildType == BuildType.Solution;
			_buildTypeSolution.Enabled = _driver_ != null;
			_buildTypeStartupProject.Checked = options.BuildType == BuildType.StartupProject;
			_buildTypeStartupProject.Enabled = _driver_ != null;
			_disableWhenDebugging.Checked = options.DisableWhenDebugging;
			_disableWhenDebugging.Enabled = _driver_ != null;
			_relaunchNewBuildWhenSaved.Checked = options.RelaunchNewBuildWhenSaved;
			_relaunchNewBuildWhenSaved.Enabled = _driver_ != null;
		}

		void connectDriver(SolutionOptions options)
		{
			if (_driver_ != null)
				return;

			var backgroundBuild = new BackgroundBuild2(_dte, _outputPane);
			var ui = new DriverUI(_dte, _outputWindow, _outputPane);
			var driver = new Driver(_dte, options, backgroundBuild, ui);

			_documentEvents.DocumentSaved += driver.onDocumentSaved;

			_buildEvents.OnBuildBegin += driver.onBuildBegin;
			_buildEvents.OnBuildDone += driver.onBuildDone;

			_buildSolutionEvent.BeforeExecute += driver.onBeforeBuildSolutionCommand;
			_buildSolutionEvent.AfterExecute += driver.onAfterBuildSolutionCommand;

			_debuggerEvents.OnEnterRunMode += driver.onDebuggerEnterRunMode;
			_debuggerEvents.OnEnterDesignMode += driver.onDebuggerEnterDesignMode;

			_driver_ = driver;

			Log.D("driver connected");
		}

		void disconnectDriver()
		{
			var driver = _driver_;
			if (driver == null)
				return;

			_documentEvents.DocumentSaved -= driver.onDocumentSaved;

			_buildEvents.OnBuildBegin -= driver.onBuildBegin;
			_buildEvents.OnBuildDone -= driver.onBuildDone;

			_buildSolutionEvent.BeforeExecute -= driver.onBeforeBuildSolutionCommand;
			_buildSolutionEvent.AfterExecute -= driver.onAfterBuildSolutionCommand;

			_debuggerEvents.OnEnterRunMode -= driver.onDebuggerEnterRunMode;
			_debuggerEvents.OnEnterDesignMode -= driver.onDebuggerEnterDesignMode;

			_driver_.Dispose();
			_driver_ = null;

			Log.D("driver disconnected");
		}


		public SolutionOptions SolutionOptions
		{
			get { return _solutionOptions; }
			set
			{
				_solutionOptions = value;
				syncOptions();
			}
		}

		static readonly SolutionOptions DefaultOptions = new SolutionOptions
		{
			Enabled = true,
			BuildType = BuildType.Solution,
			DisableWhenDebugging = false,
			RelaunchNewBuildWhenSaved = true,
		};
	}

	static class Helpers
	{
		public static void reportException(this OutputWindowPane pane, Exception e)
		{
			pane.OutputString($"---------- BuildOnSave CRASHED, please report to https://www.github.com/pragmatrix/BuildOnSave/issues\n");
			pane.OutputString(e.ToString());
		}
	}
}
